using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterSuccessionContractVersions
{
    public const int Snapshot = 4;
    public const int State = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int ClaimState = 1;
    public const int ClaimHistory = 1;
    public const int ClaimAction = 1;
    public const int ClaimOutcome = 1;
    public const int SupportState = 1;
    public const int SupportHistory = 1;
    public const int SupportAction = 1;
    public const int SupportOutcome = 1;
    public const int CandidateEligibilityRule = 1;
    public const int CandidateEvaluation = 1;
    public const int CandidateSet = 1;
    public const int ResolutionRule = 1;
    public const int ResolutionCandidate = 1;
    public const int Resolution = 1;
    public const int ResolutionHistory = 1;
    public const int Inheritance = 1;
    public const int Regency = 1;
    public const int CampaignContinuity = 1;
    public const int AuthoritativeQuery = 6;
}

public static class CharacterSuccessionLimits
{
    public const int RecentTerminalDesignationsPerCharacter = 32;
    public const int MaximumActiveClaimsPerSubject = 64;
    public const int MaximumActiveClaimsPerClaimant = 64;
    public const int RecentWithdrawnClaimsPerSubject = 32;
    public const int MaximumActiveSupportsPerSubject = 64;
    public const int MaximumActiveSupportsPerSupporter = 64;
    public const int RecentTerminalSupportsPerSubject = 32;
    public const int MaximumEvaluatedDescendantGeneration = 64;
    public const int MaximumConfiguredMinimumCandidateAge = 100;
    public const int MaximumResolutionCandidates = 256;
    public const int MaximumDisputedCandidates = 32;
    public const int MaximumCollateralDistance = 16;
    public const int RecentSuccessionResolutions = 256;
}

