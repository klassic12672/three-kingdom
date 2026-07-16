using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Simulation.Core;

namespace Game.Application;

public static class CharacterObserverContractVersions
{
    public const int CharacterProfile = 1;
    public const int HouseholdView = 1;
    public const int SuccessionView = 1;
}

public sealed record KnownCharacterConditionDetails(
    CharacterHealthStatus? HealthStatus,
    bool? IsIncapacitated,
    CharacterCustodyStatus CustodyStatus,
    EntityId? CustodianId);

public sealed record CharacterPrivateDetails(
    IReadOnlyList<EntityId> AbilityIds,
    IReadOnlyList<EntityId> AptitudeIds,
    IReadOnlyList<EntityId> TraitIds,
    IReadOnlyList<EntityId> FlawIds,
    IReadOnlyList<EntityId> AmbitionIds,
    IReadOnlyList<EntityId> AcquiredEducationAbilityIds,
    bool ListsTruncated);

public sealed record LegalUnionSummary(
    EntityId UnionId,
    EntityId OtherParticipantCharacterId,
    MarriageUnionForm Form,
    MarriageUnionStatus Status,
    CampaignDate StartDate,
    CampaignDate? ResolutionDate);

public sealed record PoliticalBetrothalSummary(
    EntityId BetrothalId,
    EntityId OtherParticipantCharacterId,
    MarriageUnionForm Form,
    PoliticalBetrothalStatus Status,
    CampaignDate StartDate,
    CampaignDate? ResolutionDate);

public sealed record RomanceRouteSummary(
    EntityId RouteId,
    EntityId OtherParticipantCharacterId,
    RomanceRouteStatus Status,
    int ProgressLevel,
    CampaignDate StartDate,
    CampaignDate? ResolutionDate);

public sealed record CharacterProfile(
    int ContractVersion,
    EntityId CharacterId,
    StructuredCharacterName StructuredName,
    int Age,
    EntityId? CultureId,
    EntityId? OriginLocationId,
    CharacterVitalStatus VitalStatus,
    KnownCharacterConditionDetails? KnownConditionDetails,
    EntityId? FamilyId,
    EntityId? HouseholdId,
    IReadOnlyList<CharacterParentLink> ParentLinks,
    int TotalParentLinkCount,
    IReadOnlyList<CharacterChildLink> ChildLinks,
    int TotalChildLinkCount,
    IReadOnlyList<EntityId> ReputationIds,
    CharacterPrivateDetails? PrivateDetails,
    IReadOnlyList<LegalUnionSummary> LegalUnions,
    IReadOnlyList<PoliticalBetrothalSummary> PoliticalBetrothals,
    IReadOnlyList<RomanceRouteSummary> RomanceRoutes);

public sealed record HouseholdView(
    int ContractVersion,
    EntityId HouseholdId,
    EntityId NameKey,
    EntityId HeadCharacterId,
    IReadOnlyList<EntityId> MemberIds,
    int TotalMemberCount,
    bool MembersTruncated);

public sealed record CurrentDesignationSummary(
    EntityId DesignationId,
    EntityId HeirCharacterId);

public sealed record ActiveSuccessionClaimSummary(
    EntityId ClaimId,
    EntityId ClaimantCharacterId);

public sealed record ActiveSuccessionSupportSummary(
    EntityId SupportId,
    EntityId SupporterCharacterId,
    EntityId SupportedCandidateCharacterId);

public sealed record CompletedSuccessionSummary(
    EntityId ResolutionId,
    SuccessionResolutionStatus Status,
    EntityId? SelectedSuccessorCharacterId,
    IReadOnlyList<EntityId> DisputedCandidateCharacterIds,
    CampaignDate ResolutionDate);

public sealed record PublicRegencySummary(
    EntityId SuccessorCharacterId,
    EntityId? RegentCharacterId,
    SuccessionRegencyReason Reasons);

public sealed record SuccessionView(
    int ContractVersion,
    EntityId SubjectCharacterId,
    CurrentDesignationSummary? CurrentDesignation,
    IReadOnlyList<ActiveSuccessionClaimSummary> ActiveClaims,
    IReadOnlyList<ActiveSuccessionSupportSummary> ActiveSupports,
    CompletedSuccessionSummary? CompletedResolution,
    PublicRegencySummary? Regency);

