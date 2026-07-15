using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterMarriageContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int Practice = 1;
    public const int Eligibility = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterMarriageLimits
{
    public const int ActiveProposalsPerRecipient = 8;
    public const int RetainedRecordsPerCategoryPerCharacter = 64;
    public const int ActiveLegalRelationshipsPerCharacter = 64;
    public const int MaximumPrincipalSpouseLimit = 8;
    public const int MaximumConcubinageLimit = 64;
    public const int MinimumAdultAge = 18;
    public const int MaximumConfiguredMinimumAge = 100;
    public const int MaximumRomanceProgressLevel = 4;
}

public static class CharacterMarriageSystem
{
    public const string SystemId = "simulation.character_marriages";
    public const int Version = 1;
}

[Flags]
public enum MarriageProhibitedKinship
{
    None = 0,
    DirectLine = 1 << 0,
    Siblings = 1 << 1,
}

public enum MarriageProposalKind
{
    LegalUnion = 0,
    PoliticalBetrothal = 1,
}

public enum MarriageBasis
{
    Political = 0,
    Romantic = 1,
}

public enum MarriageUnionForm
{
    PrincipalSpouse = 0,
    Concubinage = 1,
}

public enum MarriageConsentKind
{
    Voluntary = 0,
    PoliticalArrangement = 1,
    Coerced = 2,
}

public enum MarriageProposalStatus
{
    Active = 0,
    Accepted = 1,
    Refused = 2,
    Withdrawn = 3,
    Cancelled = 4,
    Invalidated = 5,
}

public enum MarriageProposalResponse
{
    Accept = 0,
    Refuse = 1,
}

public enum PoliticalBetrothalStatus
{
    Active = 0,
    Fulfilled = 1,
    Released = 2,
    Cancelled = 3,
    Invalidated = 4,
}

public enum MarriageUnionStatus
{
    Active = 0,
    Ended = 1,
}

public enum MarriageUnionEndReason
{
    SpouseDied = 0,
    Annulled = 1,
    Separated = 2,
    PoliticalDissolution = 3,
}

public enum RomanceRouteStatus
{
    Active = 0,
    Completed = 1,
    Ended = 2,
    Invalidated = 3,
}

public enum MarriageEligibilityCategory
{
    VoluntaryLegalUnion = 0,
    PoliticalBetrothal = 1,
    VoluntaryRomance = 2,
    CoercivePoliticalAction = 3,
}

public enum MarriageEligibilityReason
{
    InvalidDate = 0,
    InvalidParticipant = 1,
    UnknownParticipant = 2,
    SameParticipant = 3,
    NotBorn = 4,
    Dead = 5,
    BelowMinimumAge = 6,
    Incapacitated = 7,
    InCustody = 8,
    UnknownPractice = 9,
    UnsupportedUnionForm = 10,
    InvalidConcubinagePrincipal = 11,
    ProhibitedDirectLineKinship = 12,
    ProhibitedSiblingKinship = 13,
    PoliticalBetrothalDisabled = 14,
    WidowRemarriageDisabled = 15,
    ActiveUnionLimitReached = 16,
    DuplicateActiveRelationship = 17,
}

public sealed record MarriagePracticeState(
    int ContractVersion,
    EntityId PracticeId,
    int MinimumLegalUnionAge,
    int MinimumRomanceAge,
    int MaximumActivePrincipalSpousesPerCharacter,
    int MaximumActiveConcubinageUnionsPerPrincipal,
    int MaximumActiveConcubinageUnionsPerPartner,
    bool AllowsPoliticalBetrothalBeforeLegalAge,
    bool AllowsWidowRemarriage,
    MarriageProhibitedKinship ProhibitedKinship);

public sealed record MarriageProposalState(
    int ContractVersion,
    EntityId ProposalId,
    MarriageProposalKind Kind,
    MarriageBasis Basis,
    MarriageUnionForm ProposedForm,
    MarriageConsentKind ConsentKind,
    EntityId ProposerCharacterId,
    EntityId RecipientCharacterId,
    EntityId? ConcubinagePrincipalCharacterId,
    EntityId PracticeId,
    CampaignDate CreatedDate,
    long CreatedTurnIndex,
    EntityId SourceCommandId,
    MarriageProposalStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId);