public static class CharacterSuccessionSystem
{
    public const string SystemId = "simulation.character_succession";
    public const int Version = 4;
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

public enum SuccessionSupportStatus
{
    Active = 0,
    Replaced = 1,
    Withdrawn = 2,
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

public enum SuccessionLegalBasis
{
    ActiveDesignation = 0,
    BiologicalDescendant = 1,
    LegalAdoptiveDescendant = 2,
    UnspecifiedLegacyDescendant = 3,
    PrincipalSpouse = 4,
    BiologicalCollateral = 5,
    LegalAdoptiveCollateral = 6,
    UnspecifiedLegacyCollateral = 7,
}

public enum SuccessionContestResolutionMode
{
    ResolveByStableId = 0,
    RecordDispute = 1,
}

public enum SuccessionResolutionStatus
{
    Selected = 0,
    Disputed = 1,
    NoSuccessor = 2,
}

[Flags]
public enum SuccessionRegencyReason
{
    None = 0,
    Minor = 1 << 0,
    Incapacitated = 1 << 1,
}

public enum PlayerCampaignContinuityStatus
{
    Active = 0,
    ContinueWithoutControlledCharacter = 1,
    Ended = 2,
}

public enum SuccessionNoAcceptedSuccessorBehavior
{
    ContinueWithoutControlledCharacter = 0,
    EndCampaign = 1,
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

public sealed record SuccessionResolutionRule(
    int ContractVersion,
    SuccessionCandidateEligibilityRule CandidateEligibility,
    IReadOnlyList<SuccessionLegalBasis> LegalBasisPrecedence,
    bool IncludesPrincipalSpouse,
    IReadOnlyList<ParentChildLinkKind> AllowedCollateralKinds,
    int MaximumCollateralDistance,
    SuccessionContestResolutionMode ContestResolutionMode,
    int MaximumCandidates,
    int MaximumDisputedCandidates,
    bool CreatesRegencyForIncapacitatedSuccessor,
    SuccessionNoAcceptedSuccessorBehavior NoAcceptedSuccessorBehavior)
{
    public SuccessionResolutionRule Canonicalize() => this with
    {
        CandidateEligibility = CandidateEligibility?.Canonicalize()!,
        LegalBasisPrecedence = LegalBasisPrecedence is null
            ? null!
            : LegalBasisPrecedence.ToArray(),
        AllowedCollateralKinds = AllowedCollateralKinds is null
            ? null!
            : AllowedCollateralKinds.Order().ToArray(),
    };
}

public sealed record SuccessionLegalBasisEvidence(
    int ContractVersion,
    SuccessionLegalBasis Basis,
    int? DescendantGeneration,
    int? CollateralDistance,
    EntityId? SourceDesignationId,
    EntityId? SourceMarriageUnionId,
    EntityId? SharedAncestorCharacterId);

public sealed record SuccessionResolutionCandidate(
    int ContractVersion,
    EntityId CandidateCharacterId,
    int CandidateAge,
    CharacterConditionState CandidateCondition,
    IReadOnlyList<SuccessionLegalBasisEvidence> LegalBases,
    EntityId? ActiveClaimId,
    IReadOnlyList<EntityId> ActiveSupportIds,
    int LegalBasisPrecedenceIndex,
    int KinshipDistance)
{
    public SuccessionResolutionCandidate Canonicalize() => this with
    {
        LegalBases = LegalBases is null
            ? null!
            : LegalBases.OrderBy(item => item.Basis)
                .ThenBy(item => item.DescendantGeneration)
                .ThenBy(item => item.CollateralDistance)
                .ThenBy(item => item.SourceDesignationId)
                .ThenBy(item => item.SourceMarriageUnionId)
                .ThenBy(item => item.SharedAncestorCharacterId)
                .ToArray(),
        ActiveSupportIds = ActiveSupportIds is null
            ? null!
            : ActiveSupportIds.Order().ToArray(),
    };
}

public sealed record SuccessionEstateTransfer(
    int ContractVersion,
    EntityId EstateId,
    EntityId PreviousOwnerCharacterId,
    EntityId CurrentOwnerCharacterId);

public sealed record SuccessionInheritanceChange(
    int ContractVersion,
    WealthTransferredOutcome? WealthTransfer,
    IReadOnlyList<SuccessionEstateTransfer> EstateTransfers)
{
    public SuccessionInheritanceChange Canonicalize() => this with
    {
        EstateTransfers = EstateTransfers is null
            ? null!
            : EstateTransfers.OrderBy(item => item.EstateId).ToArray(),
    };
}

public sealed record SuccessionRegencyHook(
    int ContractVersion,
    EntityId SuccessorCharacterId,
    SuccessionRegencyReason Reasons,
    EntityId? RegentCharacterId,
    EntityId? SourceGuardianshipId,
    EntityId? SourceGuardianCharacterId,
    EntityId? SourceCustodianCharacterId);

public sealed record PlayerCampaignContinuityState(
    int ContractVersion,
    PlayerCampaignContinuityStatus Status,
    EntityId? ControlledCharacterId,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record SuccessionResolutionState(
    int ContractVersion,
    EntityId ResolutionId,
    EntityId SubjectCharacterId,
    EntityId DeathId,
    SuccessionResolutionStatus Status,
    SuccessionResolutionCandidate? SelectedCandidate,
    IReadOnlyList<SuccessionResolutionCandidate> DisputedCandidates,
    int EligibleCandidateCount,
    SuccessionResolutionRule Rule,
    SuccessionInheritanceChange Inheritance,
    SuccessionRegencyHook? Regency,
    PlayerCampaignContinuityState? PreviousCampaignContinuity,
    PlayerCampaignContinuityState? CurrentCampaignContinuity,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId)
{
    public SuccessionResolutionState Canonicalize() => this with
    {
        SelectedCandidate = SelectedCandidate?.Canonicalize(),
        DisputedCandidates = DisputedCandidates is null
            ? null!
            : DisputedCandidates.Select(item => item.Canonicalize())
                .OrderBy(item => item.CandidateCharacterId)
                .ToArray(),
        Rule = Rule?.Canonicalize()!,
        Inheritance = Inheritance?.Canonicalize()!,
    };
}

public sealed record SuccessionResolutionHistoryAggregate(
    int ContractVersion,
    long FoldedSelectedCount,
    long FoldedDisputedCount,
    long FoldedNoSuccessorCount,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate)
{
    public static SuccessionResolutionHistoryAggregate Empty { get; } = new(
        CharacterSuccessionContractVersions.ResolutionHistory,
        0,
        0,
        0,
        null,
        null);

    [JsonIgnore]
    public long TotalFoldedCount => checked(
        FoldedSelectedCount + FoldedDisputedCount + FoldedNoSuccessorCount);
}

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

public sealed record SuccessionSupportState(
    int ContractVersion,
    EntityId SupportId,
    EntityId SubjectId,
    EntityId SupporterId,
    EntityId SupportedCandidateId,
    CampaignDate DeclaredDate,
    long DeclaredTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId,
    SuccessionSupportStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId,
    EntityId? ResolutionEventId);

public sealed record SuccessionSupportHistoryAggregate(
    int ContractVersion,
    EntityId SubjectId,
    long FoldedReplacedCount,
    long FoldedWithdrawnCount,
    CampaignDate EarliestDate,
    CampaignDate LatestDate)
{
    [JsonIgnore]
    public long TotalFoldedCount => checked(FoldedReplacedCount + FoldedWithdrawnCount);
}

[method: JsonConstructor]
public sealed record CharacterSuccessionWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<HeirDesignationState> Designations,
    IReadOnlyList<HeirDesignationHistoryAggregate> History,
    IReadOnlyList<SuccessionClaimState> Claims,
    IReadOnlyList<SuccessionClaimHistoryAggregate> ClaimHistory,
    IReadOnlyList<SuccessionSupportState> Supports,
    IReadOnlyList<SuccessionSupportHistoryAggregate> SupportHistory,
    IReadOnlyList<SuccessionResolutionState> Resolutions,
    SuccessionResolutionHistoryAggregate ResolutionHistory,
    PlayerCampaignContinuityState? CampaignContinuity)
{
    public CharacterSuccessionWorldSnapshot(
        int contractVersion,
        IReadOnlyList<HeirDesignationState> designations,
        IReadOnlyList<HeirDesignationHistoryAggregate> history,
        IReadOnlyList<SuccessionClaimState> claims,
        IReadOnlyList<SuccessionClaimHistoryAggregate> claimHistory)
        : this(
            contractVersion,
            designations,
            history,
            claims,
            claimHistory,
            [],
            [],
            [],
            SuccessionResolutionHistoryAggregate.Empty,
            null)
    {
    }

    public CharacterSuccessionWorldSnapshot(
        int contractVersion,
        IReadOnlyList<HeirDesignationState> designations,
        IReadOnlyList<HeirDesignationHistoryAggregate> history,
        IReadOnlyList<SuccessionClaimState> claims,
        IReadOnlyList<SuccessionClaimHistoryAggregate> claimHistory,
        IReadOnlyList<SuccessionSupportState> supports,
        IReadOnlyList<SuccessionSupportHistoryAggregate> supportHistory)
        : this(
            contractVersion,
            designations,
            history,
            claims,
            claimHistory,
            supports,
            supportHistory,
            [],
            SuccessionResolutionHistoryAggregate.Empty,
            null)
    {
    }

    public static CharacterSuccessionWorldSnapshot Empty { get; } = new(
        CharacterSuccessionContractVersions.Snapshot,
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        SuccessionResolutionHistoryAggregate.Empty,
        null);

    public CharacterSuccessionWorldSnapshot Canonicalize() => this with
    {
        Designations = Designations.OrderBy(item => item.DesignationId).ToArray(),
        History = History.OrderBy(item => item.DesignatorCharacterId).ToArray(),
        Claims = Claims.OrderBy(item => item.ClaimId).ToArray(),
        ClaimHistory = ClaimHistory.OrderBy(item => item.SubjectCharacterId).ToArray(),
        Supports = Supports.OrderBy(item => item.SupportId).ToArray(),
        SupportHistory = SupportHistory.OrderBy(item => item.SubjectId).ToArray(),
        Resolutions = Resolutions.OrderBy(item => item.ResolutionTurnIndex)
            .ThenBy(item => item.ResolutionDate)
            .ThenBy(item => item.ResolutionId)
            .Select(item => item.Canonicalize())
            .ToArray(),
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

    bool TryGetCurrentSupport(
        EntityId subjectId,
        EntityId supporterId,
        [NotNullWhen(true)] out SuccessionSupportState? support);

    IReadOnlyList<SuccessionSupportState> GetActiveSupportsForSubject(
        EntityId subjectId);

    IReadOnlyList<SuccessionSupportState> GetActiveSupportsForCandidate(
        EntityId subjectId,
        EntityId supportedCandidateId);

    IReadOnlyList<SuccessionSupportState> GetRecentSupportRecordsForSubject(
        EntityId subjectId);

    bool TryGetSupportHistory(
        EntityId subjectId,
        [NotNullWhen(true)] out SuccessionSupportHistoryAggregate? history);

    IReadOnlyList<SuccessionResolutionState> Resolutions { get; }

    SuccessionResolutionHistoryAggregate ResolutionHistory { get; }

    PlayerCampaignContinuityState? CampaignContinuity { get; }

    bool TryGetResolutionForSubject(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionResolutionState? resolution);
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

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(DeclareSuccessionSupportAction), "declare_succession_support.v1")]
[JsonDerivedType(typeof(WithdrawSuccessionSupportAction), "withdraw_succession_support.v1")]
public interface ICharacterSuccessionSupportAction;

public sealed record DeclareSuccessionSupportAction(
    EntityId SubjectId,
    EntityId SupportedCandidateId,
    EntityId? ExpectedCurrentSupportId)
    : ICharacterSuccessionSupportAction;

public sealed record WithdrawSuccessionSupportAction(
    EntityId SubjectId,
    EntityId ExpectedCurrentSupportId)
    : ICharacterSuccessionSupportAction;

[method: JsonConstructor]
public sealed record CharacterSuccessionSupportActionCommandPayload(
    ICharacterSuccessionSupportAction Action)
    : ICampaignCommandPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SuccessionSupportDeclaredOutcome), "succession_support_declared.v1")]
