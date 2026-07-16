using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterSuccessionContractVersions
{
    public const int Snapshot = 2;
    public const int State = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int ClaimState = 1;
    public const int ClaimHistory = 1;
    public const int ClaimAction = 1;
    public const int ClaimOutcome = 1;
    public const int CandidateEligibilityRule = 1;
    public const int CandidateEvaluation = 1;
    public const int CandidateSet = 1;
    public const int AuthoritativeQuery = 4;
}

public static class CharacterSuccessionLimits
{
    public const int RecentTerminalDesignationsPerCharacter = 32;
    public const int MaximumActiveClaimsPerSubject = 64;
    public const int MaximumActiveClaimsPerClaimant = 64;
    public const int RecentWithdrawnClaimsPerSubject = 32;
    public const int MaximumEvaluatedDescendantGeneration = 64;
    public const int MaximumConfiguredMinimumCandidateAge = 100;
}

public static class CharacterSuccessionSystem
{
    public const string SystemId = "simulation.character_succession";
    public const int Version = 2;
}

public enum HeirDesignationStatus
{
    Active = 0,
    Replaced = 1,
    Revoked = 2,
}

public enum SuccessionClaimOrigin
{
    PersonalAssertion = 0,
}

public enum SuccessionClaimStatus
{
    Active = 0,
    Withdrawn = 1,
}

public enum SuccessionCandidateBasis
{
    ActiveDesignation = 0,
    BiologicalDescendant = 1,
    LegalAdoptiveDescendant = 2,
    UnspecifiedLegacyDescendant = 3,
}

public enum SuccessionCandidateEligibilityReason
{
    InvalidRequest = 0,
    UnsupportedRuleVersion = 1,
    MissingAllowedBasis = 2,
    UnsupportedAllowedBasis = 3,
    DuplicateAllowedBasis = 4,
    InvalidMaximumDescendantGeneration = 5,
    InvalidMinimumCandidateAge = 6,
    MissingAllowedCustodyStatus = 7,
    UnsupportedAllowedCustodyStatus = 8,
    DuplicateAllowedCustodyStatus = 9,
    InvalidSubject = 10,
    UnknownSubject = 11,
    SubjectNotBorn = 12,
    InvalidCandidate = 13,
    UnknownCandidate = 14,
    SameCharacter = 15,
    CandidateNotBorn = 16,
    CandidateDead = 17,
    CandidateBelowMinimumAge = 18,
    CandidateIncapacitated = 19,
    CandidateCustodyNotAllowed = 20,
    NoRecognizedBasis = 21,
}

public enum SuccessionCandidateSetStatus
{
    InvalidRequest = 0,
    Complete = 1,
    MaximumCandidatesExceeded = 2,
}

public enum SuccessionCandidateSetIssueReason
{
    InvalidRequest = 0,
    InvalidMaximumCandidates = 1,
    UnsupportedRuleVersion = 2,
    MissingAllowedBasis = 3,
    UnsupportedAllowedBasis = 4,
    DuplicateAllowedBasis = 5,
    InvalidMaximumDescendantGeneration = 6,
    InvalidMinimumCandidateAge = 7,
    MissingAllowedCustodyStatus = 8,
    UnsupportedAllowedCustodyStatus = 9,
    DuplicateAllowedCustodyStatus = 10,
    InvalidSubject = 11,
    UnknownSubject = 12,
    SubjectNotBorn = 13,
    MaximumCandidatesExceeded = 14,
}

public sealed record SuccessionCandidateEligibilityRule(
    int ContractVersion,
    IReadOnlyList<SuccessionCandidateBasis> AllowedBases,
    int MaximumDescendantGeneration,
    int MinimumCandidateAge,
    bool AllowsIncapacitatedCandidates,
    IReadOnlyList<CharacterCustodyStatus> AllowedCustodyStatuses)
{
    public SuccessionCandidateEligibilityRule Canonicalize() => this with
    {
        AllowedBases = AllowedBases is null
            ? null!
            : AllowedBases.Order().ToArray(),
        AllowedCustodyStatuses = AllowedCustodyStatuses is null
            ? null!
            : AllowedCustodyStatuses.Order().ToArray(),
    };
}

public sealed record SuccessionCandidateEvaluationRequest(
    int ContractVersion,
    EntityId SubjectCharacterId,
    EntityId CandidateCharacterId,
    SuccessionCandidateEligibilityRule Rule);

public sealed record SuccessionCandidateBasisEvidence(
    int ContractVersion,
    SuccessionCandidateBasis Basis,
    int? DescendantGeneration,
    EntityId? SourceDesignationId);

public sealed record SuccessionCandidateEligibilityIssue(
    int ContractVersion,
    SuccessionCandidateEligibilityReason Reason);