public sealed class CharacterProfileQuery(IWorldQuery world)
{
    private const int KinshipLimit = 64;
    private const int PrivateListLimit = 64;
    private const int ReputationLimit = 64;
    private readonly IWorldQuery world = world ?? throw new ArgumentNullException(nameof(world));

    public bool TryGet(
        EntityId observerCharacterId,
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out CharacterProfile? profile)
    {
        if (!world.Characters.TryGetCharacterProfile(observerCharacterId, out _)
            || !world.Characters.TryGetCharacterProfile(
                subjectCharacterId,
                out AuthoritativeCharacterProfile? subject))
        {
            profile = null;
            return false;
        }

        bool isSelf = observerCharacterId == subjectCharacterId;
        KnownCharacterConditionDetails? condition = isSelf
            ? new(
                subject.Condition.HealthStatus,
                subject.Condition.IsIncapacitated,
                subject.Condition.CustodyStatus,
                subject.Condition.CustodianId)
            : subject.Condition.CustodianId == observerCharacterId
                ? new(
                    null,
                    null,
                    subject.Condition.CustodyStatus,
                    subject.Condition.CustodianId)
                : null;
        CharacterPrivateDetails? privateDetails = isSelf
            ? CreatePrivateDetails(subject)
            : null;
        CharacterParentLink[] parents = subject.ParentLinks
            .OrderBy(item => item.ParentCharacterId)
            .ThenBy(item => item.Kind)
            .Take(KinshipLimit)
            .Select(item => item with { })
            .ToArray();
        CharacterChildLink[] children = subject.ChildLinks
            .OrderBy(item => item.ChildCharacterId)
            .ThenBy(item => item.Kind)
            .Take(KinshipLimit)
            .Select(item => item with { })
            .ToArray();
        LegalUnionSummary[] unions = world.CharacterMarriages
            .GetUnionsInvolving(subjectCharacterId)
            .OrderBy(item => item.UnionId)
            .Take(CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
            .Select(item => new LegalUnionSummary(
                item.UnionId,
                OtherParticipant(
                    item.FirstCharacterId,
                    item.SecondCharacterId,
                    subjectCharacterId),
                item.Form,
                item.Status,
                item.StartDate,
                item.EndDate))
            .ToArray();
        PoliticalBetrothalSummary[] betrothals = world.CharacterMarriages
            .GetBetrothalsInvolving(subjectCharacterId)
            .OrderBy(item => item.BetrothalId)
            .Take(CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
            .Select(item => new PoliticalBetrothalSummary(
                item.BetrothalId,
                OtherParticipant(
                    item.FirstCharacterId,
                    item.SecondCharacterId,
                    subjectCharacterId),
                item.IntendedForm,
                item.Status,
                item.StartDate,
                item.ResolutionDate))
            .ToArray();
        RomanceRouteSummary[] romance = world.CharacterMarriages
            .GetRomanceRoutesInvolving(subjectCharacterId)
            .Where(item => isSelf || OtherParticipant(
                item.FirstCharacterId,
                item.SecondCharacterId,
                subjectCharacterId) == observerCharacterId)
            .OrderBy(item => item.RouteId)
            .Take(CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
            .Select(item => new RomanceRouteSummary(
                item.RouteId,
                OtherParticipant(
                    item.FirstCharacterId,
                    item.SecondCharacterId,
                    subjectCharacterId),
                item.Status,
                item.ProgressLevel,
                item.StartDate,
                item.ResolutionDate))
            .ToArray();
        profile = new CharacterProfile(
            CharacterObserverContractVersions.CharacterProfile,
            subject.CharacterId,
            subject.StructuredName with { },
            subject.Age,
            subject.CultureId,
            subject.OriginLocationId,
            subject.Condition.VitalStatus,
            condition,
            subject.FamilyId,
            subject.HouseholdId,
            ReadOnly(parents),
            subject.ParentLinks.Count,
            ReadOnly(children),
            subject.ChildLinks.Count,
            ReadOnly(subject.ReputationIds.Order().Take(ReputationLimit).ToArray()),
            privateDetails,
            ReadOnly(unions),
            ReadOnly(betrothals),
            ReadOnly(romance));
        return true;
    }

    private static CharacterPrivateDetails CreatePrivateDetails(
        AuthoritativeCharacterProfile subject)
    {
        EntityId[] abilities = subject.AbilityIds.Order().Take(PrivateListLimit).ToArray();
        EntityId[] aptitudes = subject.AptitudeIds.Order().Take(PrivateListLimit).ToArray();
        EntityId[] traits = subject.TraitIds.Order().Take(PrivateListLimit).ToArray();
        EntityId[] flaws = subject.FlawIds.Order().Take(PrivateListLimit).ToArray();
        EntityId[] ambitions = subject.AmbitionIds.Order().Take(PrivateListLimit).ToArray();
        EntityId[] acquired = subject.EducationAttainments
            .Select(item => item.AbilityId)
            .Distinct()
            .Order()
            .Take(PrivateListLimit)
            .ToArray();
        bool truncated = subject.AbilityIds.Count > PrivateListLimit
            || subject.AptitudeIds.Count > PrivateListLimit
            || subject.TraitIds.Count > PrivateListLimit
            || subject.FlawIds.Count > PrivateListLimit
            || subject.AmbitionIds.Count > PrivateListLimit
            || subject.EducationAttainments
                .Select(item => item.AbilityId)
                .Distinct()
                .Skip(PrivateListLimit)
                .Any();
        return new CharacterPrivateDetails(
            ReadOnly(abilities),
            ReadOnly(aptitudes),
            ReadOnly(traits),
            ReadOnly(flaws),
            ReadOnly(ambitions),
            ReadOnly(acquired),
            truncated);
    }

    private static EntityId OtherParticipant(
        EntityId first,
        EntityId second,
        EntityId subject) => first == subject ? second : first;

    private static ReadOnlyCollection<T> ReadOnly<T>(T[] values) =>
        Array.AsReadOnly(values);
}

public sealed class HouseholdViewQuery(IWorldQuery world)
{
    private const int MemberLimit = 256;
    private readonly IWorldQuery world = world ?? throw new ArgumentNullException(nameof(world));

    public bool TryGet(
        EntityId observerCharacterId,
        EntityId householdId,
        [NotNullWhen(true)] out HouseholdView? household)
    {
        if (!world.Characters.TryGetCharacterProfile(observerCharacterId, out _)
            || !world.Characters.TryGetHousehold(
                householdId,
                out AuthoritativeHouseholdView? source))
        {
            household = null;
            return false;
        }

        EntityId[] members = source.MemberIds
            .Order()
            .Take(MemberLimit)
            .ToArray();
        household = new HouseholdView(
            CharacterObserverContractVersions.HouseholdView,
            source.HouseholdId,
            source.NameKey,
            source.HeadCharacterId,
            Array.AsReadOnly(members),
            source.MemberIds.Count,
            source.MemberIds.Count > MemberLimit);
        return true;
    }
}

public sealed class SuccessionViewQuery(IWorldQuery world)
{
    private readonly IWorldQuery world = world ?? throw new ArgumentNullException(nameof(world));

    public bool TryGet(
        EntityId observerCharacterId,
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionView? view)
    {
        if (!world.Characters.TryGetCharacterProfile(observerCharacterId, out _)
            || !world.Characters.TryGetCharacterProfile(subjectCharacterId, out _))
        {
            view = null;
            return false;
        }

        bool isSubject = observerCharacterId == subjectCharacterId;
        CurrentDesignationSummary? designation = null;
        if (world.CharacterSuccessions.TryGetCurrentDesignation(
                subjectCharacterId,
                out HeirDesignationState? current)
            && (isSubject || current.HeirCharacterId == observerCharacterId))
        {
            designation = new(
                current.DesignationId,
                current.HeirCharacterId);
        }

        ActiveSuccessionClaimSummary[] claims = world.CharacterSuccessions
            .GetActiveClaimsForSubject(subjectCharacterId)
            .Where(item => isSubject
                || item.ClaimantCharacterId == observerCharacterId)
            .OrderBy(item => item.ClaimId)
            .Take(CharacterSuccessionLimits.MaximumActiveClaimsPerSubject)
            .Select(item => new ActiveSuccessionClaimSummary(
                item.ClaimId,
                item.ClaimantCharacterId))
            .ToArray();
        ActiveSuccessionSupportSummary[] supports = world.CharacterSuccessions
            .GetActiveSupportsForSubject(subjectCharacterId)
            .Where(item => isSubject
                || item.SupporterId == observerCharacterId
                || item.SupportedCandidateId == observerCharacterId)
            .OrderBy(item => item.SupportId)
            .Take(CharacterSuccessionLimits.MaximumActiveSupportsPerSubject)
            .Select(item => new ActiveSuccessionSupportSummary(
                item.SupportId,
                item.SupporterId,
                item.SupportedCandidateId))
            .ToArray();
        CompletedSuccessionSummary? completed = null;
        PublicRegencySummary? regency = null;
        if (world.CharacterSuccessions.TryGetResolutionForSubject(
                subjectCharacterId,
                out SuccessionResolutionState? resolution))
        {
            completed = new(
                resolution.ResolutionId,
                resolution.Status,
                resolution.SelectedCandidate?.CandidateCharacterId,
                Array.AsReadOnly(resolution.DisputedCandidates
                    .Select(item => item.CandidateCharacterId)
                    .Order()
                    .Take(CharacterSuccessionLimits.MaximumDisputedCandidates)
                    .ToArray()),
                resolution.ResolutionDate);
            if (resolution.Regency is SuccessionRegencyHook sourceRegency)
            {
                regency = new(
                    sourceRegency.SuccessorCharacterId,
                    sourceRegency.RegentCharacterId,
                    sourceRegency.Reasons);
            }
        }

        view = new SuccessionView(
            CharacterObserverContractVersions.SuccessionView,
            subjectCharacterId,
            designation,
            Array.AsReadOnly(claims),
            Array.AsReadOnly(supports),
            completed,
            regency);
        return true;
    }
}

public sealed class ControlledCharacterObserverFacade
{
    private readonly IWorldQuery world;
    private readonly EntityId? initialControlledCharacterId;
    private readonly CharacterProfileQuery characters;
    private readonly HouseholdViewQuery households;
    private readonly SuccessionViewQuery successions;
    private readonly RelationshipSummaryQuery relationships;

    public ControlledCharacterObserverFacade(
        IWorldQuery world,
        EntityId? initialControlledCharacterId = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.initialControlledCharacterId = initialControlledCharacterId;
        characters = new(world);
        households = new(world);
        successions = new(world);
        relationships = new(world);
    }

    public bool TryGetControlledCharacterId(out EntityId characterId)
    {
        PlayerCampaignContinuityState? continuity =
            world.CharacterSuccessions.CampaignContinuity;
        EntityId? current = continuity is null
            ? initialControlledCharacterId
            : continuity.Status == PlayerCampaignContinuityStatus.Active
                ? continuity.ControlledCharacterId
                : null;
        if (current is EntityId value
            && world.Characters.TryGetCharacterProfile(value, out _))
        {
            characterId = value;
            return true;
        }

        characterId = default;
        return false;
    }

    public bool TryGetCharacter(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out CharacterProfile? profile)
    {
        if (TryGetControlledCharacterId(out EntityId observer))
        {
            return characters.TryGet(observer, subjectCharacterId, out profile);
        }

        profile = null;
        return false;
    }

    public bool TryGetHousehold(
        EntityId householdId,
        [NotNullWhen(true)] out HouseholdView? household)
    {
        if (TryGetControlledCharacterId(out EntityId observer))
        {
            return households.TryGet(observer, householdId, out household);
        }

        household = null;
        return false;
    }

    public bool TryGetSuccession(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionView? succession)
    {
        if (TryGetControlledCharacterId(out EntityId observer))
        {
            return successions.TryGet(observer, subjectCharacterId, out succession);
        }

        succession = null;
        return false;
    }

    public bool TryGetRelationship(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out RelationshipSummary? summary)
    {
        if (TryGetControlledCharacterId(out EntityId observer))
        {
            return relationships.TryGet(observer, subjectCharacterId, out summary);
        }

        summary = null;
        return false;
    }
}
