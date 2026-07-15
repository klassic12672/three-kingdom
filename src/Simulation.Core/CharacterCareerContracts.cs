using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CareerContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int RelationshipConsequence = RelationshipContractVersions.Consequence;
    public const int AuthoritativeQuery = 1;
    public const int DeathChange = 1;
}

public static class CareerLimits
{
    public const int ActiveProposalsPerRecipient = 8;
    public const int ActiveMembershipsPerRetinue = 64;
    public const int ActivePatronageBondsPerCharacter = 16;
    public const int ActiveEmploymentTenuresPerEmployee = 8;
    public const int CompletedRecordsPerCategoryPerCharacter = 64;
    public const int RecommendationsPerCharacter = 64;
    public const int RelationshipConsequencesPerAction = 64;
    public const int RelationshipWitnesses = 32;
}

public enum ServicePrincipalKind
{
    Character = 0,
    Household = 1,
}

public sealed record ServicePrincipalReference(
    ServicePrincipalKind Kind,
    EntityId PrincipalId);

public enum CareerProposalKind
{
    RetinueInvitation = 0,
    PatronageOffer = 1,
    EmploymentOffer = 2,
}

public enum CareerProposalStatus
{
    Active = 0,
    Accepted = 1,
    Refused = 2,
    Withdrawn = 3,
    Invalidated = 4,
}

public enum CareerProposalResponse
{
    Accept = 0,
    Refuse = 1,
}

public enum CareerServiceEndReason
{
    MemberLeft = 0,
    PatronEnded = 1,
    BeneficiaryEnded = 2,
    EmployeeLeft = 3,
    EmployerEnded = 4,
    LeaderDied = 5,
    MemberDied = 6,
    PatronDied = 7,
    BeneficiaryDied = 8,
    EmployeeDied = 9,
    EmployerDied = 10,
}

public sealed record CareerProposalState(
    int ContractVersion,
    EntityId ProposalId,
    CareerProposalKind Kind,
    EntityId ProposerCharacterId,
    EntityId RecipientCharacterId,
    ServicePrincipalReference Principal,
    EntityId? ProposedRoleId,
    CampaignDate CreatedDate,
    long CreatedTurnIndex,
    EntityId SourceCommandId,
    CareerProposalStatus Status,
    CampaignDate? ResolutionDate,
    long? ResolutionTurnIndex,
    EntityId? ResolutionCommandId);

public sealed record RetinueState(
    int ContractVersion,
    EntityId RetinueId,
    EntityId LeaderCharacterId);

public sealed record RetinueMembershipState(
    int ContractVersion,
    EntityId MembershipId,
    EntityId RetinueId,
    EntityId LeaderCharacterId,
    EntityId MemberCharacterId,
    EntityId SourceProposalId,
    CampaignDate StartDate,
    long StartTurnIndex,
    CampaignDate? EndDate,
    long? EndTurnIndex,
    EntityId? EndCommandId,
    CareerServiceEndReason? EndReason)
{
    [JsonIgnore]
    public bool IsActive => EndDate is null;
}

public sealed record PatronageBondState(
    int ContractVersion,
    EntityId BondId,
    EntityId PatronCharacterId,
    EntityId BeneficiaryCharacterId,
    EntityId SourceProposalId,
    CampaignDate StartDate,
    long StartTurnIndex,
    CampaignDate? EndDate,
    long? EndTurnIndex,
    EntityId? EndCommandId,
    CareerServiceEndReason? EndReason)
{
    [JsonIgnore]
    public bool IsActive => EndDate is null;
}

public sealed record RecommendationRecord(
    int ContractVersion,
    EntityId RecommendationId,
    EntityId RecommenderCharacterId,
    EntityId BeneficiaryCharacterId,
    ServicePrincipalReference Principal,
    EntityId? RecommendedRoleId,
    CampaignDate RecordedDate,
    long RecordedTurnIndex,
    EntityId SourceCommandId);

