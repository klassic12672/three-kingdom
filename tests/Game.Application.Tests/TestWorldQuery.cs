using System.Diagnostics.CodeAnalysis;
using Simulation.Core;

namespace Game.Application.Tests;

internal sealed class TestWorldQuery : IWorldQuery
{
    public CampaignCalendar Calendar { get; init; } =
        new(new CampaignDate(200, 1, 1), 0);

    public IReadOnlyList<SyntheticEntitySnapshot> Entities { get; init; } = [];

    public TestCharacterQuery CharacterData { get; } = new();

    public TestRelationshipQuery RelationshipData { get; } = new();

    public TestMarriageQuery MarriageData { get; } = new();

    public TestSuccessionQuery SuccessionData { get; } = new();

    public IGeographicWorldQuery Geography => null!;

    public IAuthoritativeCharacterWorldQuery Characters => CharacterData;

    public IAuthoritativeRelationshipWorldQuery Relationships => RelationshipData;

    public IAuthoritativeCareerWorldQuery Careers => null!;

    public IAuthoritativeCharacterResourceWorldQuery CharacterResources => null!;

    public IAuthoritativeCharacterEstateHoldingWorldQuery CharacterEstateHoldings => null!;

    public IAuthoritativeCharacterMarriageWorldQuery CharacterMarriages => MarriageData;

    public IAuthoritativeCharacterGuardianshipWorldQuery CharacterGuardianships => null!;

    public IAuthoritativeCharacterPregnancyWorldQuery CharacterPregnancies => null!;

    public IAuthoritativeCharacterSuccessionWorldQuery CharacterSuccessions => SuccessionData;

    public bool TryGetEntity(
        EntityId id,
        [NotNullWhen(true)] out SyntheticEntitySnapshot? entity)
    {
        entity = Entities.SingleOrDefault(item => item.Id == id);
        return entity is not null;
    }
}

internal sealed class TestCharacterQuery : IAuthoritativeCharacterWorldQuery
{
    private readonly SortedDictionary<EntityId, AuthoritativeCharacterProfile> profiles = [];
    private readonly SortedDictionary<EntityId, AuthoritativeHouseholdView> households = [];

    public IReadOnlyList<AuthoritativeCharacterProfile> Profiles =>
        profiles.Values.ToArray();

    public IReadOnlyList<AuthoritativeHouseholdView> Households =>
        households.Values.ToArray();

    public void Add(AuthoritativeCharacterProfile profile) =>
        profiles.Add(profile.CharacterId, profile);

    public void Add(AuthoritativeHouseholdView household) =>
        households.Add(household.HouseholdId, household);

    public bool TryGetCharacterProfile(
        EntityId id,
        [NotNullWhen(true)] out AuthoritativeCharacterProfile? profile) =>
        profiles.TryGetValue(id, out profile);

    public bool TryGetHousehold(
        EntityId id,
        [NotNullWhen(true)] out AuthoritativeHouseholdView? household) =>
        households.TryGetValue(id, out household);
}

internal sealed class TestRelationshipQuery : IAuthoritativeRelationshipWorldQuery
{
    private readonly SortedDictionary<EntityId, SubjectRelationshipHistory> subjects = [];

    public IReadOnlyList<SubjectRelationshipHistory> Subjects =>
        subjects.Values.ToArray();

    public void Add(SubjectRelationshipHistory history) =>
        subjects.Add(history.SubjectCharacterId, history);

    public bool TryGetSubjectHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SubjectRelationshipHistory? history) =>
        subjects.TryGetValue(subjectCharacterId, out history);
}

internal sealed class TestMarriageQuery : IAuthoritativeCharacterMarriageWorldQuery
{
    public IReadOnlyList<MarriagePracticeState> Practices { get; init; } = [];

    public IReadOnlyList<MarriageProposalState> Proposals { get; init; } = [];

    public IReadOnlyList<PoliticalBetrothalState> Betrothals { get; set; } = [];

    public IReadOnlyList<MarriageUnionState> Unions { get; set; } = [];

    public IReadOnlyList<RomanceRouteState> RomanceRoutes { get; set; } = [];

    public IReadOnlyList<RomanceInvitationState> RomanceInvitations { get; init; } = [];