public sealed record PoliticalBetrothalState(
    int ContractVersion,
    EntityId BetrothalId,
    EntityId FirstCharacterId,
    EntityId SecondCharacterId,
    MarriageUnionForm IntendedForm,
    EntityId? ConcubinagePrincipalCharacterId,
    EntityId PracticeId,
    EntityId SourceProposalId,
    CampaignDate StartDate,
    long StartTurnIndex,
    PoliticalBetrothalStatus Status,
    EntityId? FulfillmentUnionId,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId);

public sealed record MarriageUnionState(
    int ContractVersion,
    EntityId UnionId,
    EntityId FirstCharacterId,
    EntityId SecondCharacterId,
    MarriageUnionForm Form,
    EntityId? ConcubinagePrincipalCharacterId,
    MarriageBasis Basis,
    MarriageConsentKind ConsentKind,
    EntityId PracticeId,
    EntityId SourceProposalId,
    CampaignDate StartDate,
    long StartTurnIndex,
    MarriageUnionStatus Status,
    CampaignDate? EndDate,
    long? EndTurnIndex,
    EntityId? EndCommandId,
    MarriageUnionEndReason? EndReason)
{
    [JsonIgnore]
    public bool IsActive => Status == MarriageUnionStatus.Active;
}

public sealed record RomanceRouteState(
    int ContractVersion,
    EntityId RouteId,
    EntityId FirstCharacterId,
    EntityId SecondCharacterId,
    EntityId PracticeId,
    int ProgressLevel,
    CampaignDate StartDate,
    long StartTurnIndex,
    EntityId SourceCommandId,
    RomanceRouteStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId)
{
    [JsonIgnore]
    public bool IsActive => Status == RomanceRouteStatus.Active;
}

public sealed record CharacterMarriageHistoryAggregate(
    int ContractVersion,
    EntityId CharacterId,
    long FoldedProposalCount,
    long FoldedBetrothalCount,
    long FoldedUnionCount,
    long FoldedRomanceRouteCount,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate)
{
    public static CharacterMarriageHistoryAggregate Empty(EntityId characterId) => new(
        CharacterMarriageContractVersions.State,
        characterId,
        0,
        0,
        0,
        0,
        null,
        null);
}

public sealed record MarriageEligibilityRequest(
    int ContractVersion,
    MarriageEligibilityCategory Category,
    EntityId FirstCharacterId,
    EntityId SecondCharacterId,
    EntityId PracticeId,
    MarriageUnionForm? ProposedForm,
    EntityId? ConcubinagePrincipalCharacterId);

public sealed record MarriageEligibilityIssue(MarriageEligibilityReason Reason);

public sealed record MarriageEligibilityResult(
    int ContractVersion,
    bool IsEligible,
    IReadOnlyList<MarriageEligibilityIssue> Issues);

public sealed record CharacterMarriageWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<MarriagePracticeState> Practices,
    IReadOnlyList<MarriageProposalState> Proposals,
    IReadOnlyList<PoliticalBetrothalState> Betrothals,
    IReadOnlyList<MarriageUnionState> Unions,
    IReadOnlyList<RomanceRouteState> RomanceRoutes,
    IReadOnlyList<CharacterMarriageHistoryAggregate> History)
{
    public static CharacterMarriageWorldSnapshot Empty { get; } = new(
        CharacterMarriageContractVersions.Snapshot,
        [],
        [],
        [],
        [],
        [],
        []);

    public CharacterMarriageWorldSnapshot Canonicalize() => this with
    {
        Practices = Practices.OrderBy(item => item.PracticeId).ToArray(),
        Proposals = Proposals.OrderBy(item => item.ProposalId).ToArray(),
        Betrothals = Betrothals.OrderBy(item => item.BetrothalId).ToArray(),
        Unions = Unions.OrderBy(item => item.UnionId).ToArray(),
        RomanceRoutes = RomanceRoutes.OrderBy(item => item.RouteId).ToArray(),
        History = History.OrderBy(item => item.CharacterId).ToArray(),
    };
}