public sealed record SuccessionCandidateEvaluationResult(
    int ContractVersion,
    EntityId? SubjectCharacterId,
    EntityId? CandidateCharacterId,
    CampaignDate EvaluationDate,
    long EvaluationTurnIndex,
    IReadOnlyList<SuccessionCandidateBasisEvidence> RecognizedBases,
    IReadOnlyList<SuccessionCandidateEligibilityIssue> Issues,
    bool IsEligible);

public sealed record SuccessionCandidateSetRequest(
    int ContractVersion,
    EntityId SubjectCharacterId,
    SuccessionCandidateEligibilityRule Rule,
    int MaximumCandidates);

public sealed record SuccessionCandidateSetEntry(
    int ContractVersion,
    EntityId CandidateCharacterId,
    IReadOnlyList<SuccessionCandidateBasisEvidence> RecognizedBases);

public sealed record SuccessionCandidateSetIssue(
    int ContractVersion,
    SuccessionCandidateSetIssueReason Reason);

public sealed record SuccessionCandidateSetResult(
    int ContractVersion,
    EntityId? SubjectCharacterId,
    CampaignDate EvaluationDate,
    long EvaluationTurnIndex,
    int MaximumCandidates,
    int EligibleCandidateCount,
    IReadOnlyList<SuccessionCandidateSetEntry> Candidates,
    IReadOnlyList<SuccessionCandidateSetIssue> Issues,
    SuccessionCandidateSetStatus Status);

public sealed record HeirDesignationState(
    int ContractVersion,
    EntityId DesignationId,
    EntityId DesignatorCharacterId,
    EntityId HeirCharacterId,
    CampaignDate EstablishedDate,
    long EstablishedTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId,
    HeirDesignationStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId,
    EntityId? ResolutionEventId);

public sealed record HeirDesignationHistoryAggregate(
    int ContractVersion,
    EntityId DesignatorCharacterId,
    long FoldedReplacedCount,
    long FoldedRevokedCount,
    CampaignDate EarliestDate,
    CampaignDate LatestDate)
{
    [JsonIgnore]
    public long TotalFoldedCount => checked(FoldedReplacedCount + FoldedRevokedCount);
}

public sealed record SuccessionClaimState(
    int ContractVersion,
    EntityId ClaimId,
    EntityId SubjectCharacterId,
    EntityId ClaimantCharacterId,
    SuccessionClaimOrigin Origin,
    CampaignDate AssertedDate,
    long AssertedTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId,
    SuccessionClaimStatus Status,
    CampaignDate? WithdrawalDate,
    long? WithdrawalTurnIndex,
    EntityId? WithdrawalCommandId,
    EntityId? WithdrawalEventId);

public sealed record SuccessionClaimHistoryAggregate(
    int ContractVersion,
    EntityId SubjectCharacterId,
    long FoldedWithdrawnCount,
    CampaignDate EarliestDate,
    CampaignDate LatestDate)
{
    [JsonIgnore]
    public long TotalFoldedCount => checked(FoldedWithdrawnCount);
}

public sealed record CharacterSuccessionWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<HeirDesignationState> Designations,
    IReadOnlyList<HeirDesignationHistoryAggregate> History,
    IReadOnlyList<SuccessionClaimState> Claims,
    IReadOnlyList<SuccessionClaimHistoryAggregate> ClaimHistory)
{
    public static CharacterSuccessionWorldSnapshot Empty { get; } = new(
        CharacterSuccessionContractVersions.Snapshot,
        [],
        [],
        [],
        []);

    public CharacterSuccessionWorldSnapshot Canonicalize() => this with
    {
        Designations = Designations.OrderBy(item => item.DesignationId).ToArray(),
        History = History.OrderBy(item => item.DesignatorCharacterId).ToArray(),
        Claims = Claims.OrderBy(item => item.ClaimId).ToArray(),
        ClaimHistory = ClaimHistory.OrderBy(item => item.SubjectCharacterId).ToArray(),
    };
}

public interface IAuthoritativeCharacterSuccessionWorldQuery
{
    IReadOnlyList<HeirDesignationState> Designations { get; }

    IReadOnlyList<HeirDesignationHistoryAggregate> History { get; }

    bool TryGetCurrentDesignation(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationState? designation);

    IReadOnlyList<HeirDesignationState> GetDesignationRecordsInvolving(
        EntityId characterId);

    bool TryGetHistory(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationHistoryAggregate? history);

    SuccessionCandidateEvaluationResult EvaluateCandidate(
        SuccessionCandidateEvaluationRequest request);

    SuccessionCandidateSetResult FindEligibleCandidates(
        SuccessionCandidateSetRequest request);

