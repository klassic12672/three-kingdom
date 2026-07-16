using System.Reflection;
using System.Text.Json;
using Simulation.Core;

namespace Game.Application.Tests;

public sealed class CharacterObserverQueryTests
{
    private static readonly CampaignDate Date = new(200, 1, 1);
    private static readonly EntityId Subject = new("character:test/observer-subject");
    private static readonly EntityId Custodian = new("character:test/custodian");
    private static readonly EntityId LegalPartner = new("character:test/legal-partner");
    private static readonly EntityId RomancePartner = new("character:test/romance-partner");
    private static readonly EntityId Stranger = new("character:test/stranger");

    [Fact]
    public void ProfileWhitelistAppliesSelfCustodianParticipantAndPublicVisibility()
    {
        TestWorldQuery world = CreateObserverWorld();
        CharacterProfileQuery query = new(world);

        CharacterProfile self = AssertProfile(query, Subject, Subject);
        Assert.Equal(CharacterObserverContractVersions.CharacterProfile, self.ContractVersion);
        Assert.Equal(CharacterHealthStatus.Critical, self.KnownConditionDetails!.HealthStatus);
        Assert.True(self.KnownConditionDetails.IsIncapacitated);
        Assert.NotNull(self.PrivateDetails);
        Assert.Equal(64, self.PrivateDetails.AbilityIds.Count);
        Assert.Equal(64, self.PrivateDetails.AcquiredEducationAbilityIds.Count);
        Assert.True(self.PrivateDetails.ListsTruncated);
        Assert.Equal(64, self.ParentLinks.Count);
        Assert.Equal(70, self.TotalParentLinkCount);
        Assert.Equal(64, self.ReputationIds.Count);
        Assert.Single(self.LegalUnions);
        Assert.Single(self.PoliticalBetrothals);
        Assert.Single(self.RomanceRoutes);

        CharacterProfile custodian = AssertProfile(query, Custodian, Subject);
        Assert.Null(custodian.KnownConditionDetails!.HealthStatus);
        Assert.Null(custodian.KnownConditionDetails.IsIncapacitated);
        Assert.Equal(CharacterCustodyStatus.Captive, custodian.KnownConditionDetails.CustodyStatus);
        Assert.Equal(Custodian, custodian.KnownConditionDetails.CustodianId);
        Assert.Null(custodian.PrivateDetails);
        Assert.Empty(custodian.RomanceRoutes);

        CharacterProfile romancePartner = AssertProfile(query, RomancePartner, Subject);
        Assert.Null(romancePartner.KnownConditionDetails);
        Assert.Null(romancePartner.PrivateDetails);
        Assert.Single(romancePartner.RomanceRoutes);

        CharacterProfile stranger = AssertProfile(query, Stranger, Subject);
        Assert.Null(stranger.KnownConditionDetails);
        Assert.Null(stranger.PrivateDetails);
        Assert.Single(stranger.LegalUnions);
        Assert.Single(stranger.PoliticalBetrothals);
        Assert.Empty(stranger.RomanceRoutes);
        Assert.DoesNotContain(
            typeof(MarriageConsentKind).Name,
            JsonSerializer.Serialize(stranger));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<EntityId>)self.ReputationIds).Add(new("reputation:test/leak")));
        Assert.False(query.TryGet(
            new EntityId("character:test/missing-observer"),
            Subject,
            out _));
    }

    [Fact]
    public void ObserverContractsExposeOnlyFrozenWhitelist()
    {
        AssertProperties<CharacterProfile>(
            nameof(CharacterProfile.ContractVersion),
            nameof(CharacterProfile.CharacterId),
            nameof(CharacterProfile.StructuredName),
            nameof(CharacterProfile.Age),
            nameof(CharacterProfile.CultureId),
            nameof(CharacterProfile.OriginLocationId),
            nameof(CharacterProfile.VitalStatus),
            nameof(CharacterProfile.KnownConditionDetails),
            nameof(CharacterProfile.FamilyId),
            nameof(CharacterProfile.HouseholdId),
            nameof(CharacterProfile.ParentLinks),
            nameof(CharacterProfile.TotalParentLinkCount),
            nameof(CharacterProfile.ChildLinks),
            nameof(CharacterProfile.TotalChildLinkCount),
            nameof(CharacterProfile.ReputationIds),
            nameof(CharacterProfile.PrivateDetails),
            nameof(CharacterProfile.LegalUnions),
            nameof(CharacterProfile.PoliticalBetrothals),
            nameof(CharacterProfile.RomanceRoutes));
        AssertProperties<KnownCharacterConditionDetails>(
            nameof(KnownCharacterConditionDetails.HealthStatus),
            nameof(KnownCharacterConditionDetails.IsIncapacitated),
            nameof(KnownCharacterConditionDetails.CustodyStatus),
            nameof(KnownCharacterConditionDetails.CustodianId));
        AssertProperties<CharacterPrivateDetails>(
            nameof(CharacterPrivateDetails.AbilityIds),
            nameof(CharacterPrivateDetails.AptitudeIds),
            nameof(CharacterPrivateDetails.TraitIds),
            nameof(CharacterPrivateDetails.FlawIds),
            nameof(CharacterPrivateDetails.AmbitionIds),
            nameof(CharacterPrivateDetails.AcquiredEducationAbilityIds),
            nameof(CharacterPrivateDetails.ListsTruncated));
        AssertProperties<HouseholdView>(
            nameof(HouseholdView.ContractVersion),
            nameof(HouseholdView.HouseholdId),
            nameof(HouseholdView.NameKey),
            nameof(HouseholdView.HeadCharacterId),
            nameof(HouseholdView.MemberIds),
            nameof(HouseholdView.TotalMemberCount),
            nameof(HouseholdView.MembersTruncated));
        AssertProperties<SuccessionView>(
            nameof(SuccessionView.ContractVersion),
            nameof(SuccessionView.SubjectCharacterId),
            nameof(SuccessionView.CurrentDesignation),
            nameof(SuccessionView.ActiveClaims),
            nameof(SuccessionView.ActiveSupports),
            nameof(SuccessionView.CompletedResolution),
            nameof(SuccessionView.Regency));

        string[] forbidden =
        [
            "BirthDate",
            "ContentOrigin",
            "SourceId",
            "Teacher",
            "Guardianship",
            "Wealth",
            "Estate",
            "Career",
            "Pregnancy",
            "Inheritance",
            "Hash",
            "Continuity",
        ];
        Assert.DoesNotContain(
            typeof(CharacterProfile).GetProperties()
                .Concat(typeof(HouseholdView).GetProperties())
                .Concat(typeof(SuccessionView).GetProperties()),
            property => forbidden.Any(fragment =>
                property.Name.Contains(fragment, StringComparison.Ordinal)));
    }

    [Fact]
    public void HouseholdIsPublicButRequiresAValidObserverAndIsBounded()
    {
        TestWorldQuery world = CreateObserverWorld();
        HouseholdViewQuery query = new(world);

        Assert.True(query.TryGet(Stranger, new("household:test/observer"), out HouseholdView? view));
        Assert.Equal(256, view.MemberIds.Count);
        Assert.Equal(300, view.TotalMemberCount);
        Assert.True(view.MembersTruncated);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EntityId>)view.MemberIds).Add(Subject));
        Assert.False(query.TryGet(
            new EntityId("character:test/missing-observer"),
            view.HouseholdId,
            out _));
    }

    [Fact]
    public void SuccessionRoleVisibilityAndPublicResolutionStaySeparated()
    {
        TestWorldQuery world = CreateSuccessionWorld();
        SuccessionViewQuery query = new(world);
        EntityId heir = new("character:test/heir");
        EntityId claimant = new("character:test/claimant");
        EntityId supporter = new("character:test/supporter");

        SuccessionView subject = AssertSuccession(query, Subject, Subject);
        Assert.NotNull(subject.CurrentDesignation);
        Assert.Single(subject.ActiveClaims);
        Assert.Single(subject.ActiveSupports);

        SuccessionView heirView = AssertSuccession(query, heir, Subject);
        Assert.NotNull(heirView.CurrentDesignation);
        Assert.Empty(heirView.ActiveClaims);
        Assert.Single(heirView.ActiveSupports);

        SuccessionView claimantView = AssertSuccession(query, claimant, Subject);
        Assert.Null(claimantView.CurrentDesignation);
        Assert.Single(claimantView.ActiveClaims);
        Assert.Empty(claimantView.ActiveSupports);

        SuccessionView supporterView = AssertSuccession(query, supporter, Subject);
        Assert.Null(supporterView.CurrentDesignation);
        Assert.Empty(supporterView.ActiveClaims);
        Assert.Single(supporterView.ActiveSupports);

        SuccessionView publicView = AssertSuccession(query, Stranger, Subject);
        Assert.Null(publicView.CurrentDesignation);
        Assert.Empty(publicView.ActiveClaims);
        Assert.Empty(publicView.ActiveSupports);
        Assert.Equal(heir, publicView.CompletedResolution!.SelectedSuccessorCharacterId);
        Assert.Equal(heir, publicView.Regency!.SuccessorCharacterId);
        Assert.Equal(new EntityId("character:test/regent"), publicView.Regency.RegentCharacterId);
    }

    [Fact]
    public void ControlledFacadeReResolvesContinuityAndDisablesObserversWhenInactive()
    {
        TestWorldQuery world = CreateObserverWorld();
        world.CharacterData.Add(Profile(new EntityId("character:test/successor")));
        ControlledCharacterObserverFacade facade = new(world, Subject);

        Assert.True(facade.TryGetControlledCharacterId(out EntityId initial));
        Assert.Equal(Subject, initial);
        Assert.True(facade.TryGetHousehold(new("household:test/observer"), out _));

        EntityId successor = new("character:test/successor");
        world.SuccessionData.CampaignContinuity = Continuity(
            PlayerCampaignContinuityStatus.Active,
            successor,
            "active");
        Assert.True(facade.TryGetControlledCharacterId(out EntityId transferred));
        Assert.Equal(successor, transferred);

        world.SuccessionData.CampaignContinuity = Continuity(
            PlayerCampaignContinuityStatus.Ended,
            null,
            "ended");
        Assert.False(facade.TryGetControlledCharacterId(out _));
        Assert.False(facade.TryGetCharacter(Subject, out _));
        Assert.False(facade.TryGetHousehold(new("household:test/observer"), out _));
        Assert.False(facade.TryGetSuccession(Subject, out _));
        Assert.False(facade.TryGetRelationship(Subject, out _));
    }

    [Fact]
    public void ProfileQueryIsStableAcrossSyntheticTierChanges()
    {
        SyntheticEntitySnapshot synthetic = new(
            new EntityId("synthetic:test/observer-tier"),
            SimulationTier.Full,
            1,
            2,
            3,
            []);
        CampaignSimulation simulation = CreateSimulation([Subject, Stranger], synthetic);
        CharacterProfileQuery query = new(simulation.World);
        CharacterProfile before = AssertProfile(query, Stranger, Subject);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/observer-tier"),
            synthetic.Id,
            Date,
            new ChangeSimulationTierCommandPayload(
                synthetic.Id,
                SimulationTier.Aggregate));

        Assert.True(simulation.Submit(command).IsValid);
        _ = simulation.ResolveTurn();
        CharacterProfile after = AssertProfile(query, Stranger, Subject);
        Assert.Equal(
            JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(after));
    }

    [Fact]
    public void ProfileQueryIsEquivalentAcrossSaveEnvelopeWorldRestore()
    {
        SyntheticEntitySnapshot synthetic = new(
            new EntityId("synthetic:test/observer-restore"),
            SimulationTier.Reduced,
            4,
            5,
            6,
            []);
        CampaignSimulation simulation = CreateSimulation([Subject, Stranger], synthetic);
        CharacterProfile before = AssertProfile(
            new CharacterProfileQuery(simulation.World),
            Stranger,
            Subject);
        SaveEnvelope envelope = SaveEnvelope.Create(
            "test",
            [],
            simulation,
            DateTimeOffset.UnixEpoch);
        CampaignSimulation restored = new(WorldState.Restore(envelope.Snapshot));
        CharacterProfile after = AssertProfile(
            new CharacterProfileQuery(restored.World),
            Stranger,
            Subject);

        Assert.Equal(
            JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(after));
    }

    [Fact]
    public void ControlledFacadeTracksRealSuccessionAcrossReplayRestoreAndContinuation()
    {
        EntityId successor = new("character:test/g07-successor");
        EntityId householdId = new("household:test/g07");
        CampaignSimulation original = CreateSuccessionObserverSimulation(
            successor,
            householdId);
        CampaignCommand initialMemory = CampaignCommand.Create(
            new EntityId("command:test/g07-initial-memory"),
            successor,
            original.World.Calendar.Date,
            new RelationshipActionCommandPayload(
                Subject,
                new RelationshipImpact(0, 2, 0, 0, 0, 0, 0, 0, 0),
                new EntityId("memory_meaning:test/g07-initial"),
                20,
                MemoryPublicity.Participants,
                0,
                []));
        Assert.True(original.Submit(initialMemory).IsValid);
        _ = Assert.Single(original.ResolveTurn());

        CampaignDate deathDate = original.World.Calendar.Date.AddDays(2);
        SuccessionResolutionRule rule = ResolutionRule();
        EntityId resolutionStateId =
            original.World.GetCharacterSuccessionResolutionStateId(
                Subject,
                rule,
                deathDate,
                original.World.Calendar.TurnIndex,
                householdId,
                successor);
        CampaignCommand death = CampaignCommand.Create(
            new EntityId("command:test/g07-succession-death"),
            CharacterConditionSystem.AuthoritativeActorId,
            deathDate,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterSuccessionDeathAction(
                    Subject,
                    CharacterCondition(original, Subject),
                    rule,
                    resolutionStateId,
                    householdId,
                    successor,
                    null)));
        Assert.True(original.Submit(death).IsValid);
        SaveEnvelope pending = SaveEnvelope.Create(
            "test",
            [],
            original,
            DateTimeOffset.UnixEpoch);
        Assert.Single(pending.Snapshot.PendingCommands);
        CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));
        ControlledCharacterObserverFacade originalFacade = new(original.World);
        ControlledCharacterObserverFacade replayFacade = new(replay.World);
        AssertControlled(originalFacade, Subject);
        AssertControlled(replayFacade, Subject);

        IReadOnlyList<CampaignEvent> originalEvents = original.ResolveTurn();
        IReadOnlyList<CampaignEvent> replayEvents = replay.ResolveTurn();
        Assert.Equal(Serialize(originalEvents), Serialize(replayEvents));
        AssertControlled(originalFacade, successor);
        AssertControlled(replayFacade, successor);
        FacadeViews originalViews = CaptureViews(
            originalFacade,
            Subject,
            householdId,
            successor);
        FacadeViews replayViews = CaptureViews(
            replayFacade,
            Subject,
            householdId,
            successor);
        Assert.Equal(Serialize(originalViews), Serialize(replayViews));
        Assert.Equal(CharacterVitalStatus.Dead, originalViews.Character.VitalStatus);
        Assert.Equal(successor, originalViews.Household.HeadCharacterId);
        Assert.Equal(
            successor,
            originalViews.Succession.CompletedResolution!
                .SelectedSuccessorCharacterId);
        Assert.NotNull(
            Assert.Single(originalViews.Relationship.DetailedRelationships)
                .ExactDimensions);

        SaveEnvelope resolved = SaveEnvelope.Create(
            "test",
            [],
            original,
            DateTimeOffset.UnixEpoch);
        CampaignSimulation restored = new(WorldState.Restore(resolved.Snapshot));
        ControlledCharacterObserverFacade restoredFacade = new(restored.World);
        AssertControlled(restoredFacade, successor);
        Assert.Equal(
            Serialize(originalViews),
            Serialize(CaptureViews(
                restoredFacade,
                Subject,
                householdId,
                successor)));

        CampaignDate continuationDate = original.World.Calendar.Date;
        Assert.True(continuationDate.CompareTo(deathDate) > 0);
        CampaignCommand continuation = CampaignCommand.Create(
            new EntityId("command:test/g07-later-memory"),
            successor,
            continuationDate,
            new RelationshipActionCommandPayload(
                Stranger,
                new RelationshipImpact(1, 0, 0, 0, 0, 0, 0, 0, 0),
                new EntityId("memory_meaning:test/g07-later"),
                10,
                MemoryPublicity.Public,
                0,
                []));
        Assert.True(original.Submit(continuation).IsValid);
        Assert.True(restored.Submit(continuation).IsValid);
        Assert.Equal(
            Serialize(original.ResolveTurn()),
            Serialize(restored.ResolveTurn()));
        AssertControlled(originalFacade, successor);
        AssertControlled(restoredFacade, successor);
        FacadeViews continuedOriginal = CaptureViews(
            originalFacade,
            Subject,
            householdId,
            successor);
        FacadeViews continuedRestored = CaptureViews(
            restoredFacade,
            Subject,
            householdId,
            successor);
        Assert.Equal(
            Serialize(continuedOriginal),
            Serialize(continuedRestored));
        Assert.Equal(2, continuedOriginal.Relationship.DetailedRelationships.Count);
    }

    private static TestWorldQuery CreateObserverWorld()
    {
        TestWorldQuery world = new();
        CharacterConditionState captiveCritical = CharacterConditionState.Default with
        {
            HealthStatus = CharacterHealthStatus.Critical,
            IsIncapacitated = true,
            CustodyStatus = CharacterCustodyStatus.Captive,
            CustodianId = Custodian,
        };
        EntityId[] abilities = Ids("ability", 70);
        EntityId[] reputations = Ids("reputation", 70);
        CharacterParentLink[] parents = Enumerable.Range(0, 70)
            .Select(index => new CharacterParentLink(
                new EntityId($"character:test/parent-{index:D2}"),
                ParentChildLinkKind.Biological))
            .Reverse()
            .ToArray();
        CharacterEducationAttainment[] attainments = Enumerable.Range(0, 70)
            .Select(index => new CharacterEducationAttainment(
                CharacterEducationContractVersions.Attainment,
                new EntityId($"character_education_attainment:test/{index:D2}"),
                Subject,
                new EntityId("character:test/teacher"),
                new EntityId("character_guardianship:test/source"),
                new EntityId($"ability:test/education-{index:D2}"),
                Date,
                0,
                new EntityId($"command:test/education-{index:D2}"),
                new EntityId($"event:test/education-{index:D2}")))
            .ToArray();
        world.CharacterData.Add(Profile(
            Subject,
            captiveCritical,
            abilities,
            reputations,
            parents,
            attainments,
            new EntityId("household:test/observer")));
        foreach (EntityId id in new[] { Custodian, LegalPartner, RomancePartner, Stranger })
        {
            world.CharacterData.Add(Profile(id));
        }

        world.CharacterData.Add(new AuthoritativeHouseholdView(
            CharacterContractVersions.AuthoritativeQuery,
            new EntityId("household:test/observer"),
            new EntityId("loc:test/observer-household"),
            Subject,
            Enumerable.Range(0, 300)
                .Select(index => new EntityId($"character:test/member-{index:D3}"))
                .Reverse()
                .ToArray()));
        EntityId practice = new("marriage_practice:test/observer");
        world.MarriageData.Unions =
        [
            new(
                CharacterMarriageContractVersions.State,
                new EntityId("marriage_union:test/coerced"),
                Subject,
                LegalPartner,
                MarriageUnionForm.PrincipalSpouse,
                null,
                MarriageBasis.Political,
                MarriageConsentKind.Coerced,
                practice,
                new EntityId("marriage_proposal:test/coerced"),
                Date,
                0,
                MarriageUnionStatus.Active,
                null,
                null,
                null,
                null),
        ];
        world.MarriageData.Betrothals =
        [
            new(
                CharacterMarriageContractVersions.State,
                new EntityId("political_betrothal:test/public"),
                Subject,
                LegalPartner,
                MarriageUnionForm.PrincipalSpouse,
                null,
                practice,
                new EntityId("marriage_proposal:test/betrothal"),
                Date,
                0,
                PoliticalBetrothalStatus.Active,
                null,
                null,
                null,
                null),
        ];
        world.MarriageData.RomanceRoutes =
        [
            new(
                CharacterMarriageContractVersions.RomanceRouteState,
                new EntityId("romance_route:test/private"),
                Subject,
                RomancePartner,
                practice,
                3,
                Date,
                0,
                new EntityId("command:test/romance"),
                RomanceRouteStatus.Active,
                null,
                null,
                null),
        ];
        return world;
    }

    private static TestWorldQuery CreateSuccessionWorld()
    {
        TestWorldQuery world = new();
        EntityId heir = new("character:test/heir");
        EntityId claimant = new("character:test/claimant");
        EntityId supporter = new("character:test/supporter");
        EntityId regent = new("character:test/regent");
        foreach (EntityId id in new[] { Subject, Stranger, heir, claimant, supporter, regent })
        {
            world.CharacterData.Add(Profile(id));
        }

        world.SuccessionData.Designations =
        [
            new(
                CharacterSuccessionContractVersions.State,
                new EntityId("heir_designation:test/current"),
                Subject,
                heir,
                Date,
                0,
                new EntityId("command:test/designation"),
                new EntityId("event:test/designation"),
                HeirDesignationStatus.Active,
                null,
                null,
                null,
                null),
        ];
        world.SuccessionData.Claims =
        [
            new(
                CharacterSuccessionContractVersions.ClaimState,
                new EntityId("succession_claim:test/current"),
                Subject,
                claimant,
                SuccessionClaimOrigin.PersonalAssertion,
                Date,
                0,
                new EntityId("command:test/claim"),
                new EntityId("event:test/claim"),
                SuccessionClaimStatus.Active,
                null,
                null,
                null,
                null),
        ];
        world.SuccessionData.Supports =
        [
            new(
                CharacterSuccessionContractVersions.SupportState,
                new EntityId("succession_support:test/current"),
                Subject,
                supporter,
                heir,
                Date,
                0,
                new EntityId("command:test/support"),
                new EntityId("event:test/support"),
                SuccessionSupportStatus.Active,
                null,
                null,
                null,
                null),
        ];
        SuccessionResolutionCandidate selected = new(
            CharacterSuccessionContractVersions.ResolutionCandidate,
            heir,
            30,
            CharacterConditionState.Default,
            [],
            null,
            [],
            0,
            1);
        world.SuccessionData.Resolutions =
        [
            new(
                CharacterSuccessionContractVersions.Resolution,
                new EntityId("succession_resolution:test/public"),
                Subject,
                new EntityId("character_death:test/subject"),
                SuccessionResolutionStatus.Selected,
                selected,
                [],
                1,
                ResolutionRule(),
                new SuccessionInheritanceChange(
                    CharacterSuccessionContractVersions.Inheritance,
                    null,
                    []),
                new SuccessionRegencyHook(
                    CharacterSuccessionContractVersions.Regency,
                    heir,
                    SuccessionRegencyReason.Minor,
                    regent,
                    null,
                    null,
                    null),
                null,
                null,
                Date,
                0,
                new EntityId("command:test/resolution"),
                new EntityId("event:test/resolution")),
        ];
        return world;
    }

    private static AuthoritativeCharacterProfile Profile(
        EntityId characterId,
        CharacterConditionState? condition = null,
        IReadOnlyList<EntityId>? abilities = null,
        IReadOnlyList<EntityId>? reputations = null,
        IReadOnlyList<CharacterParentLink>? parents = null,
        IReadOnlyList<CharacterEducationAttainment>? attainments = null,
        EntityId? householdId = null)
    {
        EntityId name = new($"loc:{characterId.Value.Replace(':', '/')}/name");
        return new(
            CharacterContractVersions.AuthoritativeQuery,
            characterId,
            name,
            new CampaignDate(150, 1, 1),
            50,
            parents?.Select(item => item.ParentCharacterId).ToArray() ?? [],
            [],
            new EntityId("family:test/observer"),
            householdId,
            abilities ?? [],
            Ids("aptitude", 2),
            Ids("trait", 2),
            Ids("ambition", 2),
            reputations ?? [],
            new StructuredCharacterName(name, new EntityId("loc:test/courtesy")),
            CharacterContentOrigin.LegacyUnknown(characterId),
            new EntityId("culture:test/observer"),
            new EntityId("location:test/origin"),
            Ids("flaw", 2),
            condition ?? CharacterConditionState.Default,
            parents ?? [],
            [],
            attainments ?? []);
    }

    private static CampaignSimulation CreateSimulation(
        IReadOnlyList<EntityId> characters,
        SyntheticEntitySnapshot synthetic)
    {
        CharacterDefinition[] definitions = characters.Select(id =>
        {
            EntityId name = new($"loc:{id.Value.Replace(':', '/')}/name");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                name,
                new CampaignDate(150, 1, 1),
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(name, null),
                CharacterContentOrigin.LegacyUnknown(id),
                null,
                null,
                []);
        }).ToArray();
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            characters.Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                [],
                [],
                CharacterConditionState.Default)).ToArray(),
            [],
            []);
        return new CampaignSimulation(WorldState.Create(
            Date,
            1,
            [synthetic],
            GeographicWorldSnapshot.Empty,
            snapshot,
            RelationshipWorldSnapshot.Empty));
    }

    private static CampaignSimulation CreateSuccessionObserverSimulation(
        EntityId successor,
        EntityId householdId)
    {
        EntityId[] characters = [Subject, successor, Stranger];
        CharacterDefinition[] definitions = characters.Select(id =>
        {
            EntityId name = new($"loc:{id.Value.Replace(':', '/')}/name");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                name,
                id == successor
                    ? new CampaignDate(180, 1, 1)
                    : new CampaignDate(150, 1, 1),
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(name, null),
                CharacterContentOrigin.LegacyUnknown(id),
                null,
                null,
                []);
        }).ToArray();
        CharacterWorldSnapshot charactersSnapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    householdId,
                    new EntityId("loc:test/g07-household")),
            ],
            characters.Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                id == successor ? [Subject] : [],
                id == successor
                    ? [new CharacterParentLink(
                        Subject,
                        ParentChildLinkKind.Biological)]
                    : [],
                CharacterConditionState.Default)).ToArray(),
            [],
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    householdId,
                    Subject,
                    new[] { Subject, successor }.Order().ToArray()),
            ]);
        CharacterSuccessionWorldSnapshot succession =
            CharacterSuccessionWorldSnapshot.Empty with
            {
                CampaignContinuity = new PlayerCampaignContinuityState(
                    CharacterSuccessionContractVersions.CampaignContinuity,
                    PlayerCampaignContinuityStatus.Active,
                    Subject,
                    Date.AddDays(-1),
                    0,
                    new EntityId("command:test/g07-continuity"),
                    new EntityId("event:test/g07-continuity")),
            };
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            charactersSnapshot,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            succession));
    }

    private static SuccessionResolutionRule ResolutionRule() => new(
        CharacterSuccessionContractVersions.ResolutionRule,
        new SuccessionCandidateEligibilityRule(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            [SuccessionCandidateBasis.BiologicalDescendant],
            8,
            0,
            true,
            [CharacterCustodyStatus.Free]),
        [SuccessionLegalBasis.BiologicalDescendant],
        false,
        [],
        0,
        SuccessionContestResolutionMode.ResolveByStableId,
        32,
        8,
        true,
        SuccessionNoAcceptedSuccessorBehavior.ContinueWithoutControlledCharacter);

    private static PlayerCampaignContinuityState Continuity(
        PlayerCampaignContinuityStatus status,
        EntityId? characterId,
        string suffix) => new(
        CharacterSuccessionContractVersions.CampaignContinuity,
        status,
        characterId,
        Date,
        0,
        new EntityId($"command:test/continuity-{suffix}"),
        new EntityId($"event:test/continuity-{suffix}"));

    private static CharacterProfile AssertProfile(
        CharacterProfileQuery query,
        EntityId observer,
        EntityId subject)
    {
        Assert.True(query.TryGet(observer, subject, out CharacterProfile? profile));
        return profile;
    }

    private static SuccessionView AssertSuccession(
        SuccessionViewQuery query,
        EntityId observer,
        EntityId subject)
    {
        Assert.True(query.TryGet(observer, subject, out SuccessionView? view));
        return view;
    }

    private static FacadeViews CaptureViews(
        ControlledCharacterObserverFacade facade,
        EntityId characterId,
        EntityId householdId,
        EntityId relationshipSubjectId)
    {
        Assert.True(facade.TryGetCharacter(
            characterId,
            out CharacterProfile? character));
        Assert.True(facade.TryGetHousehold(
            householdId,
            out HouseholdView? household));
        Assert.True(facade.TryGetSuccession(
            characterId,
            out SuccessionView? succession));
        Assert.True(facade.TryGetRelationship(
            relationshipSubjectId,
            out RelationshipSummary? relationship));
        return new(character, household, succession, relationship);
    }

    private static CharacterConditionState CharacterCondition(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile.Condition;
    }

    private static void AssertControlled(
        ControlledCharacterObserverFacade facade,
        EntityId expected)
    {
        Assert.True(facade.TryGetControlledCharacterId(out EntityId actual));
        Assert.Equal(expected, actual);
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private static EntityId[] Ids(string prefix, int count) =>
        Enumerable.Range(0, count)
            .Select(index => new EntityId($"{prefix}:test/{index:D2}"))
            .Reverse()
            .ToArray();

    private static void AssertProperties<T>(params string[] expected)
    {
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    private sealed record FacadeViews(
        CharacterProfile Character,
        HouseholdView Household,
        SuccessionView Succession,
        RelationshipSummary Relationship);
}