public sealed record EmploymentTenure(
    int ContractVersion,
    EntityId TenureId,
    EntityId EmployeeCharacterId,
    ServicePrincipalReference Employer,
    EntityId RoleId,
    EntityId SourceProposalId,
    CampaignDate StartDate,
    long StartTurnIndex,
    CampaignDate? EndDate,
    long? EndTurnIndex,
    EntityId? EndCommandId,
    CareerServiceEndReason? EndReason)
{
    [JsonIgnore]
    public bool IsActive => EndDate is null;
}

public sealed record CareerHistoryAggregate(
    int ContractVersion,
    EntityId CharacterId,
    long FoldedRetinueProposalCount,
    long FoldedPatronageProposalCount,
    long FoldedEmploymentProposalCount,
    long FoldedRetinueMembershipCount,
    long FoldedPatronageBondCount,
    long FoldedRecommendationCount,
    long FoldedEmploymentTenureCount,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate)
{
    public static CareerHistoryAggregate Empty(EntityId characterId) => new(
        CareerContractVersions.State,
        characterId,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        null,
        null);

    [JsonIgnore]
    public long TotalFoldedRecordCount => checked(
        FoldedRetinueProposalCount
        + FoldedPatronageProposalCount
        + FoldedEmploymentProposalCount
        + FoldedRetinueMembershipCount
        + FoldedPatronageBondCount
        + FoldedRecommendationCount
        + FoldedEmploymentTenureCount);
}

public sealed record CareerWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CareerProposalState> Proposals,
    IReadOnlyList<RetinueState> Retinues,
    IReadOnlyList<RetinueMembershipState> RetinueMemberships,
    IReadOnlyList<PatronageBondState> PatronageBonds,
    IReadOnlyList<RecommendationRecord> Recommendations,
    IReadOnlyList<EmploymentTenure> EmploymentTenures,
    IReadOnlyList<CareerHistoryAggregate> History)
{
    public static CareerWorldSnapshot Empty { get; } = new(
        CareerContractVersions.Snapshot,
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    public CareerWorldSnapshot Canonicalize() => this with
    {
        Proposals = Proposals.OrderBy(item => item.ProposalId).ToArray(),
        Retinues = Retinues.OrderBy(item => item.RetinueId).ToArray(),
        RetinueMemberships = RetinueMemberships.OrderBy(item => item.MembershipId).ToArray(),
        PatronageBonds = PatronageBonds.OrderBy(item => item.BondId).ToArray(),
        Recommendations = Recommendations.OrderBy(item => item.RecommendationId).ToArray(),
        EmploymentTenures = EmploymentTenures.OrderBy(item => item.TenureId).ToArray(),
        History = History.OrderBy(item => item.CharacterId).ToArray(),
    };
}

public sealed record CharacterCareerDeathChangeSet(
    int ContractVersion,
    IReadOnlyList<CareerProposalState> InvalidatedProposals,
    IReadOnlyList<RetinueMembershipState> EndedRetinueMemberships,
    IReadOnlyList<PatronageBondState> EndedPatronageBonds,
    IReadOnlyList<EmploymentTenure> EndedEmploymentTenures);

public interface IAuthoritativeCareerWorldQuery
{
    IReadOnlyList<CareerProposalState> Proposals { get; }

    IReadOnlyList<RetinueState> Retinues { get; }

    IReadOnlyList<RetinueMembershipState> RetinueMemberships { get; }

    IReadOnlyList<PatronageBondState> PatronageBonds { get; }

    IReadOnlyList<RecommendationRecord> Recommendations { get; }

    IReadOnlyList<EmploymentTenure> EmploymentTenures { get; }

    IReadOnlyList<CareerHistoryAggregate> History { get; }

    bool TryGetProposal(EntityId proposalId, [NotNullWhen(true)] out CareerProposalState? proposal);

    bool TryGetRetinue(EntityId retinueId, [NotNullWhen(true)] out RetinueState? retinue);

    bool TryGetRetinueMembership(
        EntityId membershipId,
        [NotNullWhen(true)] out RetinueMembershipState? membership);

    bool TryGetPatronageBond(EntityId bondId, [NotNullWhen(true)] out PatronageBondState? bond);

    bool TryGetEmploymentTenure(EntityId tenureId, [NotNullWhen(true)] out EmploymentTenure? tenure);

    bool TryGetHistory(EntityId characterId, [NotNullWhen(true)] out CareerHistoryAggregate? history);

    IReadOnlyList<RecommendationRecord> GetRecommendationsInvolving(EntityId characterId);
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(RetinueInviteAction), "retinue_invite.v1")]
[JsonDerivedType(typeof(RespondToRetinueInvitationAction), "retinue_respond.v1")]
[JsonDerivedType(typeof(LeaveRetinueAction), "retinue_leave.v1")]
[JsonDerivedType(typeof(PatronageOfferAction), "patronage_offer.v1")]
[JsonDerivedType(typeof(RespondToPatronageOfferAction), "patronage_respond.v1")]
[JsonDerivedType(typeof(EndPatronageAction), "patronage_end.v1")]
[JsonDerivedType(typeof(MakeRecommendationAction), "recommendation.v1")]
[JsonDerivedType(typeof(EmploymentOfferAction), "employment_offer.v1")]
[JsonDerivedType(typeof(RespondToEmploymentOfferAction), "employment_respond.v1")]
[JsonDerivedType(typeof(EndEmploymentAction), "employment_end.v1")]
[JsonDerivedType(typeof(WithdrawCareerProposalAction), "career_proposal_withdraw.v1")]
public interface ICharacterAction;

