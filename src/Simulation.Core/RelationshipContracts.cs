using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class RelationshipContractVersions
{
    public const int LegacySnapshot = 1;
    public const int Snapshot = 2;
    public const int LegacyMemory = 1;
    public const int Memory = 2;
    public const int Consequence = 1;
    public const int State = 1;
    public const int AuthoritativeQuery = 1;
}

public static class RelationshipLimits
{
    public const int ConsequencesPerSourceEvent = 64;
}

public enum RelationshipMemorySourceKind
{
    RelationshipAction = 0,
    CharacterAction = 1,
    HouseholdDecision = 2,
    CharacterMarriageAction = 3,
    CharacterCondition = 4,
}

public enum RelationshipMemoryIdentityScheme
{
    LegacyRelationshipActionV1 = 0,
    SourceEventV2 = 1,
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

public sealed record RelationshipMemoryConsequenceSpecification(
    int ContractVersion,
    EntityId ConsequenceId,
    EntityId SubjectCharacterId,
    EntityId TargetCharacterId,
    RelationshipImpact Impact,
    EntityId MeaningId,
    int InitialSeverity,
    MemoryPublicity Publicity,
    int DecayIntervalTurns,
    IReadOnlyList<EntityId> WitnessIds);

[method: JsonConstructor]
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
    EntityId SourceEventId,
    RelationshipMemorySourceKind SourceKind,
    RelationshipMemoryIdentityScheme IdentityScheme,
    int ConsequenceIndex);

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

    public static EntityId DeriveMemoryId(
        EntityId sourceEventId,
        EntityId subjectCharacterId,
        EntityId targetCharacterId,
        int consequenceIndex)
    {
        if (!sourceEventId.IsValid
            || !subjectCharacterId.IsValid
            || !targetCharacterId.IsValid
            || subjectCharacterId == targetCharacterId
            || consequenceIndex < 0)
        {
            throw new ArgumentException(
                "Source-event memory IDs require a valid source event, different valid participants, and a non-negative consequence index.");
        }

        StringBuilder canonical = new();
        AppendField(canonical, "memory.source-event.v2");
        AppendField(canonical, sourceEventId.Value);
        AppendField(canonical, subjectCharacterId.Value);
        AppendField(canonical, targetCharacterId.Value);
        AppendField(canonical, consequenceIndex.ToString(CultureInfo.InvariantCulture));
        return Hash("memory", canonical.ToString());
    }

    private static void AppendField(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
        target.Append(';');
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
