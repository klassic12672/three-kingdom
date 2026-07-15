using System.Diagnostics.CodeAnalysis;
using Simulation.Core;

namespace Game.Application;

public static class RelationshipQueryContractVersions
{
    public const int Summary = 2;
}

public sealed record RelationshipMemorySummary(
    EntityId MemoryId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    IReadOnlyList<EntityId> WitnessIds,
    CampaignDate ResolutionDate,
    long RecordedTurnIndex,
    EntityId MeaningId,
    int InitialSeverity,
    int EffectiveSeverity,
    MemoryPublicity Publicity,
    int DecayIntervalTurns,
    RelationshipImpact AppliedImpact,
    EntityId SourceEventId,
    RelationshipMemorySourceKind SourceKind,
    RelationshipMemoryIdentityScheme IdentityScheme,
    int ConsequenceIndex);

public sealed record VisibleDirectionalRelationshipSummary(
    EntityId RelationshipId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    RelationshipDimensions? ExactDimensions,
    IReadOnlyList<RelationshipMemorySummary> ActiveMemories,
    FoldedMemorySummary? FoldedMemories);

public sealed record ArchivedRelationshipSummary(
    EntityId RelationshipId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    RelationshipDimensions Dimensions,
    CampaignDate LastChangeDate,
    long LastChangeTurnIndex,
    FoldedMemorySummary FoldedMemories);

public sealed record RelationshipSummary(
    int ContractVersion,
    EntityId SubjectCharacterId,
    IReadOnlyList<VisibleDirectionalRelationshipSummary> DetailedRelationships,
    IReadOnlyList<ArchivedRelationshipSummary> ArchivedRelationships,
    DistantRelationshipHistoryAggregate? DistantHistory);

public interface IRelationshipSummaryQuery
{
    bool TryGet(
        EntityId observerCharacterId,
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out RelationshipSummary? summary);
}