public interface IAuthoritativeCharacterMarriageWorldQuery
{
    IReadOnlyList<MarriagePracticeState> Practices { get; }

    IReadOnlyList<MarriageProposalState> Proposals { get; }

    IReadOnlyList<PoliticalBetrothalState> Betrothals { get; }

    IReadOnlyList<MarriageUnionState> Unions { get; }

    IReadOnlyList<RomanceRouteState> RomanceRoutes { get; }

    IReadOnlyList<CharacterMarriageHistoryAggregate> History { get; }

    bool TryGetPractice(EntityId practiceId, [NotNullWhen(true)] out MarriagePracticeState? practice);

    bool TryGetProposal(EntityId proposalId, [NotNullWhen(true)] out MarriageProposalState? proposal);

    bool TryGetBetrothal(EntityId betrothalId, [NotNullWhen(true)] out PoliticalBetrothalState? betrothal);

    bool TryGetUnion(EntityId unionId, [NotNullWhen(true)] out MarriageUnionState? union);

    bool TryGetRomanceRoute(EntityId routeId, [NotNullWhen(true)] out RomanceRouteState? route);

    bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterMarriageHistoryAggregate? history);

    IReadOnlyList<MarriageProposalState> GetProposalsInvolving(EntityId characterId);

    IReadOnlyList<PoliticalBetrothalState> GetBetrothalsInvolving(EntityId characterId);

    IReadOnlyList<MarriageUnionState> GetUnionsInvolving(EntityId characterId);

    IReadOnlyList<RomanceRouteState> GetRomanceRoutesInvolving(EntityId characterId);

    MarriageEligibilityResult EvaluateEligibility(
        MarriageEligibilityRequest request,
        CampaignDate date);
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ProposePoliticalMarriageAction), "propose_political_marriage.v1")]
[JsonDerivedType(typeof(RespondToPoliticalMarriageProposalAction), "respond_political_marriage_proposal.v1")]
[JsonDerivedType(typeof(WithdrawPoliticalMarriageProposalAction), "withdraw_political_marriage_proposal.v1")]
[JsonDerivedType(typeof(CancelPoliticalBetrothalAction), "cancel_political_betrothal.v1")]
[JsonDerivedType(typeof(FulfillPoliticalBetrothalAction), "fulfill_political_betrothal.v1")]
public interface ICharacterMarriageAction;

public sealed record ProposePoliticalMarriageAction(
    EntityId RecipientCharacterId,
    MarriageProposalKind Kind,
    MarriageUnionForm ProposedForm,
    EntityId? ConcubinagePrincipalCharacterId,
    EntityId PracticeId) : ICharacterMarriageAction;

public sealed record RespondToPoliticalMarriageProposalAction(
    EntityId ProposalId,
    MarriageProposalResponse Response) : ICharacterMarriageAction;

public sealed record WithdrawPoliticalMarriageProposalAction(EntityId ProposalId)
    : ICharacterMarriageAction;

public sealed record CancelPoliticalBetrothalAction(EntityId BetrothalId)
    : ICharacterMarriageAction;

public sealed record FulfillPoliticalBetrothalAction(EntityId BetrothalId)
    : ICharacterMarriageAction;

[method: JsonConstructor]
public sealed record CharacterMarriageActionCommandPayload(ICharacterMarriageAction Action)
    : ICampaignCommandPayload;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(MarriageProposalCreatedOutcome), "marriage_proposal_created.v1")]
[JsonDerivedType(typeof(MarriageProposalRefusedOutcome), "marriage_proposal_refused.v1")]
[JsonDerivedType(typeof(MarriageProposalWithdrawnOutcome), "marriage_proposal_withdrawn.v1")]
[JsonDerivedType(typeof(MarriageProposalCancelledOutcome), "marriage_proposal_cancelled.v1")]
[JsonDerivedType(typeof(PoliticalBetrothalAcceptedOutcome), "political_betrothal_accepted.v1")]
[JsonDerivedType(typeof(DirectPoliticalUnionAcceptedOutcome), "direct_political_union_accepted.v1")]
[JsonDerivedType(typeof(PoliticalBetrothalCancelledOutcome), "political_betrothal_cancelled.v1")]
[JsonDerivedType(typeof(PoliticalBetrothalFulfilledOutcome), "political_betrothal_fulfilled.v1")]
public interface ICharacterMarriageActionOutcome;

