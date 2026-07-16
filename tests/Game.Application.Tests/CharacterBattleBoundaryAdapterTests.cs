using System.Reflection;
using System.Text.Json;
using Simulation.Core;

namespace Game.Application.Tests;

public sealed class CharacterBattleBoundaryAdapterTests
{
    private static readonly CampaignDate Date = new(200, 1, 1);
    private static readonly EntityId Wounded = new("character:test/battle-wounded");
    private static readonly EntityId Captured = new("character:test/battle-captured");
    private static readonly EntityId Rescued = new("character:test/battle-rescued");
    private static readonly EntityId Dying = new("character:test/battle-dying");
    private static readonly EntityId Captor = new("character:test/battle-captor");
    private static readonly EntityId MemoryTarget = new("character:test/battle-memory-target");

    [Fact]
    public void SetupIsCanonicalBoundedAndExposesOnlyApprovedInputs()
    {
        TestWorldQuery world = new();
        EntityId first = new("character:test/setup-a");
        EntityId second = new("character:test/setup-b");
        world.CharacterData.Add(Profile(
            first,
            CharacterConditionState.Default with
            {
                HealthStatus = CharacterHealthStatus.Injured,
                IsIncapacitated = true,
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = second,
            },
            abilities: [new("ability:test/z"), new("ability:test/a")]));
        world.CharacterData.Add(Profile(second));
        RelationshipDimensions dimensions = new(1, 2, 3, 4, 5, 6, 7, 8, 9);
        world.RelationshipData.Add(new SubjectRelationshipHistory(
            RelationshipContractVersions.State,
            first,
            [
                new DetailedDirectionalRelationship(
                    RelationshipContractVersions.State,
                    RelationshipIds.DeriveRelationshipId(first, second),
                    first,
                    second,
                    dimensions,
                    45,
                    Date,
                    0,
                    [],
                    FoldedMemorySummary.Empty),
            ],
            [],
            DistantRelationshipHistoryAggregate.Empty));
        CharacterBattleBoundaryAdapter adapter = new(world);

        IReadOnlyList<CharacterBattleSetupContribution> setup =
            adapter.CreateSetupContributions([second, first]);

        Assert.Equal([first, second], setup.Select(item => item.CharacterId));
        CharacterBattleSetupContribution firstSetup = setup[0];
        Assert.Equal(
            [new EntityId("ability:test/a"), new EntityId("ability:test/z")],
            firstSetup.AbilityIds);
        Assert.Equal(CharacterHealthStatus.Injured, firstSetup.HealthStatus);
        Assert.True(firstSetup.IsIncapacitated);
        Assert.Equal(dimensions, firstSetup.DirectionalRelationshipModifiers[second]);
        Assert.Equal(
            RelationshipDimensions.Zero,
            setup[1].DirectionalRelationshipModifiers[first]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EntityId>)firstSetup.AbilityIds).Add(new("ability:test/leak")));
        Assert.False(firstSetup.DirectionalRelationshipModifiers
            is IDictionary<EntityId, RelationshipDimensions> mutable
            && !mutable.IsReadOnly);

        AssertProperties<CharacterBattleSetupContribution>(
            nameof(CharacterBattleSetupContribution.CharacterId),
            nameof(CharacterBattleSetupContribution.AbilityIds),
            nameof(CharacterBattleSetupContribution.AptitudeIds),
            nameof(CharacterBattleSetupContribution.TraitIds),
            nameof(CharacterBattleSetupContribution.FlawIds),
            nameof(CharacterBattleSetupContribution.HealthStatus),
            nameof(CharacterBattleSetupContribution.IsIncapacitated),
            nameof(CharacterBattleSetupContribution.DirectionalRelationshipModifiers));
        Assert.DoesNotContain(
            typeof(CharacterBattleSetupContribution).GetProperties(),
            property => property.Name.Contains("Custod", StringComparison.Ordinal)
                || property.Name.Contains("Vital", StringComparison.Ordinal)
                || property.Name == "Condition");

        Assert.Throws<ArgumentException>(() =>
            adapter.CreateSetupContributions([first, first]));
        world.CharacterData.Add(Profile(
            new EntityId("character:test/setup-dead"),
            CharacterConditionState.Default with
            {
                VitalStatus = CharacterVitalStatus.Dead,
                HealthStatus = CharacterHealthStatus.Critical,
                IsIncapacitated = true,
            }));
        Assert.Throws<ArgumentException>(() =>
            adapter.CreateSetupContributions(
                [new EntityId("character:test/setup-dead")]));
        Assert.Throws<ArgumentException>(() =>
            adapter.CreateSetupContributions(Enumerable.Range(0, 65)
                .Select(index => new EntityId($"character:test/setup-{index:D2}"))
                .ToArray()));
    }

    [Fact]
    public void ResultCommandsRoundTripAndResolveWoundCustodyRescueDeathAndMemories()
    {
        CampaignSimulation simulation = CreateBattleSimulation();
        CharacterBattleBoundaryAdapter adapter = new(simulation.World);
        CharacterBattleResultContribution[] nonDeathInput =
        [
            Contribution(
                Rescued,
                releasesCustody: true),
            Contribution(
                Captured,
                captor: Captor),
            Contribution(
                Wounded,
                wound: CharacterHealthStatus.Injured,
                memories: [Memory(MemoryTarget, MemoryPublicity.Participants, trust: 3)]),
        ];

        IReadOnlyList<CampaignCommand> nonDeathCommands = adapter.CreateResultCommands(
            new EntityId("battle_result:test/round-trip-condition"),
            Date,
            nonDeathInput);
        IReadOnlyList<CampaignCommand> reordered = adapter.CreateResultCommands(
            new EntityId("battle_result:test/round-trip-condition"),
            Date,
            nonDeathInput.Reverse().ToArray());

        Assert.Equal(4, nonDeathCommands.Count);
        Assert.Equal(Serialize(nonDeathCommands), Serialize(reordered));
        Assert.Equal(
            [0, 10, 20, 20],
            nonDeathCommands.Select(item => item.Priority));
        Assert.Equal(
            Serialize(nonDeathCommands),
            Serialize(JsonSerializer.Deserialize<CampaignCommand[]>(
                Serialize(nonDeathCommands),
                SimulationJson.CreateOptions())!));

        foreach (CampaignCommand command in nonDeathCommands)
        {
            CommandValidationResult validation = simulation.Submit(command);
            Assert.True(
                validation.IsValid,
                string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }

        IReadOnlyList<CampaignEvent> nonDeathEvents = simulation.ResolveTurn();
        Assert.Equal(nonDeathCommands.Count, nonDeathEvents.Count);
        Assert.Equal(CharacterHealthStatus.Injured, Condition(simulation, Wounded).HealthStatus);
        Assert.Equal(CharacterCustodyStatus.Captive, Condition(simulation, Captured).CustodyStatus);
        Assert.Equal(Captor, Condition(simulation, Captured).CustodianId);
        Assert.Equal(CharacterCustodyStatus.Free, Condition(simulation, Rescued).CustodyStatus);
        Assert.Null(Condition(simulation, Rescued).CustodianId);
        Assert.Contains(
            simulation.World.Relationships.Subjects,
            history => history.SubjectCharacterId == Wounded
                && history.DetailedRelationships.Single().TargetCharacterId == MemoryTarget);

        CampaignDate deathDate = simulation.World.Calendar.Date;
        ResolveCharacterSuccessionDeathAction death = DeathAction(simulation);
        CharacterBattleResultContribution deathContribution = Contribution(
            Dying,
            death: death,
            memories: [Memory(MemoryTarget, MemoryPublicity.Public, respect: 2)]);
        IReadOnlyList<CampaignCommand> deathCommands = adapter.CreateResultCommands(
            new EntityId("battle_result:test/round-trip-death"),
            deathDate,
            [deathContribution]);
        Assert.Equal([0, 30], deathCommands.Select(item => item.Priority));
        Assert.IsType<ResolveCharacterSuccessionDeathAction>(
            Assert.IsType<CharacterConditionActionCommandPayload>(
                deathCommands[^1].Payload).Action);
        foreach (CampaignCommand command in deathCommands)
        {
            Assert.True(simulation.Submit(command).IsValid);
        }

        Assert.Equal(deathCommands.Count, simulation.ResolveTurn().Count);
        Assert.Equal(CharacterVitalStatus.Dead, Condition(simulation, Dying).VitalStatus);
        Assert.True(simulation.World.CharacterSuccessions.TryGetResolutionForSubject(
            Dying,
            out SuccessionResolutionState? resolution));
        Assert.Equal(SuccessionResolutionStatus.NoSuccessor, resolution.Status);
        Assert.Contains(
            simulation.World.Relationships.Subjects,
            history => history.SubjectCharacterId == Dying
                && history.DetailedRelationships.Single().TargetCharacterId == MemoryTarget);

        CampaignCommand woundCommand = nonDeathCommands.Single(item =>
            item.Payload is CharacterConditionActionCommandPayload
            {
                Action: ApplyCharacterWoundAction,
            });
        Assert.False(simulation.Submit(woundCommand).IsValid);
    }

    [Fact]
    public void ResultBoundaryRejectsInvalidOrOverlappingOutcomesAndBounds()
    {
        TestWorldQuery world = new();
        CharacterConditionState incapacitated = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
        };
        world.CharacterData.Add(Profile(Wounded, incapacitated));
        world.CharacterData.Add(Profile(Captor));
        CharacterBattleBoundaryAdapter adapter = new(world);

        Assert.Throws<ArgumentException>(() =>
            adapter.CreateResultCommands(
                new EntityId("battle_result:test/restore"),
                Date,
                [
                    Contribution(
                        Wounded,
                        wound: CharacterHealthStatus.Injured,
                        woundIncapacitated: false),
                ]));
        Assert.Throws<ArgumentException>(() =>
            adapter.CreateResultCommands(
                new EntityId("battle_result:test/critical"),
                Date,
                [
                    Contribution(
                        Wounded,
                        wound: CharacterHealthStatus.Critical,
                        woundIncapacitated: false),
                ]));
        Assert.Throws<ArgumentException>(() =>
            adapter.CreateResultCommands(
                new EntityId("battle_result:test/overlap"),
                Date,
                [
                    Contribution(
                        Wounded,
                        wound: CharacterHealthStatus.Injured,
                        woundIncapacitated: true,
                        captor: Captor),
                ]));
        Assert.Throws<ArgumentException>(() =>
            adapter.CreateResultCommands(
                new EntityId("battle_result:test/bounds"),
                Date,
                Enumerable.Range(0, 65)
                    .Select(index => Contribution(
                        new EntityId($"character:test/result-{index:D2}")))
                    .ToArray()));

        CampaignSimulation deathSimulation = CreateBattleSimulation();
        CharacterBattleBoundaryAdapter deathAdapter = new(deathSimulation.World);
        Assert.Throws<ArgumentException>(() =>
            deathAdapter.CreateResultCommands(
                new EntityId("battle_result:test/mixed-death"),
                Date,
                [
                    Contribution(
                        Wounded,
                        wound: CharacterHealthStatus.Injured),
                    Contribution(
                        Dying,
                        death: DeathAction(deathSimulation)),
                ]));
    }

    [Fact]
    public void ApplicationExportsOnlyTheThreeFrozenBattleTypes()
    {
        Assert.Equal(
            [
                nameof(CharacterBattleBoundaryAdapter),
                nameof(CharacterBattleResultContribution),
                nameof(CharacterBattleSetupContribution),
            ],
            typeof(CharacterBattleBoundaryAdapter).Assembly
                .GetExportedTypes()
                .Where(type => type.Namespace == "Game.Application"
                    && type.Name.Contains("Battle", StringComparison.Ordinal))
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal));
        AssertProperties<CharacterBattleResultContribution>(
            nameof(CharacterBattleResultContribution.CharacterId),
            nameof(CharacterBattleResultContribution.ResultingWoundHealthStatus),
            nameof(CharacterBattleResultContribution.ResultingWoundIncapacitated),
            nameof(CharacterBattleResultContribution.CaptorCharacterId),
            nameof(CharacterBattleResultContribution.ReleasesCustody),
            nameof(CharacterBattleResultContribution.DeathAction),
            nameof(CharacterBattleResultContribution.SharedMemories));
    }

    private static CampaignSimulation CreateBattleSimulation()
    {
        EntityId[] characters =
        [
            Wounded,
            Captured,
            Rescued,
            Dying,
            Captor,
            MemoryTarget,
        ];
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
        CharacterState[] states = characters.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            id == Rescued
                ? CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Captor,
                }
                : CharacterConditionState.Default)).ToArray();
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            snapshot,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            CharacterSuccessionWorldSnapshot.Empty));
    }

    private static ResolveCharacterSuccessionDeathAction DeathAction(
        CampaignSimulation simulation)
    {
        SuccessionResolutionRule rule = ResolutionRule();
        CampaignDate date = simulation.World.Calendar.Date;
        EntityId stateId = simulation.World.GetCharacterSuccessionResolutionStateId(
            Dying,
            rule,
            date,
            simulation.World.Calendar.TurnIndex);
        return new(
            Dying,
            Condition(simulation, Dying),
            rule,
            stateId,
            null,
            null,
            null);
    }

    private static SuccessionResolutionRule ResolutionRule() => new(
        CharacterSuccessionContractVersions.ResolutionRule,
        new SuccessionCandidateEligibilityRule(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            [SuccessionCandidateBasis.BiologicalDescendant],
            8,
            0,
            true,
            Enum.GetValues<CharacterCustodyStatus>()),
        [SuccessionLegalBasis.BiologicalDescendant],
        false,
        [],
        0,
        SuccessionContestResolutionMode.ResolveByStableId,
        64,
        16,
        true,
        SuccessionNoAcceptedSuccessorBehavior.ContinueWithoutControlledCharacter);

    private static CharacterBattleResultContribution Contribution(
        EntityId characterId,
        CharacterHealthStatus? wound = null,
        bool woundIncapacitated = false,
        EntityId? captor = null,
        bool releasesCustody = false,
        ResolveCharacterSuccessionDeathAction? death = null,
        IReadOnlyList<RelationshipActionCommandPayload>? memories = null) => new(
        characterId,
        wound,
        woundIncapacitated,
        captor,
        releasesCustody,
        death,
        memories ?? []);

    private static RelationshipActionCommandPayload Memory(
        EntityId target,
        MemoryPublicity publicity,
        int trust = 0,
        int respect = 0) => new(
        target,
        new RelationshipImpact(0, trust, respect, 0, 0, 0, 0, 0, 0),
        new EntityId("memory_meaning:test/battle"),
        20,
        publicity,
        0,
        []);

    private static AuthoritativeCharacterProfile Profile(
        EntityId characterId,
        CharacterConditionState? condition = null,
        IReadOnlyList<EntityId>? abilities = null)
    {
        EntityId name = new($"loc:{characterId.Value.Replace(':', '/')}/name");
        return new(
            CharacterContractVersions.AuthoritativeQuery,
            characterId,
            name,
            new CampaignDate(150, 1, 1),
            50,
            [],
            [],
            null,
            null,
            abilities ?? [],
            [new EntityId("aptitude:test/battle")],
            [new EntityId("trait:test/battle")],
            [],
            [],
            new StructuredCharacterName(name, null),
            CharacterContentOrigin.LegacyUnknown(characterId),
            null,
            null,
            [new EntityId("flaw:test/battle")],
            condition ?? CharacterConditionState.Default,
            [],
            [],
            []);
    }

    private static CharacterConditionState Condition(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile.Condition;
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private static void AssertProperties<T>(params string[] expected)
    {
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }
}
