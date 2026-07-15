using Simulation.Core;

namespace Game.Application.Tests;

public sealed class RelationshipSummaryQueryTests
{
    private static readonly CampaignDate StartDate = new(200, 1, 1);
    private static readonly EntityId Subject = new("character:test/subject");
    private static readonly EntityId Target = new("character:test/target");
    private static readonly EntityId Witness = new("character:test/witness");
    private static readonly EntityId Stranger = new("character:test/stranger");
    private static readonly EntityId PrivateTarget = new("character:test/private_target");

    [Fact]
    public void ObserverMatrixReturnsOnlyActiveVisibleMemoriesWithoutPrivateStateLeakage()
    {
        CampaignSimulation simulation = CreateSimulation(
            [Subject, Target, Witness, Stranger, PrivateTarget]);
        Submit(
            simulation,
            "private",
            Target,
            MemoryPublicity.Private,
            Impact(affection: 1));
        Submit(
            simulation,
            "participants",
            Target,
            MemoryPublicity.Participants,
            Impact(trust: 1));
        Submit(
            simulation,
            "witnessed",
            Target,
            MemoryPublicity.Witnessed,
            Impact(respect: 1),
            [Witness]);
        Submit(
            simulation,
            "public",
            Target,
            MemoryPublicity.Public,
            Impact(obligation: 1));
        Submit(
            simulation,
            "private-link",
            PrivateTarget,
            MemoryPublicity.Private,
            Impact(fear: 1));
        simulation.ResolveTurn();
        RelationshipSummaryQuery query = new(simulation.World);

        RelationshipSummary subject = AssertSummary(query, Subject, Subject);
        Assert.Equal(2, subject.DetailedRelationships.Count);
        VisibleDirectionalRelationshipSummary subjectTarget = subject.DetailedRelationships
            .Single(item => item.TargetCharacterId == Target);
        Assert.NotNull(subjectTarget.ExactDimensions);
        Assert.Equal(4, subjectTarget.ActiveMemories.Count);
        Assert.NotNull(subjectTarget.FoldedMemories);
        Assert.NotNull(subject.DistantHistory);

        RelationshipSummary target = AssertSummary(query, Target, Subject);
        AssertVisiblePublicities(
            target,
            MemoryPublicity.Participants,
            MemoryPublicity.Witnessed,
            MemoryPublicity.Public);
        RelationshipSummary witness = AssertSummary(query, Witness, Subject);
        AssertVisiblePublicities(witness, MemoryPublicity.Witnessed, MemoryPublicity.Public);
        RelationshipSummary stranger = AssertSummary(query, Stranger, Subject);
        AssertVisiblePublicities(stranger, MemoryPublicity.Public);

        foreach (RelationshipSummary filtered in new[] { target, witness, stranger })
        {
            VisibleDirectionalRelationshipSummary relationship = Assert.Single(filtered.DetailedRelationships);
            Assert.Equal(Target, relationship.TargetCharacterId);
            Assert.Null(relationship.ExactDimensions);
            Assert.Null(relationship.FoldedMemories);
            Assert.Empty(filtered.ArchivedRelationships);
            Assert.Null(filtered.DistantHistory);
        }
    }

    [Fact]
    public void DecayedMemoryStopsVisibilityWithoutMutatingAuthoritativeHistory()
    {
        CampaignSimulation simulation = CreateSimulation([Subject, Target]);
        Submit(
            simulation,
            "decay",
            Target,
            MemoryPublicity.Participants,
            Impact(trust: 1),
            severity: 2,
            decay: 1);
        simulation.ResolveTurn();
        RelationshipSummaryQuery query = new(simulation.World);

        RelationshipSummary visible = AssertSummary(query, Target, Subject);
        Assert.Equal(1, Assert.Single(Assert.Single(visible.DetailedRelationships).ActiveMemories).EffectiveSeverity);

        simulation.ResolveTurn();

        Assert.False(query.TryGet(Target, Subject, out RelationshipSummary? hidden));
        Assert.Null(hidden);
        RelationshipSummary own = AssertSummary(query, Subject, Subject);
        Assert.Empty(Assert.Single(own.DetailedRelationships).ActiveMemories);
        Assert.Single(simulation.World.Relationships.Subjects
            .Single()
            .DetailedRelationships
            .Single()
            .Memories);
    }

    [Fact]
    public void SubjectSeesDetailedArchiveAndDistantHistoryAtApprovedBounds()
    {
        EntityId[] characters = Enumerable.Range(0, 194)
            .Select(index => index == 0 ? Subject : Character(index))
            .ToArray();
        CampaignSimulation simulation = CreateSimulation(characters);
        for (int index = 1; index <= 193; index++)
        {
            Submit(
                simulation,
                $"bounded-{index:D3}",
                Character(index),
                MemoryPublicity.Private,
                Impact(affection: 1),
                severity: 1);
            simulation.ResolveTurn();
        }

        RelationshipSummary summary = AssertSummary(
            new RelationshipSummaryQuery(simulation.World),
            Subject,
            Subject);

        Assert.Equal(64, summary.DetailedRelationships.Count);
        Assert.Equal(128, summary.ArchivedRelationships.Count);
        Assert.All(summary.DetailedRelationships, item => Assert.NotNull(item.ExactDimensions));
        DistantRelationshipHistoryAggregate distant = Assert.IsType<DistantRelationshipHistoryAggregate>(
            summary.DistantHistory);
        Assert.Equal(1, distant.RelationshipCount);
        Assert.Equal(1, distant.MemoryCount);
    }