[JsonDerivedType(typeof(SuccessionSupportReplacedOutcome), "succession_support_replaced.v1")]
[JsonDerivedType(typeof(SuccessionSupportWithdrawnOutcome), "succession_support_withdrawn.v1")]
public interface ICharacterSuccessionSupportActionOutcome;

public sealed record SuccessionSupportDeclaredOutcome(
    SuccessionSupportState CurrentSupport)
    : ICharacterSuccessionSupportActionOutcome;

public sealed record SuccessionSupportReplacedOutcome(
    SuccessionSupportState PreviousSupport,
    SuccessionSupportState CurrentSupport)
    : ICharacterSuccessionSupportActionOutcome;

public sealed record SuccessionSupportWithdrawnOutcome(
    SuccessionSupportState PreviousSupport)
    : ICharacterSuccessionSupportActionOutcome;

public sealed record CharacterSuccessionSupportActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterSuccessionSupportAction Action,
    ICharacterSuccessionSupportActionOutcome Outcome)
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

    public static EntityId DeriveSupportActionEventId(
        CampaignDate resolutionDate,
        EntityId commandId) =>
        StableId.Hash(
            "event",
            "character-succession-support-action-event.v1",
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

    public static EntityId DeriveSupportId(
        EntityId eventId,
        EntityId subjectId,
        EntityId supporterId,
        EntityId supportedCandidateId) =>
        StableId.Hash(
            "succession_support",
            "succession-support.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(subjectId, nameof(subjectId)).Value,
            StableId.RequireId(supporterId, nameof(supporterId)).Value,
            StableId.RequireId(
                supportedCandidateId,
                nameof(supportedCandidateId)).Value);

    public static EntityId DeriveResolutionId(
        EntityId eventId,
        EntityId subjectCharacterId) =>
        StableId.Hash(
            "succession_resolution",
            "succession-resolution.v1",
            StableId.RequireId(eventId, nameof(eventId)).Value,
            StableId.RequireId(subjectCharacterId, nameof(subjectCharacterId)).Value);

    public static EntityId DeriveResolutionStateId(params string[] canonicalFields) =>
        StableId.Hash(
            "succession_resolution_state",
            "succession-resolution-state.v1",
            canonicalFields);
}
