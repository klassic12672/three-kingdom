using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterMarriageContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int Practice = 1;
    public const int Eligibility = 1;
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