    public IReadOnlyList<CharacterMarriageHistoryAggregate> History { get; init; } = [];

    public bool TryGetPractice(
        EntityId practiceId,
        [NotNullWhen(true)] out MarriagePracticeState? practice) =>
        TryGet(Practices, item => item.PracticeId == practiceId, out practice);

    public bool TryGetProposal(
        EntityId proposalId,
        [NotNullWhen(true)] out MarriageProposalState? proposal) =>
        TryGet(Proposals, item => item.ProposalId == proposalId, out proposal);

    public bool TryGetBetrothal(
        EntityId betrothalId,
        [NotNullWhen(true)] out PoliticalBetrothalState? betrothal) =>
        TryGet(Betrothals, item => item.BetrothalId == betrothalId, out betrothal);

    public bool TryGetUnion(
        EntityId unionId,
        [NotNullWhen(true)] out MarriageUnionState? union) =>
        TryGet(Unions, item => item.UnionId == unionId, out union);

    public bool TryGetRomanceRoute(
        EntityId routeId,
        [NotNullWhen(true)] out RomanceRouteState? route) =>
        TryGet(RomanceRoutes, item => item.RouteId == routeId, out route);

    public bool TryGetRomanceInvitation(
        EntityId invitationId,
        [NotNullWhen(true)] out RomanceInvitationState? invitation) =>
        TryGet(
            RomanceInvitations,
            item => item.InvitationId == invitationId,
            out invitation);

    public bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterMarriageHistoryAggregate? history) =>
        TryGet(History, item => item.CharacterId == characterId, out history);

    public IReadOnlyList<MarriageProposalState> GetProposalsInvolving(
        EntityId characterId) => Proposals.Where(item =>
            item.ProposerCharacterId == characterId
            || item.RecipientCharacterId == characterId).ToArray();

    public IReadOnlyList<PoliticalBetrothalState> GetBetrothalsInvolving(
        EntityId characterId) => Betrothals.Where(item =>
            item.FirstCharacterId == characterId
            || item.SecondCharacterId == characterId).ToArray();

    public IReadOnlyList<MarriageUnionState> GetUnionsInvolving(
        EntityId characterId) => Unions.Where(item =>
            item.FirstCharacterId == characterId
            || item.SecondCharacterId == characterId).ToArray();

    public IReadOnlyList<RomanceRouteState> GetRomanceRoutesInvolving(
        EntityId characterId) => RomanceRoutes.Where(item =>
            item.FirstCharacterId == characterId
            || item.SecondCharacterId == characterId).ToArray();

    public IReadOnlyList<RomanceInvitationState> GetRomanceInvitationsInvolving(
        EntityId characterId) => RomanceInvitations.Where(item =>
            item.InitiatorCharacterId == characterId
            || item.RecipientCharacterId == characterId).ToArray();

    public MarriageEligibilityResult EvaluateEligibility(
        MarriageEligibilityRequest request,
        CampaignDate date) => throw new NotSupportedException();

    private static bool TryGet<T>(
        IReadOnlyList<T> values,
        Func<T, bool> predicate,
        [NotNullWhen(true)] out T? value)
        where T : class
    {
        value = values.SingleOrDefault(predicate);
        return value is not null;
    }
}

internal sealed class TestSuccessionQuery : IAuthoritativeCharacterSuccessionWorldQuery
{
    public IReadOnlyList<HeirDesignationState> Designations { get; set; } = [];

    public IReadOnlyList<HeirDesignationHistoryAggregate> History { get; init; } = [];

    public IReadOnlyList<SuccessionClaimState> Claims { get; set; } = [];

    public IReadOnlyList<SuccessionClaimHistoryAggregate> ClaimHistory { get; init; } = [];

    public IReadOnlyList<SuccessionSupportState> Supports { get; set; } = [];

    public IReadOnlyList<SuccessionSupportHistoryAggregate> SupportHistory { get; init; } = [];

    public IReadOnlyList<SuccessionResolutionState> Resolutions { get; set; } = [];

    public SuccessionResolutionHistoryAggregate ResolutionHistory { get; init; } =
        SuccessionResolutionHistoryAggregate.Empty;

