using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterFamilyCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId AdoptiveParent = new("character:test/adoptive_parent");
    private static readonly EntityId Adopted = new("character:test/adopted");
    private static readonly EntityId BiologicalParent = new("character:test/biological_parent");
    private static readonly EntityId Sibling = new("character:test/sibling");
    private static readonly EntityId StrictPracticeId = new("marriage_practice:test/family_strict");
    private static readonly EntityId PermissivePracticeId = new("marriage_practice:test/family_permissive");
    private readonly ITestOutputHelper output;

    public CharacterFamilyCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void PrepareFamilyAction_AddsOnlyCanonicalLegalParentageAndAppliesInPlace()
    {
        CharacterParentLink biological = new(
            BiologicalParent,
            ParentChildLinkKind.Biological);
        CharacterWorldState world = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(170, 1, 1)),
            Seed(BiologicalParent, new CampaignDate(160, 1, 1)),
            Seed(Adopted, new CampaignDate(190, 1, 1), parents: [biological]));
        IAuthoritativeCharacterWorldQuery queryReference = world;
        CharacterWorldSnapshot before = world.CaptureSnapshot();
        CharacterParentLink[] expected = [biological with { }];
        EntityId commandId = new("command:test/establish-adoption");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);

        CharacterFamilyMutationPlan plan = world.PrepareFamilyAction(
            new EstablishLegalAdoptiveParentAction(
                AdoptiveParent,
                Adopted,
                expected),
            Date,
            7,
            commandId,
            eventId);

        Assert.Equal(CharacterFamilyContractVersions.Change, plan.Change.ContractVersion);
        Assert.Equal(
            CharacterFamilyIds.DeriveParentageChangeId(eventId, Adopted),
            plan.Change.ChangeId);
        Assert.Equal(Adopted, plan.Change.AdoptedCharacterId);
        Assert.Equal([biological], plan.Change.PreviousParentLinks);
        Assert.Equal(
            [
                new CharacterParentLink(
                    AdoptiveParent,
                    ParentChildLinkKind.LegalAdoptive),
                biological,
            ],
            plan.Change.CurrentParentLinks);
        Assert.Equal(Date, plan.Change.ResolutionDate);
        Assert.Equal(7, plan.Change.ResolutionTurnIndex);
        Assert.Equal(commandId, plan.Change.SourceCommandId);

        Assert.True(world.TryGetCharacterProfile(Adopted, out AuthoritativeCharacterProfile? unchanged));
        Assert.Equal([biological], unchanged.ParentLinks);
        CharacterWorldSnapshot candidate = plan.CharacterPlan.Candidate.CaptureSnapshot();
        Assert.Equal(
            Serialize(before.CharacterDefinitions),
            Serialize(candidate.CharacterDefinitions));
        Assert.Equal(Serialize(before.FamilyStates), Serialize(candidate.FamilyStates));
        Assert.Equal(Serialize(before.HouseholdStates), Serialize(candidate.HouseholdStates));
        Assert.Equal(
            before.CharacterStates.Single(state => state.CharacterId == Adopted).Condition,
            candidate.CharacterStates.Single(state => state.CharacterId == Adopted).Condition);

        expected[0] = new CharacterParentLink(
            AdoptiveParent,
            ParentChildLinkKind.LegalAdoptive);
        Assert.Equal([biological], plan.Change.PreviousParentLinks);

        world.ApplyPrepared(plan.CharacterPlan);

        Assert.True(queryReference.TryGetCharacterProfile(
            Adopted,
            out AuthoritativeCharacterProfile? adopted));
        Assert.Equal(plan.Change.CurrentParentLinks, adopted.ParentLinks);
        Assert.Equal(
            new CharacterChildLink(Adopted, ParentChildLinkKind.LegalAdoptive),
            Assert.Single(queryReference.Profiles
                .Single(profile => profile.CharacterId == AdoptiveParent)
                .ChildLinks));
    }

    [Fact]
    public void PrepareFamilyAction_UsesExactBirthdayAndAdopteeCapacityRules()
    {
        CharacterConditionState incapacitated = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
        };
        CharacterWorldState exactAdultAndMinor = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(182, 5, 10)),
            Seed(Adopted, new CampaignDate(190, 5, 10), incapacitated));

        CharacterFamilyMutationPlan accepted = Prepare(
            exactAdultAndMinor,
            AdoptiveParent,
            Adopted,
            [],
            "exact-adult-minor");

        Assert.Single(accepted.Change.CurrentParentLinks);

        CharacterWorldState underageParent = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(182, 5, 11)),
            Seed(Adopted, new CampaignDate(190, 5, 10)));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            underageParent,
            AdoptiveParent,
            Adopted,
            [],
            "underage-parent"));

        CharacterWorldState incapacitatedAdult = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(160, 1, 1)),
            Seed(Adopted, new CampaignDate(180, 1, 1), incapacitated));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            incapacitatedAdult,
            AdoptiveParent,
            Adopted,
            [],
            "incapacitated-adult"));
    }

    [Fact]
    public void PrepareFamilyAction_RejectsInvalidIdentityExpectedStateAndConditions()
    {
        CharacterParentLink biological = new(
            BiologicalParent,
            ParentChildLinkKind.Biological);
        CharacterWorldState world = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(160, 1, 1)),
            Seed(BiologicalParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(190, 1, 1), parents: [biological]));

        Assert.Throws<SimulationValidationException>(() => Prepare(
            world,
            AdoptiveParent,
            Adopted,
            [],
            "stale"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            world,
            Adopted,
            Adopted,
            [biological],
            "self"));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            world,
            BiologicalParent,
            Adopted,
            [biological],
            "duplicate-parent"));
        Assert.Throws<SimulationValidationException>(() => world.PrepareFamilyAction(
            new EstablishLegalAdoptiveParentAction(
                AdoptiveParent,
                Adopted,
                [biological]),
            Date,
            0,
            new EntityId("command:test/wrong-event"),
            new EntityId("event:test/wrong")));
        Assert.Throws<SimulationValidationException>(() => world.PrepareFamilyAction(
            new EstablishLegalAdoptiveParentAction(
                AdoptiveParent,
                Adopted,
                null!),
            Date,
            0,
            new EntityId("command:test/null-expected"),
            CharacterFamilyIds.DeriveActionEventId(
                Date,
                new EntityId("command:test/null-expected"))));

        CharacterWorldState captiveAdoptee = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(160, 1, 1)),
            Seed(
                Adopted,
                new CampaignDate(190, 1, 1),
                CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = AdoptiveParent,
                }));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            captiveAdoptee,
            AdoptiveParent,
            Adopted,
            [],
            "captive-adoptee"));
    }

    [Fact]
    public void PrepareFamilyAction_EnforcesActionLocalParentCaps()
    {
        EntityId firstLegal = new("character:test/first_legal");
        EntityId secondLegal = new("character:test/second_legal");
        CharacterParentLink[] twoLegal =
        [
            new(firstLegal, ParentChildLinkKind.LegalAdoptive),
            new(secondLegal, ParentChildLinkKind.LegalAdoptive),
        ];
        CharacterWorldState legalLimit = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(firstLegal, new CampaignDate(151, 1, 1)),
            Seed(secondLegal, new CampaignDate(152, 1, 1)),
            Seed(Adopted, new CampaignDate(190, 1, 1), parents: twoLegal));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            legalLimit,
            AdoptiveParent,
            Adopted,
            twoLegal,
            "legal-limit"));

        EntityId firstBiological = new("character:test/first_biological");
        EntityId secondBiological = new("character:test/second_biological");
        EntityId thirdBiological = new("character:test/third_biological");
        CharacterParentLink[] fourTotal =
        [
            new(firstBiological, ParentChildLinkKind.Biological),
            new(firstLegal, ParentChildLinkKind.LegalAdoptive),
            new(secondBiological, ParentChildLinkKind.Biological),
            new(thirdBiological, ParentChildLinkKind.Biological),
        ];
        CharacterWorldState totalLimit = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(145, 1, 1)),
            Seed(firstBiological, new CampaignDate(150, 1, 1)),
            Seed(firstLegal, new CampaignDate(151, 1, 1)),
            Seed(secondBiological, new CampaignDate(152, 1, 1)),
            Seed(thirdBiological, new CampaignDate(153, 1, 1)),
            Seed(Adopted, new CampaignDate(190, 1, 1), parents: fourTotal));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            totalLimit,
            AdoptiveParent,
            Adopted,
            fourTotal,
            "total-limit"));
    }

    [Fact]
    public void PrepareFamilyAction_EnforcesLegalAdoptiveChildCapWithoutRejectingSnapshot()
    {
        List<CharacterSeed> seeds =
        [
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(190, 1, 1)),
        ];
        for (int index = 0; index < 64; index++)
        {
            seeds.Add(Seed(
                new EntityId($"character:test/existing_adoptee_{index:D2}"),
                new CampaignDate(180, 1, 1),
                parents:
                [
                    new CharacterParentLink(
                        AdoptiveParent,
                        ParentChildLinkKind.LegalAdoptive),
                ]));
        }

        CharacterWorldState world = CreateWorld([.. seeds]);

        Assert.Equal(64, world.Profiles.Single(
            profile => profile.CharacterId == AdoptiveParent).ChildLinks.Count);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            world,
            AdoptiveParent,
            Adopted,
            [],
            "child-limit"));
    }

    [Fact]
    public void CharacterFamilyContracts_RoundTripDiscriminatorsAndStableIdGoldens()
    {
        EntityId commandId = new("command:test/family-golden");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        EntityId changeId = CharacterFamilyIds.DeriveParentageChangeId(eventId, Adopted);
        EstablishLegalAdoptiveParentAction action = new(
            AdoptiveParent,
            Adopted,
            []);
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            CharacterFamilySystem.AuthoritativeActorId,
            Date,
            new CharacterFamilyActionCommandPayload(action));
        CharacterParentageChange change = new(
            CharacterFamilyContractVersions.Change,
            changeId,
            Adopted,
            [],
            [new CharacterParentLink(AdoptiveParent, ParentChildLinkKind.LegalAdoptive)],
            Date,
            0,
            commandId);
        CharacterFamilyActionResolvedEventPayload payload = new(
            CharacterFamilySystem.AuthoritativeActorId,
            action,
            new LegalAdoptiveParentEstablishedOutcome(change));
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            payload);

        string commandJson = Serialize(command);
        string eventJson = Serialize(campaignEvent);
        CampaignCommand restoredCommand = JsonSerializer.Deserialize<CampaignCommand>(
            commandJson,
            SimulationJson.CreateOptions())!;
        CampaignEvent restoredEvent = JsonSerializer.Deserialize<CampaignEvent>(
            eventJson,
            SimulationJson.CreateOptions())!;

        Assert.Equal("character_family_action.v1", command.CommandType);
        Assert.Equal("character_family_action_resolved.v1", campaignEvent.EventType);
        Assert.Contains("character_family_action.v1", commandJson, StringComparison.Ordinal);
        Assert.Contains("establish_legal_adoptive_parent.v1", commandJson, StringComparison.Ordinal);
        Assert.Contains("character_family_action_resolved.v1", eventJson, StringComparison.Ordinal);
        Assert.Contains("legal_adoptive_parent_established.v1", eventJson, StringComparison.Ordinal);
        Assert.IsType<EstablishLegalAdoptiveParentAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(restoredCommand.Payload).Action);
        Assert.IsType<LegalAdoptiveParentEstablishedOutcome>(Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(restoredEvent.Payload).Outcome);
        Assert.Equal(
            "event:sha256/9f762d3b573fc9d31eb6e716bef7069f5af3f1247a0c84102462b337e3511902",
            eventId.Value);
        Assert.Equal(
            "character_parentage_change:sha256/5bcfe63bccdabdb69c13b5e90931af9ab450576689a5df128051796ebd989cd7",
            changeId.Value);
        Assert.Equal(
            [
                typeof(EstablishLegalAdoptiveParentAction),
                typeof(EstablishPrimaryGuardianshipAction),
            ],
            typeof(ICharacterFamilyAction).Assembly.GetTypes()
                .Where(type => typeof(ICharacterFamilyAction).IsAssignableFrom(type)
                    && type is { IsInterface: false, IsAbstract: false })
                .OrderBy(type => type.FullName)
                .ToArray());
        Assert.Equal(
            [
                typeof(LegalAdoptiveParentEstablishedOutcome),
                typeof(PrimaryGuardianshipEstablishedOutcome),
            ],
            typeof(ICharacterFamilyActionOutcome).Assembly.GetTypes()
                .Where(type => typeof(ICharacterFamilyActionOutcome).IsAssignableFrom(type)
                    && type is { IsInterface: false, IsAbstract: false })
                .OrderBy(type => type.FullName)
                .ToArray());
    }

    [Fact]
    public void CampaignFamilyAction_EnforcesAuthorityPhaseAndAppliesExactAffectedIds()
    {
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        Assert.False(simulation.Submit(FamilyCommand(
            simulation,
            new EntityId("command:test/family-unauthorized"),
            AdoptiveParent,
            Adopted,
            [],
            issuingActor: AdoptiveParent)).IsValid);
        Assert.False(simulation.Submit(FamilyCommand(
            simulation,
            new EntityId("command:test/family-wrong-phase"),
            AdoptiveParent,
            Adopted,
            [],
            phase: ResolutionPhase.Systems)).IsValid);

        CampaignCommand command = FamilyCommand(
            simulation,
            new EntityId("command:test/family-success"),
            AdoptiveParent,
            Adopted,
            []);
        Assert.True(simulation.Submit(command).IsValid);

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        CharacterParentageChange change = Assert.IsType<
            LegalAdoptiveParentEstablishedOutcome>(payload.Outcome).Change;

        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.Equal(
            new EntityId[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                change.ChangeId,
                AdoptiveParent,
                Adopted,
            }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            new CharacterParentLink(AdoptiveParent, ParentChildLinkKind.LegalAdoptive),
            Assert.Single(Profile(simulation, Adopted).ParentLinks));
    }

    [Fact]
    public void CampaignFamilyAction_TamperedOutcomeRollsBackAtomically()
    {
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        EntityId commandId = new("command:test/family-tampered");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishLegalAdoptiveParentAction(
                    AdoptiveParent,
                    Adopted,
                    [])),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        LegalAdoptiveParentEstablishedOutcome outcome = Assert.IsType<
            LegalAdoptiveParentEstablishedOutcome>(plan.ResolvedPayload.Outcome);
        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                Change = outcome.Change with { CurrentParentLinks = [] },
            },
        };
        CampaignEvent tampered = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(tamperedPayload),
            tamperedPayload);
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(tampered));

        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(Profile(simulation, Adopted).ParentLinks);
    }

    [Fact]
    public void PendingFamilyAction_SaveLoadReplayIsDeterministic()
    {
        CampaignSimulation original = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        CampaignCommand command = FamilyCommand(
            original,
            new EntityId("command:test/family-pending-save"),
            AdoptiveParent,
            Adopted,
            []);
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-family-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "family-pending.save.gz");
            new SaveStore().SaveAtomic(
                path,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope loaded = new SaveStore().Load(path);
            CampaignSimulation replay = new(WorldState.Restore(loaded.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsType<CharacterFamilyActionCommandPayload>(
                Assert.Single(loaded.Snapshot.PendingCommands).Payload);
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameTurnMarriageAndAdoptionRaceFollowsCrossDomainEventOrder(bool adoptionFirst)
    {
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        (EntityId familyCommandId, EntityId marriageCommandId) = FindCrossDomainCommandIds(
            adoptionFirst);
        CampaignCommand family = FamilyCommand(
            simulation,
            familyCommandId,
            AdoptiveParent,
            Adopted,
            []);
        CampaignCommand marriage = CampaignCommand.Create(
            marriageCommandId,
            AdoptiveParent,
            Date,
            new CharacterMarriageActionCommandPayload(
                new ProposePoliticalMarriageAction(
                    Adopted,
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    StrictPracticeId)));
        Assert.True(simulation.Submit(marriage).IsValid);
        Assert.True(simulation.Submit(family).IsValid);

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.Equal(2, events.Count);
        Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        if (adoptionFirst)
        {
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
            Assert.Single(Profile(simulation, Adopted).ParentLinks);
            Assert.Empty(simulation.World.CharacterMarriages.Proposals);
        }
        else
        {
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(events[0].Payload);
            Assert.Empty(Profile(simulation, Adopted).ParentLinks);
            Assert.Single(simulation.World.CharacterMarriages.Proposals);
        }
    }

    [Fact]
    public void SameTurnDoubleAdoptionRaceUsesEventIdOrderForEitherProspectiveParent()
    {
        EntityId secondParent = new("character:test/second_adoptive_parent");
        (EntityId earlier, EntityId later) = OrderedFamilyCommandIds();
        for (int scenario = 0; scenario < 2; scenario++)
        {
            CampaignSimulation simulation = CreateCampaign(
                marriages: null,
                Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
                Seed(secondParent, new CampaignDate(151, 1, 1)),
                Seed(Adopted, new CampaignDate(170, 1, 1)));
            EntityId earlierParent = scenario == 0 ? AdoptiveParent : secondParent;
            EntityId laterParent = scenario == 0 ? secondParent : AdoptiveParent;
            Assert.True(simulation.Submit(FamilyCommand(
                simulation,
                later,
                laterParent,
                Adopted,
                [])).IsValid);
            Assert.True(simulation.Submit(FamilyCommand(
                simulation,
                earlier,
                earlierParent,
                Adopted,
                [])).IsValid);

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
            Assert.Equal(
                earlierParent,
                Assert.Single(Profile(simulation, Adopted).ParentLinks).ParentCharacterId);
        }
    }

    [Fact]
    public void SubmittedFamilyActionOwnsExpectedLinksAndExternalMutationCannotChangeRaceOrChecksum()
    {
        EntityId secondParent = new("character:test/second_adoptive_parent");
        (EntityId earlier, EntityId later) = OrderedFamilyCommandIds();
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(secondParent, new CampaignDate(151, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        List<CharacterParentLink> earlierExpected = [];
        List<CharacterParentLink> laterExpected = [];
        CampaignCommand earlierCommand = FamilyCommand(
            simulation,
            earlier,
            AdoptiveParent,
            Adopted,
            earlierExpected);
        CampaignCommand laterCommand = FamilyCommand(
            simulation,
            later,
            secondParent,
            Adopted,
            laterExpected);
        Assert.True(simulation.Submit(laterCommand).IsValid);
        Assert.True(simulation.Submit(earlierCommand).IsValid);
        SimulationChecksum beforeMutation = SimulationChecksum.Compute(
            simulation.World.CaptureSnapshot());

        earlierExpected.Add(new CharacterParentLink(
            secondParent,
            ParentChildLinkKind.LegalAdoptive));
        laterExpected.Add(new CharacterParentLink(
            AdoptiveParent,
            ParentChildLinkKind.LegalAdoptive));

        Assert.Empty(Assert.IsType<EstablishLegalAdoptiveParentAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(earlierCommand.Payload).Action)
            .ExpectedCurrentParentLinks);
        Assert.Empty(Assert.IsType<EstablishLegalAdoptiveParentAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(laterCommand.Payload).Action)
            .ExpectedCurrentParentLinks);
        Assert.Equal(
            beforeMutation,
            SimulationChecksum.Compute(simulation.World.CaptureSnapshot()));

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
        Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        Assert.Equal(
            AdoptiveParent,
            Assert.Single(Profile(simulation, Adopted).ParentLinks).ParentCharacterId);
    }

    [Fact]
    public void PreparedFamilyEventReplansAndRejectsInterveningParentageWithoutPartialMutation()
    {
        EntityId secondParent = new("character:test/second_adoptive_parent");
        (EntityId earlier, EntityId later) = OrderedFamilyCommandIds();
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(secondParent, new CampaignDate(151, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        CharacterFamilyAggregatePlan stalePlan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishLegalAdoptiveParentAction(secondParent, Adopted, [])),
            Date,
            simulation.World.Calendar.TurnIndex,
            later,
            CharacterFamilyIds.DeriveActionEventId(Date, later));
        CharacterFamilyAggregatePlan earlierPlan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishLegalAdoptiveParentAction(AdoptiveParent, Adopted, [])),
            Date,
            simulation.World.Calendar.TurnIndex,
            earlier,
            CharacterFamilyIds.DeriveActionEventId(Date, earlier));
        CampaignEvent earlierEvent = FamilyEvent(earlier, earlierPlan.ResolvedPayload);
        CampaignEvent staleEvent = FamilyEvent(later, stalePlan.ResolvedPayload);

        simulation.World.Apply(earlierEvent);
        string afterEarlier = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(staleEvent));
        Assert.Equal(afterEarlier, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Equal(
            AdoptiveParent,
            Assert.Single(Profile(simulation, Adopted).ParentLinks).ParentCharacterId);
    }

    [Theory]
    [InlineData(MarriageRecordKind.ActiveProposal)]
    [InlineData(MarriageRecordKind.TerminalProposal)]
    [InlineData(MarriageRecordKind.ActiveBetrothal)]
    [InlineData(MarriageRecordKind.TerminalBetrothal)]
    [InlineData(MarriageRecordKind.ActiveUnion)]
    [InlineData(MarriageRecordKind.TerminalUnion)]
    [InlineData(MarriageRecordKind.Invitation)]
    [InlineData(MarriageRecordKind.ActiveRoute)]
    [InlineData(MarriageRecordKind.TerminalRoute)]
    public void MarriagePreflightRejectsDirectLineKinshipAcrossEveryRetainedRecord(
        MarriageRecordKind kind)
    {
        CampaignSimulation simulation = CreateCampaign(
            MarriageSnapshot(
                kind,
                MarriageProhibitedKinship.DirectLine,
                AdoptiveParent,
                Adopted),
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        EntityId commandId = new(
            $"command:test/preflight-{kind.ToString().ToLowerInvariant()}");
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishLegalAdoptiveParentAction(
                        AdoptiveParent,
                        Adopted,
                        [])),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                CharacterFamilyIds.DeriveActionEventId(Date, commandId)));

        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Theory]
    [InlineData(MarriageRecordKind.ActiveProposal)]
    [InlineData(MarriageRecordKind.TerminalProposal)]
    [InlineData(MarriageRecordKind.ActiveBetrothal)]
    [InlineData(MarriageRecordKind.TerminalBetrothal)]
    [InlineData(MarriageRecordKind.ActiveUnion)]
    [InlineData(MarriageRecordKind.TerminalUnion)]
    [InlineData(MarriageRecordKind.Invitation)]
    [InlineData(MarriageRecordKind.ActiveRoute)]
    [InlineData(MarriageRecordKind.TerminalRoute)]
    public void MarriagePreflightRejectsSiblingKinshipAcrossEveryRetainedRecord(
        MarriageRecordKind kind)
    {
        CharacterParentLink existingChildLink = new(
            AdoptiveParent,
            ParentChildLinkKind.Biological);
        CampaignSimulation simulation = CreateCampaign(
            MarriageSnapshot(
                kind,
                MarriageProhibitedKinship.Siblings,
                Adopted,
                Sibling),
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)),
            Seed(Sibling, new CampaignDate(171, 1, 1), parents: [existingChildLink]));
        EntityId commandId = new(
            $"command:test/sibling-matrix-{kind.ToString().ToLowerInvariant()}");
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishLegalAdoptiveParentAction(
                        AdoptiveParent,
                        Adopted,
                        [])),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                CharacterFamilyIds.DeriveActionEventId(Date, commandId)));

        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Theory]
    [InlineData(MarriageProhibitedKinship.Siblings, false)]
    [InlineData(MarriageProhibitedKinship.DirectLine, true)]
    [InlineData(MarriageProhibitedKinship.None, true)]
    public void MarriagePreflightAppliesOnlyConfiguredSiblingRule(
        MarriageProhibitedKinship flags,
        bool accepted)
    {
        CharacterParentLink siblingParent = new(
            AdoptiveParent,
            ParentChildLinkKind.Biological);
        CampaignSimulation simulation = CreateCampaign(
            MarriageSnapshot(
                MarriageRecordKind.ActiveProposal,
                flags,
                Adopted,
                Sibling),
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)),
            Seed(Sibling, new CampaignDate(171, 1, 1), parents: [siblingParent]));
        EntityId commandId = new($"command:test/sibling-preflight-{(int)flags}");

        Action prepare = () => simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishLegalAdoptiveParentAction(
                    AdoptiveParent,
                    Adopted,
                    [])),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));

        if (accepted)
        {
            prepare();
        }
        else
        {
            Assert.Throws<SimulationValidationException>(prepare);
        }
    }

    [Fact]
    public void PermissivePracticeAllowsAdoptionWithRetainedUnion()
    {
        CampaignSimulation simulation = CreateCampaign(
            MarriageSnapshot(
                MarriageRecordKind.ActiveUnion,
                MarriageProhibitedKinship.None,
                AdoptiveParent,
                Adopted,
                PermissivePracticeId),
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        CampaignCommand command = FamilyCommand(
            simulation,
            new EntityId("command:test/permissive-union-adoption"),
            AdoptiveParent,
            Adopted,
            []);

        Assert.True(simulation.Submit(command).IsValid);
        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        Assert.Single(Profile(simulation, Adopted).ParentLinks);
        Assert.Single(simulation.World.CharacterMarriages.Unions);
    }

    [Fact]
    public void PrepareFamilyAction_RejectsReverseParentCycleAndIneligibleAdopters()
    {
        CharacterParentLink reverse = new(
            Adopted,
            ParentChildLinkKind.Biological);
        CharacterWorldState reverseParent = CreateWorld(
            Seed(Adopted, new CampaignDate(150, 1, 1)),
            Seed(AdoptiveParent, new CampaignDate(170, 1, 1), parents: [reverse]));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            reverseParent,
            AdoptiveParent,
            Adopted,
            [],
            "reverse-cycle"));

        CharacterConditionState[] invalidConditions =
        [
            new(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            CharacterConditionState.Default with { IsIncapacitated = true },
            CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = BiologicalParent,
            },
        ];
        for (int index = 0; index < invalidConditions.Length; index++)
        {
            CharacterWorldState world = CreateWorld(
                Seed(AdoptiveParent, new CampaignDate(150, 1, 1), invalidConditions[index]),
                Seed(BiologicalParent, new CampaignDate(140, 1, 1)),
                Seed(Adopted, new CampaignDate(170, 1, 1)));
            Assert.Throws<SimulationValidationException>(() => Prepare(
                world,
                AdoptiveParent,
                Adopted,
                [],
                $"invalid-adopter-{index}"));
        }
    }

    [Fact]
    public void PrepareFamilyAction_RejectsDuplicateExpectedLinksAndGrandfathersOldOverCapState()
    {
        CharacterParentLink biological = new(
            BiologicalParent,
            ParentChildLinkKind.Biological);
        CharacterWorldState duplicateExpected = CreateWorld(
            Seed(AdoptiveParent, new CampaignDate(140, 1, 1)),
            Seed(BiologicalParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1), parents: [biological]));
        Assert.Throws<SimulationValidationException>(() => Prepare(
            duplicateExpected,
            AdoptiveParent,
            Adopted,
            [biological, biological],
            "duplicate-expected"));

        CharacterSeed[] oldParents = Enumerable.Range(0, 5)
            .Select(index => Seed(
                new EntityId($"character:test/old_parent_{index}"),
                new CampaignDate(140 + index, 1, 1)))
            .ToArray();
        CharacterParentLink[] oldLinks = oldParents
            .Select((parent, index) => new CharacterParentLink(
                parent.Id,
                index < 3
                    ? ParentChildLinkKind.LegalAdoptive
                    : ParentChildLinkKind.Biological))
            .ToArray();
        CharacterWorldState grandfathered = CreateWorld(
            [
                Seed(AdoptiveParent, new CampaignDate(139, 1, 1)),
                .. oldParents,
                Seed(Adopted, new CampaignDate(170, 1, 1), parents: oldLinks),
            ]);

        Assert.Equal(5, grandfathered.Profiles.Single(
            profile => profile.CharacterId == Adopted).ParentLinks.Count);
        Assert.Throws<SimulationValidationException>(() => Prepare(
            grandfathered,
            AdoptiveParent,
            Adopted,
            oldLinks,
            "grandfathered-over-cap"));
    }

    [Fact]
    public void AdoptedParentageChecksumIsOrderInvariantMutationSensitiveAndCurrentSaveRoundTripsDiagnostics()
    {
        CampaignSimulation simulation = CreateCampaign(
            marriages: null,
            Seed(AdoptiveParent, new CampaignDate(150, 1, 1)),
            Seed(Adopted, new CampaignDate(170, 1, 1)));
        Assert.True(simulation.Submit(FamilyCommand(
            simulation,
            new EntityId("command:test/family-current-save"),
            AdoptiveParent,
            Adopted,
            [])).IsValid);
        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        WorldSnapshot shuffled = snapshot with
        {
            Characters = snapshot.Characters with
            {
                CharacterDefinitions = snapshot.Characters.CharacterDefinitions.Reverse().ToArray(),
                CharacterStates = snapshot.Characters.CharacterStates.Reverse().ToArray(),
            },
        };
        CharacterState adoptedState = snapshot.Characters.CharacterStates.Single(
            state => state.CharacterId == Adopted);
        CharacterParentLink adoptedLink = Assert.Single(adoptedState.ParentLinks!);
        WorldSnapshot mutated = snapshot with
        {
            Characters = snapshot.Characters with
            {
                CharacterStates = snapshot.Characters.CharacterStates
                    .Select(state => state.CharacterId == Adopted
                        ? state with
                        {
                            ParentLinks =
                            [
                                adoptedLink with { Kind = ParentChildLinkKind.Biological },
                            ],
                        }
                        : state)
                    .ToArray(),
            },
        };

        Assert.Equal(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(shuffled));
        Assert.NotEqual(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(mutated));

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-family-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "family-current.save.gz");
            new SaveStore().SaveAtomic(path, SaveEnvelope.Create("test", [], simulation));
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Contains(
                loaded.DiagnosticCommands,
                command => command.Payload is CharacterFamilyActionCommandPayload
                {
                    Action: EstablishLegalAdoptiveParentAction,
                });
            Assert.Contains(
                loaded.DiagnosticEvents,
                campaignEvent => campaignEvent.Payload
                    is CharacterFamilyActionResolvedEventPayload
                {
                    Outcome: LegalAdoptiveParentEstablishedOutcome,
                });
            Assert.Equal(
                Serialize(snapshot.Characters),
                Serialize(loaded.Snapshot.Characters));
            Assert.Equal(SimulationChecksum.Compute(snapshot).Value, loaded.Checksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ThousandCharacterWorld_ResolvesBoundedAdoptionBatchAndRecordsRawEvidence()
    {
        List<CharacterSeed> seeds = Enumerable.Range(0, 1_000)
            .Select(index => Seed(
                new EntityId($"character:performance/{index:D4}"),
                index == 0
                    ? new CampaignDate(150, 1, 1)
                    : new CampaignDate(180, 1, 1)))
            .ToList();
        CampaignSimulation simulation = CreateCampaign(null, [.. seeds]);
        EntityId parent = seeds[0].Id;
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 1; index <= 64; index++)
        {
            Assert.True(simulation.Submit(FamilyCommand(
                simulation,
                new EntityId($"command:performance/adoption-{index:D2}"),
                parent,
                seeds[index].Id,
                [])).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch checksumWatch = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        checksumWatch.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(64, events.Count);
        Assert.All(events, campaignEvent =>
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload));
        Assert.Equal(64, Profile(simulation, parent).ChildLinks.Count(link =>
            link.Kind == ParentChildLinkKind.LegalAdoptive));
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"family_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static CharacterFamilyMutationPlan Prepare(
        CharacterWorldState world,
        EntityId adoptiveParent,
        EntityId adopted,
        IReadOnlyList<CharacterParentLink> expected,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return world.PrepareFamilyAction(
            new EstablishLegalAdoptiveParentAction(
                adoptiveParent,
                adopted,
                expected),
            Date,
            0,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));
    }

    private static CampaignSimulation CreateCampaign(
        CharacterMarriageWorldSnapshot? marriages,
        params CharacterSeed[] seeds) => new(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            CreateWorld(seeds).CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            marriages ?? new CharacterMarriageWorldSnapshot(
                CharacterMarriageContractVersions.Snapshot,
                [
                    Practice(
                        StrictPracticeId,
                        MarriageProhibitedKinship.DirectLine
                            | MarriageProhibitedKinship.Siblings),
                    Practice(PermissivePracticeId, MarriageProhibitedKinship.None),
                ],
                [],
                [],
                [],
                [],
                [],
                [])));

    private static CampaignCommand FamilyCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        EntityId adoptiveParent,
        EntityId adopted,
        IReadOnlyList<CharacterParentLink> expected,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands) => CampaignCommand.Create(
        commandId,
        issuingActor ?? CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new EstablishLegalAdoptiveParentAction(
                adoptiveParent,
                adopted,
                expected)),
        phase);

    private static CampaignEvent FamilyEvent(
        EntityId commandId,
        CharacterFamilyActionResolvedEventPayload payload)
    {
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        return new CampaignEvent(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            payload);
    }

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile;
    }

    private static (EntityId FamilyCommandId, EntityId MarriageCommandId)
        FindCrossDomainCommandIds(bool familyFirst)
    {
        string order = familyFirst ? "family-first" : "marriage-first";
        for (int index = 0; index < 10_000; index++)
        {
            EntityId family = new($"command:test/cross-family-{order}-{index:D4}");
            EntityId marriage = new($"command:test/cross-marriage-{order}-{index:D4}");
            int comparison = CharacterFamilyIds.DeriveActionEventId(Date, family).CompareTo(
                CharacterMarriageIds.DeriveActionEventId(Date, marriage));
            if (familyFirst ? comparison < 0 : comparison > 0)
            {
                return (family, marriage);
            }
        }

        throw new InvalidOperationException("Could not construct the requested event ordering.");
    }

    private static (EntityId Earlier, EntityId Later) OrderedFamilyCommandIds()
    {
        EntityId first = new("command:test/double-adoption-first");
        EntityId second = new("command:test/double-adoption-second");
        return CharacterFamilyIds.DeriveActionEventId(Date, first).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, second)) < 0
                ? (first, second)
                : (second, first);
    }

    private static CharacterMarriageWorldSnapshot MarriageSnapshot(
        MarriageRecordKind kind,
        MarriageProhibitedKinship flags,
        EntityId first,
        EntityId second,
        EntityId? practiceId = null)
    {
        EntityId selectedPracticeId = practiceId
            ?? (flags == MarriageProhibitedKinship.None
                ? PermissivePracticeId
                : StrictPracticeId);
        MarriagePracticeState practice = Practice(selectedPracticeId, flags);
        MarriageProposalState[] proposals = [];
        PoliticalBetrothalState[] betrothals = [];
        MarriageUnionState[] unions = [];
        RomanceInvitationState[] invitations = [];
        RomanceRouteState[] routes = [];
        switch (kind)
        {
            case MarriageRecordKind.ActiveProposal:
                proposals = [Proposal("active", first, second, selectedPracticeId)];
                break;
            case MarriageRecordKind.TerminalProposal:
                proposals =
                [
                    Proposal("terminal", first, second, selectedPracticeId) with
                    {
                        Status = MarriageProposalStatus.Refused,
                        ResolutionDate = Date.AddDays(-1),
                        ResolutionTurnIndex = 0,
                        ResolutionCommandId = new EntityId(
                            "command:test/family-terminal-proposal"),
                    },
                ];
                break;
            case MarriageRecordKind.ActiveBetrothal:
            case MarriageRecordKind.TerminalBetrothal:
                {
                    MarriageProposalState source = Proposal(
                        $"{kind}-source",
                        first,
                        second,
                        selectedPracticeId,
                        MarriageProposalKind.PoliticalBetrothal,
                        accepted: true);
                    proposals = [source];
                    betrothals =
                    [
                        Betrothal(
                        kind.ToString(),
                        source,
                        kind == MarriageRecordKind.ActiveBetrothal),
                ];
                    break;
                }
            case MarriageRecordKind.ActiveUnion:
            case MarriageRecordKind.TerminalUnion:
                {
                    MarriageProposalState source = Proposal(
                        $"{kind}-source",
                        first,
                        second,
                        selectedPracticeId,
                        accepted: true);
                    proposals = [source];
                    unions =
                    [
                        Union(
                        kind.ToString(),
                        source,
                        kind == MarriageRecordKind.ActiveUnion),
                ];
                    break;
                }
            case MarriageRecordKind.Invitation:
                {
                    EntityId commandId = new("command:test/family-invitation");
                    CampaignDate created = Date.AddDays(-2);
                    invitations =
                    [
                        new RomanceInvitationState(
                        CharacterMarriageContractVersions.RomanceInvitationState,
                        CharacterMarriageIds.DeriveRomanceInvitationId(created, commandId),
                        first,
                        second,
                        selectedPracticeId,
                        created,
                        0,
                        commandId),
                ];
                    break;
                }
            case MarriageRecordKind.ActiveRoute:
            case MarriageRecordKind.TerminalRoute:
                routes =
                [
                    Route(
                        kind.ToString(),
                        first,
                        second,
                        selectedPracticeId,
                        kind == MarriageRecordKind.ActiveRoute),
                ];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new CharacterMarriageWorldSnapshot(
            CharacterMarriageContractVersions.Snapshot,
            [practice],
            proposals,
            betrothals,
            unions,
            routes,
            [],
            invitations);
    }

    private static MarriagePracticeState Practice(
        EntityId practiceId,
        MarriageProhibitedKinship flags) => new(
        CharacterMarriageContractVersions.Practice,
        practiceId,
        18,
        18,
        8,
        64,
        64,
        true,
        true,
        flags);

    private static MarriageProposalState Proposal(
        string suffix,
        EntityId proposer,
        EntityId recipient,
        EntityId practiceId,
        MarriageProposalKind kind = MarriageProposalKind.LegalUnion,
        bool accepted = false) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_proposal:family/{suffix.ToLowerInvariant()}"),
        kind,
        MarriageBasis.Political,
        MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind.PoliticalArrangement,
        proposer,
        recipient,
        null,
        practiceId,
        Date.AddDays(-2),
        0,
        new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-proposal"),
        accepted ? MarriageProposalStatus.Accepted : MarriageProposalStatus.Active,
        accepted ? Date.AddDays(-1) : null,
        accepted ? 0 : null,
        accepted
            ? new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-accept")
            : null);

    private static PoliticalBetrothalState Betrothal(
        string suffix,
        MarriageProposalState source,
        bool active) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"political_betrothal:family/{suffix.ToLowerInvariant()}"),
        Min(source.ProposerCharacterId, source.RecipientCharacterId),
        Max(source.ProposerCharacterId, source.RecipientCharacterId),
        source.ProposedForm,
        source.ConcubinagePrincipalCharacterId,
        source.PracticeId,
        source.ProposalId,
        source.ResolutionDate!.Value,
        source.ResolutionTurnIndex!.Value,
        active
            ? PoliticalBetrothalStatus.Active
            : PoliticalBetrothalStatus.Released,
        null,
        active ? null : Date,
        active ? null : 0,
        active
            ? null
            : new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-release"));

    private static MarriageUnionState Union(
        string suffix,
        MarriageProposalState source,
        bool active) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_union:family/{suffix.ToLowerInvariant()}"),
        Min(source.ProposerCharacterId, source.RecipientCharacterId),
        Max(source.ProposerCharacterId, source.RecipientCharacterId),
        source.ProposedForm,
        source.ConcubinagePrincipalCharacterId,
        source.Basis,
        source.ConsentKind,
        source.PracticeId,
        source.ProposalId,
        source.ResolutionDate!.Value,
        source.ResolutionTurnIndex!.Value,
        active ? MarriageUnionStatus.Active : MarriageUnionStatus.Ended,
        active ? null : Date,
        active ? null : 0,
        active
            ? null
            : new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-end"),
        active ? null : MarriageUnionEndReason.Annulled);

    private static RomanceRouteState Route(
        string suffix,
        EntityId first,
        EntityId second,
        EntityId practiceId,
        bool active) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"romance_route:family/{suffix.ToLowerInvariant()}"),
        Min(first, second),
        Max(first, second),
        practiceId,
        1,
        Date.AddDays(-2),
        0,
        new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-route"),
        active ? RomanceRouteStatus.Active : RomanceRouteStatus.Ended,
        active ? null : Date,
        active ? null : 0,
        active
            ? null
            : new EntityId($"command:test/family-{suffix.ToLowerInvariant()}-route-end"));

    private static EntityId Min(EntityId first, EntityId second) =>
        first.CompareTo(second) <= 0 ? first : second;

    private static EntityId Max(EntityId first, EntityId second) =>
        first.CompareTo(second) >= 0 ? first : second;

    private static CharacterWorldState CreateWorld(params CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .Select(seed => new CharacterDefinition(
                CharacterContractVersions.Definition,
                seed.Id,
                new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                seed.BirthDate,
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(
                    new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                    null),
                CharacterContentOrigin.LegacyUnknown(seed.Id),
                null,
                null,
                []))
            .OrderBy(definition => definition.Id)
            .ToArray();
        CharacterState[] states = seeds
            .Select(seed =>
            {
                CharacterParentLink[] parents = seed.Parents
                    .OrderBy(link => link.ParentCharacterId)
                    .ThenBy(link => link.Kind)
                    .ToArray();
                return new CharacterState(
                    CharacterContractVersions.State,
                    seed.Id,
                    parents.Select(link => link.ParentCharacterId).ToArray(),
                    parents,
                    seed.Condition);
            })
            .OrderBy(state => state.CharacterId)
            .ToArray();
        EntityId familyId = new("family:test/all");
        EntityId householdId = new("household:test/all");
        EntityId[] memberIds = definitions.Select(definition => definition.Id).ToArray();
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [
                new FamilyDefinition(
                    CharacterContractVersions.Definition,
                    familyId,
                    new EntityId("loc:family/test_all")),
            ],
            [
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    householdId,
                    new EntityId("loc:household/test_all")),
            ],
            states,
            [
                new FamilyState(
                    CharacterContractVersions.State,
                    familyId,
                    memberIds),
            ],
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    householdId,
                    seeds[0].Id,
                    memberIds),
            ]);
        return new CharacterWorldState(snapshot, Date);
    }

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState? condition = null,
        IReadOnlyList<CharacterParentLink>? parents = null) => new(
            id,
            birthDate,
            condition ?? CharacterConditionState.Default,
            parents ?? []);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition,
        IReadOnlyList<CharacterParentLink> Parents);

    public enum MarriageRecordKind
    {
        ActiveProposal,
        TerminalProposal,
        ActiveBetrothal,
        TerminalBetrothal,
        ActiveUnion,
        TerminalUnion,
        Invitation,
        ActiveRoute,
        TerminalRoute,
    }
}