public sealed record RetinueInviteAction(EntityId RecipientCharacterId) : ICharacterAction;

public sealed record RespondToRetinueInvitationAction(
    EntityId ProposalId,
    CareerProposalResponse Response) : ICharacterAction;

public sealed record LeaveRetinueAction(EntityId MembershipId) : ICharacterAction;

public sealed record PatronageOfferAction(EntityId RecipientCharacterId) : ICharacterAction;

public sealed record RespondToPatronageOfferAction(
    EntityId ProposalId,
    CareerProposalResponse Response) : ICharacterAction;

public sealed record EndPatronageAction(EntityId BondId) : ICharacterAction;

public sealed record MakeRecommendationAction(
    EntityId BeneficiaryCharacterId,
    ServicePrincipalReference Principal,
    EntityId? RecommendedRoleId) : ICharacterAction;

public sealed record EmploymentOfferAction(
    EntityId RecipientCharacterId,
    ServicePrincipalReference Employer,
    EntityId RoleId) : ICharacterAction;

public sealed record RespondToEmploymentOfferAction(
    EntityId ProposalId,
    CareerProposalResponse Response) : ICharacterAction;

public sealed record EndEmploymentAction(EntityId TenureId) : ICharacterAction;

public sealed record WithdrawCareerProposalAction(EntityId ProposalId) : ICharacterAction;

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CareerProposalCreatedOutcome), "career_proposal_created.v1")]
[JsonDerivedType(typeof(CareerProposalRefusedOutcome), "career_proposal_refused.v1")]
[JsonDerivedType(typeof(CareerProposalWithdrawnOutcome), "career_proposal_withdrawn.v1")]
[JsonDerivedType(typeof(CareerProposalInvalidatedOutcome), "career_proposal_invalidated.v1")]
[JsonDerivedType(typeof(RetinueInvitationAcceptedOutcome), "retinue_invitation_accepted.v1")]
[JsonDerivedType(typeof(RetinueMembershipEndedOutcome), "retinue_membership_ended.v1")]
[JsonDerivedType(typeof(PatronageOfferAcceptedOutcome), "patronage_offer_accepted.v1")]
[JsonDerivedType(typeof(PatronageBondEndedOutcome), "patronage_bond_ended.v1")]
[JsonDerivedType(typeof(RecommendationRecordedOutcome), "recommendation_recorded.v1")]
[JsonDerivedType(typeof(EmploymentOfferAcceptedOutcome), "employment_offer_accepted.v1")]
[JsonDerivedType(typeof(EmploymentTenureEndedOutcome), "employment_tenure_ended.v1")]
public interface ICharacterActionOutcome;

public sealed record CareerProposalCreatedOutcome(CareerProposalState Proposal)
    : ICharacterActionOutcome;

public sealed record CareerProposalRefusedOutcome(CareerProposalState Proposal)
    : ICharacterActionOutcome;

public sealed record CareerProposalWithdrawnOutcome(CareerProposalState Proposal)
    : ICharacterActionOutcome;