    public PlayerCampaignContinuityState? CampaignContinuity { get; set; }

    public bool TryGetCurrentDesignation(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationState? designation)
    {
        designation = Designations.SingleOrDefault(item =>
            item.DesignatorCharacterId == designatorCharacterId
            && item.Status == HeirDesignationStatus.Active);
        return designation is not null;
    }

    public IReadOnlyList<HeirDesignationState> GetDesignationRecordsInvolving(
        EntityId characterId) => Designations.Where(item =>
            item.DesignatorCharacterId == characterId
            || item.HeirCharacterId == characterId).ToArray();

    public bool TryGetHistory(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationHistoryAggregate? history)
    {
        history = History.SingleOrDefault(item =>
            item.DesignatorCharacterId == designatorCharacterId);
        return history is not null;
    }

    public SuccessionCandidateEvaluationResult EvaluateCandidate(
        SuccessionCandidateEvaluationRequest request) =>
        throw new NotSupportedException();

    public SuccessionCandidateSetResult FindEligibleCandidates(
        SuccessionCandidateSetRequest request) =>
        throw new NotSupportedException();

    public bool TryGetActiveClaim(
        EntityId subjectCharacterId,
        EntityId claimantCharacterId,
        [NotNullWhen(true)] out SuccessionClaimState? claim)
    {
        claim = Claims.SingleOrDefault(item =>
            item.SubjectCharacterId == subjectCharacterId
            && item.ClaimantCharacterId == claimantCharacterId
            && item.Status == SuccessionClaimStatus.Active);
        return claim is not null;
    }

    public IReadOnlyList<SuccessionClaimState> GetActiveClaimsForSubject(
        EntityId subjectCharacterId) => Claims.Where(item =>
            item.SubjectCharacterId == subjectCharacterId
            && item.Status == SuccessionClaimStatus.Active).ToArray();

    public IReadOnlyList<SuccessionClaimState> GetRecentClaimRecordsForSubject(
        EntityId subjectCharacterId) => Claims.Where(item =>
            item.SubjectCharacterId == subjectCharacterId).ToArray();

    public bool TryGetClaimHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionClaimHistoryAggregate? history)
    {
        history = ClaimHistory.SingleOrDefault(item =>
            item.SubjectCharacterId == subjectCharacterId);
        return history is not null;
    }

    public bool TryGetCurrentSupport(
        EntityId subjectId,
        EntityId supporterId,
        [NotNullWhen(true)] out SuccessionSupportState? support)
    {
        support = Supports.SingleOrDefault(item =>
            item.SubjectId == subjectId
            && item.SupporterId == supporterId
            && item.Status == SuccessionSupportStatus.Active);
        return support is not null;
    }

    public IReadOnlyList<SuccessionSupportState> GetActiveSupportsForSubject(
        EntityId subjectId) => Supports.Where(item =>
            item.SubjectId == subjectId
            && item.Status == SuccessionSupportStatus.Active).ToArray();

    public IReadOnlyList<SuccessionSupportState> GetActiveSupportsForCandidate(
        EntityId subjectId,
        EntityId supportedCandidateId) => Supports.Where(item =>
            item.SubjectId == subjectId
            && item.SupportedCandidateId == supportedCandidateId
            && item.Status == SuccessionSupportStatus.Active).ToArray();

    public IReadOnlyList<SuccessionSupportState> GetRecentSupportRecordsForSubject(
        EntityId subjectId) => Supports.Where(item =>
            item.SubjectId == subjectId).ToArray();

    public bool TryGetSupportHistory(
        EntityId subjectId,
        [NotNullWhen(true)] out SuccessionSupportHistoryAggregate? history)
    {
        history = SupportHistory.SingleOrDefault(item =>
            item.SubjectId == subjectId);
        return history is not null;
    }

    public bool TryGetResolutionForSubject(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionResolutionState? resolution)
    {
        resolution = Resolutions
            .Where(item => item.SubjectCharacterId == subjectCharacterId)
            .OrderByDescending(item => item.ResolutionTurnIndex)
            .ThenByDescending(item => item.ResolutionDate)
            .ThenByDescending(item => item.ResolutionId)
            .FirstOrDefault();
        return resolution is not null;
    }
}