    bool TryGetActiveClaim(
        EntityId subjectCharacterId,
        EntityId claimantCharacterId,
        [NotNullWhen(true)] out SuccessionClaimState? claim);

    IReadOnlyList<SuccessionClaimState> GetActiveClaimsForSubject(
        EntityId subjectCharacterId);

    IReadOnlyList<SuccessionClaimState> GetRecentClaimRecordsForSubject(
        EntityId subjectCharacterId);

    bool TryGetClaimHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionClaimHistoryAggregate? history);
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(DesignateHeirAction), "designate_heir.v1")]
[JsonDerivedType(typeof(RevokeHeirDesignationAction), "revoke_heir_designation.v1")]
public interface ICharacterSuccessionAction;

public sealed record DesignateHeirAction(
    EntityId HeirCharacterId,
    EntityId? ExpectedCurrentDesignationId) : ICharacterSuccessionAction;

public sealed record RevokeHeirDesignationAction(
    EntityId ExpectedCurrentDesignationId) : ICharacterSuccessionAction;

[method: JsonConstructor]
public sealed record CharacterSuccessionActionCommandPayload(ICharacterSuccessionAction Action)
    : ICampaignCommandPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HeirDesignatedOutcome), "heir_designated.v1")]
[JsonDerivedType(typeof(HeirDesignationReplacedOutcome), "heir_designation_replaced.v1")]
[JsonDerivedType(typeof(HeirDesignationRevokedOutcome), "heir_designation_revoked.v1")]
public interface ICharacterSuccessionActionOutcome;

public sealed record HeirDesignatedOutcome(HeirDesignationState CurrentDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record HeirDesignationReplacedOutcome(
    HeirDesignationState PreviousDesignation,
    HeirDesignationState CurrentDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record HeirDesignationRevokedOutcome(HeirDesignationState PreviousDesignation)
    : ICharacterSuccessionActionOutcome;

public sealed record CharacterSuccessionActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterSuccessionAction Action,
    ICharacterSuccessionActionOutcome Outcome)
    : ICampaignEventPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AssertSuccessionClaimAction), "assert_succession_claim.v1")]
[JsonDerivedType(typeof(WithdrawSuccessionClaimAction), "withdraw_succession_claim.v1")]
public interface ICharacterSuccessionClaimAction;

public sealed record AssertSuccessionClaimAction(EntityId SubjectCharacterId)
    : ICharacterSuccessionClaimAction;

public sealed record WithdrawSuccessionClaimAction(
    EntityId SubjectCharacterId,
    EntityId ExpectedCurrentClaimId)
    : ICharacterSuccessionClaimAction;

[method: JsonConstructor]
public sealed record CharacterSuccessionClaimActionCommandPayload(
    ICharacterSuccessionClaimAction Action)
    : ICampaignCommandPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SuccessionClaimAssertedOutcome), "succession_claim_asserted.v1")]
[JsonDerivedType(typeof(SuccessionClaimWithdrawnOutcome), "succession_claim_withdrawn.v1")]
public interface ICharacterSuccessionClaimActionOutcome;

public sealed record SuccessionClaimAssertedOutcome(SuccessionClaimState CurrentClaim)
    : ICharacterSuccessionClaimActionOutcome;

public sealed record SuccessionClaimWithdrawnOutcome(SuccessionClaimState PreviousClaim)
    : ICharacterSuccessionClaimActionOutcome;

public sealed record CharacterSuccessionClaimActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterSuccessionClaimAction Action,
    ICharacterSuccessionClaimActionOutcome Outcome)
    : ICampaignEventPayload;

public static class CharacterSuccessionIds
{
    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-succession-action-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveClaimActionEventId(
        CampaignDate resolutionDate,
        EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-succession-claim-action-event.v1",
            StableId.FormatDate(resolutionDate),
            StableId.RequireId(commandId, nameof(commandId)).Value);

    public static EntityId DeriveDesignationId(
        EntityId eventId,
        EntityId designatorCharacterId,
        EntityId heirCharacterId) =>
        StableId.Hash(
            "heir_designation",
            "heir-designation.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(designatorCharacterId, nameof(designatorCharacterId)).Value,
            StableId.RequireId(heirCharacterId, nameof(heirCharacterId)).Value);

    public static EntityId DeriveClaimId(
        EntityId eventId,
        EntityId subjectCharacterId,
        EntityId claimantCharacterId) =>
        StableId.Hash(
            "succession_claim",
            "succession-claim.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(subjectCharacterId, nameof(subjectCharacterId)).Value,
            StableId.RequireId(claimantCharacterId, nameof(claimantCharacterId)).Value);
}