public sealed record MarriageProposalCreatedOutcome(MarriageProposalState Proposal)
    : ICharacterMarriageActionOutcome;

public sealed record MarriageProposalRefusedOutcome(MarriageProposalState Proposal)
    : ICharacterMarriageActionOutcome;

public sealed record MarriageProposalWithdrawnOutcome(MarriageProposalState Proposal)
    : ICharacterMarriageActionOutcome;

public sealed record MarriageProposalCancelledOutcome(MarriageProposalState Proposal)
    : ICharacterMarriageActionOutcome;

public sealed record PoliticalBetrothalAcceptedOutcome(
    MarriageProposalState Proposal,
    PoliticalBetrothalState Betrothal) : ICharacterMarriageActionOutcome;

public sealed record DirectPoliticalUnionAcceptedOutcome(
    MarriageProposalState Proposal,
    MarriageUnionState Union) : ICharacterMarriageActionOutcome;

public sealed record PoliticalBetrothalCancelledOutcome(PoliticalBetrothalState Betrothal)
    : ICharacterMarriageActionOutcome;

public sealed record PoliticalBetrothalFulfilledOutcome(
    PoliticalBetrothalState Betrothal,
    MarriageProposalState FulfillmentProposal,
    MarriageUnionState Union) : ICharacterMarriageActionOutcome;

public sealed record CharacterMarriageActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterMarriageAction Action,
    ICharacterMarriageActionOutcome Outcome) : ICampaignEventPayload;

public static class CharacterMarriageIds
{
    public static EntityId DeriveActionEventId(
        CampaignDate resolutionDate,
        EntityId commandId)
    {
        RequireDate(resolutionDate, nameof(resolutionDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "event",
            "character-marriage-action-event.v1",
            FormatDate(resolutionDate),
            commandId.Value);
    }

    public static EntityId DeriveProposalId(
        MarriageProposalKind kind,
        CampaignDate createdDate,
        EntityId commandId)
    {
        RequireDefined(kind, nameof(kind));
        RequireDate(createdDate, nameof(createdDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "marriage_proposal",
            "character-marriage-proposal.v1",
            ((int)kind).ToString(CultureInfo.InvariantCulture),
            FormatDate(createdDate),
            commandId.Value);
    }

    public static EntityId DerivePoliticalBetrothalId(EntityId sourceProposalId)
    {
        RequireId(sourceProposalId, nameof(sourceProposalId));
        return Hash(
            "political_betrothal",
            "political-betrothal.v1",
            sourceProposalId.Value);
    }

    public static EntityId DeriveMarriageUnionId(EntityId sourceProposalId)
    {
        RequireId(sourceProposalId, nameof(sourceProposalId));
        return Hash(
            "marriage_union",
            "marriage-union.v1",
            sourceProposalId.Value);
    }

    private static EntityId Hash(string entityNamespace, string domain, params string[] fields)
    {
        StringBuilder canonical = new();
        AppendField(canonical, domain);
        foreach (string field in fields)
        {
            AppendField(canonical, field);
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return new EntityId($"{entityNamespace}:sha256/{Convert.ToHexStringLower(digest)}");
    }

    private static void AppendField(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
        target.Append(';');
    }

    private static string FormatDate(CampaignDate value) => string.Concat(
        value.Year.ToString("D4", CultureInfo.InvariantCulture),
        "-",
        value.Month.ToString("D2", CultureInfo.InvariantCulture),
        "-",
        value.Day.ToString("D2", CultureInfo.InvariantCulture));

    private static void RequireId(EntityId value, string parameterName)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid stable ID is required.", parameterName);
        }
    }

    private static void RequireDate(CampaignDate value, string parameterName)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid campaign date is required.", parameterName);
        }
    }

    private static void RequireDefined<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
