using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Simulation.Core;

public static class RelationshipContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public enum MemoryPublicity
{
    Private = 0,
    Participants = 1,
    Witnessed = 2,
    Public = 3,
}

public sealed record RelationshipDimensions(
    int Affection,
    int Trust,
    int Respect,
    int Attraction,
    int Obligation,
    int Fear,
    int Resentment,
    int Rivalry,
    int Compatibility)
{
    public static RelationshipDimensions Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record RelationshipImpact(
    int Affection,
    int Trust,
    int Respect,
    int Attraction,
    int Obligation,
    int Fear,
    int Resentment,
    int Rivalry,
    int Compatibility)
{
    public bool HasAnyChange => Affection != 0
        || Trust != 0
        || Respect != 0
        || Attraction != 0
        || Obligation != 0
        || Fear != 0
        || Resentment != 0
        || Rivalry != 0
        || Compatibility != 0;
}

public sealed record ConsequentialMemory(
    int ContractVersion,
    EntityId MemoryId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    IReadOnlyList<EntityId> WitnessIds,
    CampaignDate ResolutionDate,
    long RecordedTurnIndex,
    EntityId MeaningId,
    int InitialSeverity,
    MemoryPublicity Publicity,
    int DecayIntervalTurns,
    RelationshipImpact AppliedImpact,
    EntityId SourceRelationshipActionEventId);

public sealed record FoldedMemorySummary(
    long MemoryCount,
    long TotalEffectiveSeverity,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate)
{
    public static FoldedMemorySummary Empty { get; } = new(0, 0, null, null);
}

public sealed record DetailedDirectionalRelationship(
    int ContractVersion,
    EntityId RelationshipId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    RelationshipDimensions Dimensions,
    long RecordedImportance,
    CampaignDate LastChangeDate,
    long LastChangeTurnIndex,
    IReadOnlyList<ConsequentialMemory> Memories,
    FoldedMemorySummary FoldedMemories);

public sealed record ArchivedDirectionalRelationshipSummary(
    int ContractVersion,
    EntityId RelationshipId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    RelationshipDimensions Dimensions,
    long RecordedImportance,
    CampaignDate LastChangeDate,
    long LastChangeTurnIndex,
    FoldedMemorySummary FoldedMemories);

public sealed record DistantRelationshipHistoryAggregate(
    long RelationshipCount,
    long MemoryCount,
    long TotalRecordedImportance,
    long TotalEffectiveMemorySeverity,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate,
    long LatestChangeTurnIndex)
{
    public static DistantRelationshipHistoryAggregate Empty { get; } = new(0, 0, 0, 0, null, null, 0);
}

public sealed record SubjectRelationshipHistory(
    int ContractVersion,
    EntityId SubjectCharacterId,
    IReadOnlyList<DetailedDirectionalRelationship> DetailedRelationships,
    IReadOnlyList<ArchivedDirectionalRelationshipSummary> ArchivedRelationships,
    DistantRelationshipHistoryAggregate DistantHistory);

public sealed record RelationshipWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<SubjectRelationshipHistory> Subjects)
{
    public static RelationshipWorldSnapshot Empty { get; } = new(
        RelationshipContractVersions.Snapshot,
        []);
}

public sealed record RelationshipActionCommandPayload(
    EntityId TargetCharacterId,
    RelationshipImpact Impact,
    EntityId MeaningId,
    int InitialSeverity,
    MemoryPublicity Publicity,
    int DecayIntervalTurns,
    IReadOnlyList<EntityId> WitnessIds)
    : ICampaignCommandPayload;

public sealed record RelationshipActionResolvedEventPayload(
    EntityId RelationshipId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    ConsequentialMemory Memory)
    : ICampaignEventPayload;

public static class RelationshipIds
{
    public static EntityId DeriveRelationshipId(EntityId subjectCharacterId, EntityId targetCharacterId)
    {
        if (!subjectCharacterId.IsValid
            || !targetCharacterId.IsValid
            || subjectCharacterId == targetCharacterId)
        {
            throw new ArgumentException(
                "Relationship IDs require different valid subject and target character IDs.");
        }

        string canonical = string.Concat(
            "relationship.v1\n",
            subjectCharacterId.Value,
            "\n",
            targetCharacterId.Value);
        return Hash("relationship", canonical);
    }

    public static EntityId DeriveMemoryId(CampaignDate resolutionDate, EntityId commandId)
    {
        if (!resolutionDate.IsValid || !commandId.IsValid)
        {
            throw new ArgumentException(
                "Memory IDs require a valid resolution date and command ID.");
        }

        string canonical = string.Concat(
            "memory.v1\n",
            resolutionDate.Year.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            resolutionDate.Month.ToString("D2", CultureInfo.InvariantCulture),
            "-",
            resolutionDate.Day.ToString("D2", CultureInfo.InvariantCulture),
            "\n",
            commandId.Value);
        return Hash("memory", canonical);
    }

    private static EntityId Hash(string entityNamespace, string canonical)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new EntityId($"{entityNamespace}:sha256/{Convert.ToHexStringLower(digest)}");
    }
}

public interface IAuthoritativeRelationshipWorldQuery
{
    IReadOnlyList<SubjectRelationshipHistory> Subjects { get; }

    bool TryGetSubjectHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SubjectRelationshipHistory? history);
}