    [Fact]
    public void QueryRequiresExistingCharactersAndReturnsEmptySelfSummaryWithoutHistory()
    {
        CampaignSimulation simulation = CreateSimulation([Subject, Target]);
        RelationshipSummaryQuery query = new(simulation.World);

        RelationshipSummary empty = AssertSummary(query, Subject, Subject);
        Assert.Empty(empty.DetailedRelationships);
        Assert.Empty(empty.ArchivedRelationships);
        Assert.Equal(DistantRelationshipHistoryAggregate.Empty, empty.DistantHistory);
        Assert.False(query.TryGet(Target, Subject, out _));
        Assert.False(query.TryGet(new EntityId("character:test/missing"), Subject, out _));
        Assert.False(query.TryGet(Subject, new EntityId("character:test/missing"), out _));
    }

    [Fact]
    public void SummaryAndNestedCollectionsAreDefensiveCopies()
    {
        CampaignSimulation simulation = CreateSimulation([Subject, Target, Witness]);
        Submit(
            simulation,
            "defensive",
            Target,
            MemoryPublicity.Witnessed,
            Impact(respect: 1),
            [Witness]);
        simulation.ResolveTurn();
        RelationshipSummaryQuery query = new(simulation.World);
        RelationshipSummary first = AssertSummary(query, Subject, Subject);
        VisibleDirectionalRelationshipSummary relationship = Assert.Single(first.DetailedRelationships);
        RelationshipMemorySummary memory = Assert.Single(relationship.ActiveMemories);

        ((VisibleDirectionalRelationshipSummary[])first.DetailedRelationships)[0] = relationship with
        {
            ExactDimensions = RelationshipDimensions.Zero,
        };
        ((EntityId[])memory.WitnessIds)[0] = Target;

        RelationshipSummary second = AssertSummary(query, Subject, Subject);
        VisibleDirectionalRelationshipSummary unchanged = Assert.Single(second.DetailedRelationships);
        Assert.Equal(1, unchanged.ExactDimensions!.Respect);
        Assert.Equal(Witness, Assert.Single(Assert.Single(unchanged.ActiveMemories).WitnessIds));
    }

    private static CampaignSimulation CreateSimulation(IReadOnlyList<EntityId> characterIds)
    {
        CharacterDefinition[] definitions = characterIds.Select(id =>
        {
            EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}/name");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                nameKey,
                new CampaignDate(150, 1, 1),
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(nameKey, null),
                CharacterContentOrigin.LegacyUnknown(id),
                null,
                null,
                []);
        }).ToArray();
        CharacterState[] states = characterIds.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            CharacterConditionState.Default)).ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        WorldState world = WorldState.Create(
            StartDate,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty);
        return new CampaignSimulation(world);
    }

    private static void Submit(
        CampaignSimulation simulation,
        string suffix,
        EntityId target,
        MemoryPublicity publicity,
        RelationshipImpact impact,
        IReadOnlyList<EntityId>? witnesses = null,
        int severity = 10,
        int decay = 0)
    {
        CampaignCommand command = CampaignCommand.Create(
            new EntityId($"command:test/{suffix}"),
            Subject,
            simulation.World.Calendar.Date,
            new RelationshipActionCommandPayload(
                target,
                impact,
                new EntityId("memory_meaning:test/query"),
                severity,
                publicity,
                decay,
                witnesses ?? []));
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Issues.Select(issue => issue.Message)));
    }

    private static RelationshipSummary AssertSummary(
        IRelationshipSummaryQuery query,
        EntityId observer,
        EntityId subject)
    {
        Assert.True(query.TryGet(observer, subject, out RelationshipSummary? summary));
        return summary;
    }

    private static void AssertVisiblePublicities(
        RelationshipSummary summary,
        params MemoryPublicity[] expected)
    {
        Assert.Equal(
            expected.Order(),
            Assert.Single(summary.DetailedRelationships)
                .ActiveMemories
                .Select(item => item.Publicity)
                .Order());
    }

    private static RelationshipImpact Impact(
        int affection = 0,
        int trust = 0,
        int respect = 0,
        int obligation = 0,
        int fear = 0) => new(
        affection,
        trust,
        respect,
        0,
        obligation,
        fear,
        0,
        0,
        0);

    private static EntityId Character(int index) => new($"character:test/{index:D3}");
}
