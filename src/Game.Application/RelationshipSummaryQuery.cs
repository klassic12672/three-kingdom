using System.Diagnostics.CodeAnalysis;
using Simulation.Core;

namespace Game.Application;

public sealed class RelationshipSummaryQuery : IRelationshipSummaryQuery
{
    private readonly IWorldQuery world;

    public RelationshipSummaryQuery(IWorldQuery world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool TryGet(
        EntityId observerCharacterId,
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out RelationshipSummary? summary)
    {
        if (!world.Characters.TryGetCharacterProfile(observerCharacterId, out _)
            || !world.Characters.TryGetCharacterProfile(subjectCharacterId, out _))
        {
            summary = null;
            return false;
        }

        bool isSubject = observerCharacterId == subjectCharacterId;
        if (!world.Relationships.TryGetSubjectHistory(
            subjectCharacterId,
            out SubjectRelationshipHistory? history))
        {
            summary = isSubject
                ? new RelationshipSummary(
                    RelationshipQueryContractVersions.Summary,
                    subjectCharacterId,
                    [],
                    [],
                    DistantRelationshipHistoryAggregate.Empty with { })
                : null;
            return isSubject;
        }

        if (isSubject)
        {
            summary = new RelationshipSummary(
                RelationshipQueryContractVersions.Summary,
                subjectCharacterId,
                history.DetailedRelationships.Select(relationship =>
                    new VisibleDirectionalRelationshipSummary(
                        relationship.RelationshipId,
                        relationship.SubjectCharacterId,
                        relationship.TargetCharacterId,
                        relationship.Dimensions with { },
                        relationship.Memories
                            .Select(CreateMemorySummary)
                            .Where(memory => memory.EffectiveSeverity > 0)
                            .ToArray(),
                        relationship.FoldedMemories with { }))
                    .ToArray(),
                history.ArchivedRelationships.Select(relationship =>
                    new ArchivedRelationshipSummary(
                        relationship.RelationshipId,
                        relationship.SubjectCharacterId,
                        relationship.TargetCharacterId,
                        relationship.Dimensions with { },
                        relationship.LastChangeDate,
                        relationship.LastChangeTurnIndex,
                        relationship.FoldedMemories with { }))
                    .ToArray(),
                history.DistantHistory with { });
            return true;
        }

        VisibleDirectionalRelationshipSummary[] visible = history.DetailedRelationships
            .Select(relationship => CreateVisibleRelationship(relationship, observerCharacterId))
            .Where(relationship => relationship is not null)
            .Cast<VisibleDirectionalRelationshipSummary>()
            .ToArray();
        if (visible.Length == 0)
        {
            summary = null;
            return false;
        }

        summary = new RelationshipSummary(
            RelationshipQueryContractVersions.Summary,
            subjectCharacterId,
            visible,
            [],
            null);
        return true;
    }

    private VisibleDirectionalRelationshipSummary? CreateVisibleRelationship(
        DetailedDirectionalRelationship relationship,
        EntityId observerCharacterId)
    {
        RelationshipMemorySummary[] memories = relationship.Memories
            .Where(memory => IsVisible(memory, observerCharacterId))
            .Select(CreateMemorySummary)
            .Where(memory => memory.EffectiveSeverity > 0)
            .ToArray();
        return memories.Length == 0
            ? null
            : new VisibleDirectionalRelationshipSummary(
                relationship.RelationshipId,
                relationship.SubjectCharacterId,
                relationship.TargetCharacterId,
                null,
                memories,
                null);
    }

    private RelationshipMemorySummary CreateMemorySummary(ConsequentialMemory memory) => new(
        memory.MemoryId,
        memory.SubjectCharacterId,
        memory.TargetCharacterId,
        memory.WitnessIds.ToArray(),
        memory.ResolutionDate,
        memory.RecordedTurnIndex,
        memory.MeaningId,
        memory.InitialSeverity,
        RelationshipWorldState.GetEffectiveSeverity(memory, world.Calendar.TurnIndex),
        memory.Publicity,
        memory.DecayIntervalTurns,
        memory.AppliedImpact with { },
        memory.SourceRelationshipActionEventId);

    private static bool IsVisible(ConsequentialMemory memory, EntityId observerCharacterId) =>
        memory.Publicity switch
        {
            MemoryPublicity.Private => observerCharacterId == memory.SubjectCharacterId,
            MemoryPublicity.Participants => observerCharacterId == memory.SubjectCharacterId
                || observerCharacterId == memory.TargetCharacterId,
            MemoryPublicity.Witnessed => observerCharacterId == memory.SubjectCharacterId
                || observerCharacterId == memory.TargetCharacterId
                || memory.WitnessIds.Contains(observerCharacterId),
            MemoryPublicity.Public => true,
            _ => false,
        };
}