public sealed record CareerProposalInvalidatedOutcome(CareerProposalState Proposal)
    : ICharacterActionOutcome;

public sealed record RetinueInvitationAcceptedOutcome(
    CareerProposalState Proposal,
    RetinueState Retinue,
    RetinueMembershipState Membership) : ICharacterActionOutcome;

public sealed record RetinueMembershipEndedOutcome(RetinueMembershipState Membership)
    : ICharacterActionOutcome;

public sealed record PatronageOfferAcceptedOutcome(
    CareerProposalState Proposal,
    PatronageBondState Bond) : ICharacterActionOutcome;

public sealed record PatronageBondEndedOutcome(PatronageBondState Bond)
    : ICharacterActionOutcome;

public sealed record RecommendationRecordedOutcome(RecommendationRecord Recommendation)
    : ICharacterActionOutcome;

public sealed record EmploymentOfferAcceptedOutcome(
    CareerProposalState Proposal,
    EmploymentTenure Tenure) : ICharacterActionOutcome;

public sealed record EmploymentTenureEndedOutcome(EmploymentTenure Tenure)
    : ICharacterActionOutcome;

[method: JsonConstructor]
public sealed record CharacterActionCommandPayload(
    ICharacterAction Action,
    IReadOnlyList<RelationshipMemoryConsequenceSpecification> RelationshipMemoryConsequences)
    : ICampaignCommandPayload
{
    public CharacterActionCommandPayload(ICharacterAction action)
        : this(action, [])
    {
    }
}

public sealed record CharacterActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterAction Action,
    ICharacterActionOutcome Outcome,
    IReadOnlyList<RelationshipMemoryConsequenceSpecification> RelationshipMemoryConsequences)
    : ICampaignEventPayload;

public static class CareerIds
{
    public static EntityId DeriveProposalId(
        CareerProposalKind kind,
        CampaignDate createdDate,
        EntityId commandId)
    {
        RequireDefined(kind, nameof(kind));
        RequireDate(createdDate, nameof(createdDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "career_proposal",
            "career-proposal.v1",
            ((int)kind).ToString(CultureInfo.InvariantCulture),
            FormatDate(createdDate),
            commandId.Value);
    }

    public static EntityId DeriveRetinueId(EntityId leaderCharacterId)
    {
        RequireId(leaderCharacterId, nameof(leaderCharacterId));
        return Hash("retinue", "retinue.v1", leaderCharacterId.Value);
    }

    public static EntityId DeriveRetinueMembershipId(EntityId sourceProposalId)
    {
        RequireId(sourceProposalId, nameof(sourceProposalId));
        return Hash("retinue_membership", "retinue-membership.v1", sourceProposalId.Value);
    }

    public static EntityId DerivePatronageBondId(EntityId sourceProposalId)
    {
        RequireId(sourceProposalId, nameof(sourceProposalId));
        return Hash("patronage_bond", "patronage-bond.v1", sourceProposalId.Value);
    }

    public static EntityId DeriveRecommendationId(CampaignDate recordedDate, EntityId commandId)
    {
        RequireDate(recordedDate, nameof(recordedDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "recommendation",
            "recommendation.v1",
            FormatDate(recordedDate),
            commandId.Value);
    }

    public static EntityId DeriveEmploymentTenureId(EntityId sourceProposalId)
    {
        RequireId(sourceProposalId, nameof(sourceProposalId));
        return Hash("employment_tenure", "employment-tenure.v1", sourceProposalId.Value);
    }

    public static EntityId DeriveCharacterActionEventId(
        CampaignDate resolutionDate,
        EntityId commandId)
    {
        RequireDate(resolutionDate, nameof(resolutionDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "event",
            "character-action-event.v1",
            FormatDate(resolutionDate),
            commandId.Value);
    }

    public static EntityId DeriveRelationshipConsequenceId(EntityId eventId, int zeroBasedIndex)
    {
        RequireId(eventId, nameof(eventId));
        if (zeroBasedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        }

        return Hash(
            "relationship_consequence",
            "character-action-relationship-consequence.v1",
            eventId.Value,
            zeroBasedIndex.ToString(CultureInfo.InvariantCulture));
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
