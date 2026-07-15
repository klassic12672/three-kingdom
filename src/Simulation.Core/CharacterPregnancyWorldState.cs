using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterPregnancyWorldState
    : IAuthoritativeCharacterPregnancyWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly IAuthoritativeCharacterMarriageWorldQuery marriages;
    private readonly SortedDictionary<EntityId, CharacterPregnancyState> activePregnancies = [];
    private readonly Dictionary<EntityId, EntityId> activePregnancyByGestationalParent = [];
    private readonly Dictionary<EntityId, EntityId> activePregnancyByUnion = [];
    private CampaignCalendar calendar;

    public CharacterPregnancyWorldState(
        CharacterPregnancyWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        IAuthoritativeCharacterMarriageWorldQuery marriages,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException(
                "Character-pregnancy snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException(
                "Authoritative character query cannot be null.");
        this.marriages = marriages
            ?? throw new SimulationValidationException(
                "Authoritative character-marriage query cannot be null.");
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-pregnancy calendar is invalid.");
        }

        this.calendar = calendar;
        ValidateSnapshotShape(snapshot);
        AddPregnancies(snapshot.ActivePregnancies);
    }

    public IReadOnlyList<CharacterPregnancyState> ActivePregnancies =>
        activePregnancies.Values.Select(Clone).ToArray();

    public bool TryGetActivePregnancyForGestationalParent(
        EntityId gestationalParentCharacterId,
        [NotNullWhen(true)] out CharacterPregnancyState? pregnancy)
    {
        _ = RequireCharacter(
            gestationalParentCharacterId,
            "Active-pregnancy gestational-parent query");
        return TryGetIndexed(
            activePregnancyByGestationalParent,
            gestationalParentCharacterId,
            out pregnancy);
    }

    public bool TryGetActivePregnancyForUnion(
        EntityId sourceUnionId,
        [NotNullWhen(true)] out CharacterPregnancyState? pregnancy)
    {
        _ = RequireUnion(sourceUnionId, "Active-pregnancy union query");
        return TryGetIndexed(activePregnancyByUnion, sourceUnionId, out pregnancy);
    }

    public IReadOnlyList<CharacterPregnancyState> GetActivePregnanciesInvolving(
        EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Active-pregnancy involved-character query");
        return activePregnancies.Values
            .Where(item => item.GestationalParentCharacterId == characterId
                || item.OtherBiologicalParentCharacterId == characterId)
            .Select(Clone)
            .ToArray();
    }

    public CharacterPregnancyWorldSnapshot CaptureSnapshot() => new(
        CharacterPregnancyContractVersions.Snapshot,
        activePregnancies.Values.Select(Clone).ToArray());

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-pregnancy campaign calendar is invalid.");
        }

        if (value.Date.CompareTo(calendar.Date) < 0
            || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Character-pregnancy campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    internal CharacterPregnancyRegistrationPlan PrepareRegistration(
        EntityId actingActorId,
        RegisterActivePregnancyAction action,
        CampaignDate startDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException(
                "Active-pregnancy registration action cannot be null.");
        }

        if (actingActorId != CharacterFamilySystem.AuthoritativeActorId
            || !startDate.IsValid
            || startDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex
            || !commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(startDate, commandId))
        {
            throw new SimulationValidationException(
                "Active-pregnancy registration requires reserved family authority and exact resolution coordinates.");
        }

        if (action.GestationalParentCharacterId == action.OtherBiologicalParentCharacterId)
        {
            throw new SimulationValidationException(
                "An active pregnancy requires two distinct explicit biological-parent roles.");
        }

        AuthoritativeCharacterProfile gestationalParent = RequireCurrentCharacter(
            action.GestationalParentCharacterId,
            startDate,
            "Active-pregnancy gestational parent");
        AuthoritativeCharacterProfile otherParent = RequireCurrentCharacter(
            action.OtherBiologicalParentCharacterId,
            startDate,
            "Active-pregnancy other biological parent");
        RequireEligibleParent(gestationalParent, startDate, "gestational parent");
        RequireEligibleParent(otherParent, startDate, "other biological parent");

        MarriageUnionState union = RequireUnion(
            action.SourceUnionId,
            "Active-pregnancy source union");
        if (union.Status != MarriageUnionStatus.Active
            || !SamePair(
                union.FirstCharacterId,
                union.SecondCharacterId,
                gestationalParent.CharacterId,
                otherParent.CharacterId)
            || union.StartDate.CompareTo(startDate) > 0
            || union.StartTurnIndex > authoritativeTurnIndex)
        {
            throw new SimulationValidationException(
                $"Marriage union '{union.UnionId}' is not the exact active source union for the explicit parent roles.");
        }

        EntityId? currentPregnancyId = activePregnancyByGestationalParent.TryGetValue(
            gestationalParent.CharacterId,
            out EntityId storedPregnancyId)
                ? storedPregnancyId
                : null;
        if (action.ExpectedCurrentPregnancyId != currentPregnancyId)
        {
            throw new SimulationValidationException(
                $"Active-pregnancy expected-current ID is stale for gestational parent '{gestationalParent.CharacterId}'.");
        }

        EntityId pregnancyId = CharacterPregnancyIds.DerivePregnancyId(
            eventId,
            gestationalParent.CharacterId,
            otherParent.CharacterId,
            union.UnionId);
        if (activePregnancies.ContainsKey(pregnancyId))
        {
            throw new SimulationValidationException(
                $"Active pregnancy '{pregnancyId}' already exists.");
        }

        if (currentPregnancyId is not null)
        {
            throw new SimulationValidationException(
                $"Gestational parent '{gestationalParent.CharacterId}' already has an active pregnancy.");
        }

        if (activePregnancyByUnion.ContainsKey(union.UnionId))
        {
            throw new SimulationValidationException(
                $"Marriage union '{union.UnionId}' already has an active pregnancy.");
        }

        CampaignDate expectedBirthDate = CalculateExpectedBirthDate(startDate);
        CharacterPregnancyState pregnancy = new(
            CharacterPregnancyContractVersions.State,
            pregnancyId,
            gestationalParent.CharacterId,
            otherParent.CharacterId,
            union.UnionId,
            startDate,
            expectedBirthDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        CharacterPregnancyWorldSnapshot updated = CaptureSnapshot() with
        {
            ActivePregnancies =
            [
                .. activePregnancies.Values.Select(Clone),
                Clone(pregnancy),
            ],
        };
        CampaignCalendar candidateCalendar = new(
            startDate.CompareTo(calendar.Date) > 0 ? startDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterPregnancyWorldUpdatePlan pregnancyPlan = new(
            new CharacterPregnancyWorldState(
                updated,
                characters,
                marriages,
                candidateCalendar));
        IReadOnlyList<EntityId> affectedIds = Array.AsReadOnly(new[]
        {
            CharacterFamilySystem.AuthoritativeActorId,
            pregnancy.PregnancyId,
            pregnancy.GestationalParentCharacterId,
            pregnancy.OtherBiologicalParentCharacterId,
            pregnancy.SourceUnionId,
        }
            .Distinct()
            .Order()
            .ToArray());
        return new CharacterPregnancyRegistrationPlan(
            Clone(pregnancy),
            affectedIds,
            pregnancyPlan);
    }

    internal CharacterPregnancyBirthResolutionPlan PrepareBirthResolution(
        EntityId expectedPregnancyId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (!expectedPregnancyId.IsValid
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex
            || !commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Pregnancy-birth resolution coordinates or identities are invalid.");
        }

        if (!activePregnancies.TryGetValue(
                expectedPregnancyId,
                out CharacterPregnancyState? active))
        {
            throw new SimulationValidationException(
                $"Active pregnancy '{expectedPregnancyId}' is stale or missing.");
        }

        if (resolutionDate.CompareTo(active.ExpectedBirthDate) < 0)
        {
            throw new SimulationValidationException(
                $"Active pregnancy '{active.PregnancyId}' is not due for birth until '{active.ExpectedBirthDate}'.");
        }

        AuthoritativeCharacterProfile gestationalParent = RequireCurrentCharacter(
            active.GestationalParentCharacterId,
            resolutionDate,
            "Pregnancy-birth gestational parent");
        AuthoritativeCharacterProfile otherParent = RequireCurrentCharacter(
            active.OtherBiologicalParentCharacterId,
            resolutionDate,
            "Pregnancy-birth other biological parent");
        if (gestationalParent.Condition.VitalStatus != CharacterVitalStatus.Alive
            || otherParent.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException(
                "Pregnancy birth requires both biological parents to be living.");
        }

        MarriageUnionState union = RequireUnion(
            active.SourceUnionId,
            "Pregnancy-birth source union");
        if (union.Status != MarriageUnionStatus.Active
            || !SamePair(
                union.FirstCharacterId,
                union.SecondCharacterId,
                gestationalParent.CharacterId,
                otherParent.CharacterId))
        {
            throw new SimulationValidationException(
                $"Marriage union '{union.UnionId}' is not the exact active source union for pregnancy '{active.PregnancyId}'.");
        }

        CharacterPregnancyWorldSnapshot updated = CaptureSnapshot() with
        {
            ActivePregnancies = activePregnancies.Values
                .Where(item => item.PregnancyId != active.PregnancyId)
                .Select(Clone)
                .ToArray(),
        };
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0
                ? resolutionDate
                : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        return new CharacterPregnancyBirthResolutionPlan(
            Clone(active),
            new CharacterPregnancyWorldUpdatePlan(
                new CharacterPregnancyWorldState(
                    updated,
                    characters,
                    marriages,
                    candidateCalendar)));
    }

    internal void ApplyPrepared(CharacterPregnancyWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-pregnancy update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private static void ValidateSnapshotShape(CharacterPregnancyWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterPregnancyContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-pregnancy snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.ActivePregnancies is null
            || snapshot.ActivePregnancies.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Active-pregnancy snapshot collection and entries cannot be null.");
        }
    }

    private void AddPregnancies(IReadOnlyList<CharacterPregnancyState> source)
    {
        foreach (CharacterPregnancyState pregnancy in source)
        {
            ValidatePregnancy(pregnancy);
            if (!activePregnancies.TryAdd(pregnancy.PregnancyId, Clone(pregnancy)))
            {
                throw new SimulationValidationException(
                    $"Duplicate active pregnancy '{pregnancy.PregnancyId}'.");
            }

            if (!activePregnancyByGestationalParent.TryAdd(
                    pregnancy.GestationalParentCharacterId,
                    pregnancy.PregnancyId))
            {
                throw new SimulationValidationException(
                    $"Gestational parent '{pregnancy.GestationalParentCharacterId}' has more than one active pregnancy.");
            }

            if (!activePregnancyByUnion.TryAdd(
                    pregnancy.SourceUnionId,
                    pregnancy.PregnancyId))
            {
                throw new SimulationValidationException(
                    $"Marriage union '{pregnancy.SourceUnionId}' has more than one active pregnancy.");
            }
        }
    }

    private void ValidatePregnancy(CharacterPregnancyState pregnancy)
    {
        if (pregnancy.ContractVersion != CharacterPregnancyContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Pregnancy '{pregnancy.PregnancyId}' has unsupported contract version {pregnancy.ContractVersion}.");
        }

        RequireNamespacedId(pregnancy.PregnancyId, "pregnancy:", "Pregnancy ID");
        RequireNamespacedId(
            pregnancy.SourceUnionId,
            "marriage_union:",
            $"Pregnancy '{pregnancy.PregnancyId}' source union ID");
        RequireNamespacedId(
            pregnancy.SourceCommandId,
            "command:",
            $"Pregnancy '{pregnancy.PregnancyId}' source command ID");
        RequireNamespacedId(
            pregnancy.SourceEventId,
            "event:",
            $"Pregnancy '{pregnancy.PregnancyId}' source event ID");
        if (pregnancy.GestationalParentCharacterId
            == pregnancy.OtherBiologicalParentCharacterId)
        {
            throw new SimulationValidationException(
                $"Pregnancy '{pregnancy.PregnancyId}' must retain two distinct parent roles.");
        }

        ValidateRecordPoint(
            pregnancy.StartDate,
            pregnancy.StartTurnIndex,
            $"Pregnancy '{pregnancy.PregnancyId}' start");
        if (pregnancy.ExpectedBirthDate != CalculateExpectedBirthDate(pregnancy.StartDate)
            || pregnancy.SourceEventId != CharacterFamilyIds.DeriveActionEventId(
                pregnancy.StartDate,
                pregnancy.SourceCommandId)
            || pregnancy.PregnancyId != CharacterPregnancyIds.DerivePregnancyId(
                pregnancy.SourceEventId,
                pregnancy.GestationalParentCharacterId,
                pregnancy.OtherBiologicalParentCharacterId,
                pregnancy.SourceUnionId))
        {
            throw new SimulationValidationException(
                $"Pregnancy '{pregnancy.PregnancyId}' dates or source identity are incoherent.");
        }

        AuthoritativeCharacterProfile gestationalParent = RequireBornCharacter(
            pregnancy.GestationalParentCharacterId,
            pregnancy.StartDate,
            $"Pregnancy '{pregnancy.PregnancyId}' gestational parent");
        AuthoritativeCharacterProfile otherParent = RequireBornCharacter(
            pregnancy.OtherBiologicalParentCharacterId,
            pregnancy.StartDate,
            $"Pregnancy '{pregnancy.PregnancyId}' other biological parent");
        if (gestationalParent.Condition.VitalStatus != CharacterVitalStatus.Alive
            || otherParent.Condition.VitalStatus != CharacterVitalStatus.Alive
            || CalculateAge(gestationalParent.BirthDate, pregnancy.StartDate)
                < CharacterPregnancyLimits.MinimumParentAge
            || CalculateAge(otherParent.BirthDate, pregnancy.StartDate)
                < CharacterPregnancyLimits.MinimumParentAge)
        {
            throw new SimulationValidationException(
                $"Pregnancy '{pregnancy.PregnancyId}' parents must both be living and adults at its start.");
        }

        MarriageUnionState union = RequireUnion(
            pregnancy.SourceUnionId,
            $"Pregnancy '{pregnancy.PregnancyId}' source union");
        if (union.Status != MarriageUnionStatus.Active
            || !SamePair(
                union.FirstCharacterId,
                union.SecondCharacterId,
                gestationalParent.CharacterId,
                otherParent.CharacterId)
            || union.StartDate.CompareTo(pregnancy.StartDate) > 0
            || union.StartTurnIndex > pregnancy.StartTurnIndex)
        {
            throw new SimulationValidationException(
                $"Pregnancy '{pregnancy.PregnancyId}' source union was not active for its exact parent pair at the start.");
        }
    }

    private bool TryGetIndexed(
        IReadOnlyDictionary<EntityId, EntityId> index,
        EntityId key,
        [NotNullWhen(true)] out CharacterPregnancyState? pregnancy)
    {
        if (index.TryGetValue(key, out EntityId pregnancyId))
        {
            pregnancy = Clone(activePregnancies[pregnancyId]);
            return true;
        }

        pregnancy = null;
        return false;
    }

    private AuthoritativeCharacterProfile RequireCurrentCharacter(
        EntityId characterId,
        CampaignDate date,
        string label)
    {
        AuthoritativeCharacterProfile profile = RequireCharacter(characterId, label);
        if (profile.BirthDate.CompareTo(date) > 0)
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' is not born by '{date}'.");
        }

        return profile;
    }

    private AuthoritativeCharacterProfile RequireBornCharacter(
        EntityId characterId,
        CampaignDate date,
        string label) => RequireCurrentCharacter(characterId, date, label);

    private AuthoritativeCharacterProfile RequireCharacter(
        EntityId characterId,
        string label)
    {
        if (!characterId.IsValid
            || !characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile)
            || profile is null)
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' does not exist.");
        }

        return profile;
    }

    private MarriageUnionState RequireUnion(EntityId unionId, string label)
    {
        if (!unionId.IsValid
            || !marriages.TryGetUnion(unionId, out MarriageUnionState? union)
            || union is null)
        {
            throw new SimulationValidationException(
                $"{label} '{unionId}' does not exist.");
        }

        return union;
    }

    private static void RequireEligibleParent(
        AuthoritativeCharacterProfile profile,
        CampaignDate date,
        string role)
    {
        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive
            || CalculateAge(profile.BirthDate, date)
                < CharacterPregnancyLimits.MinimumParentAge)
        {
            throw new SimulationValidationException(
                $"Active-pregnancy {role} '{profile.CharacterId}' must be living and at least 18.");
        }
    }

    private void ValidateRecordPoint(CampaignDate date, long turnIndex, string label)
    {
        if (!date.IsValid
            || date.CompareTo(calendar.Date) > 0
            || turnIndex < 0
            || turnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"{label} date or turn is invalid.");
        }
    }

    private static CampaignDate CalculateExpectedBirthDate(CampaignDate startDate)
    {
        try
        {
            return startDate.AddDays(CharacterPregnancyLimits.GestationDays);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new SimulationValidationException(
                $"Active-pregnancy start date '{startDate}' cannot represent its fixed expected birth date.");
        }
        catch (OverflowException)
        {
            throw new SimulationValidationException(
                $"Active-pregnancy start date '{startDate}' cannot represent its fixed expected birth date.");
        }
    }

    private static int CalculateAge(CampaignDate birthDate, CampaignDate date)
    {
        int age = date.Year - birthDate.Year;
        if (date.Month < birthDate.Month
            || (date.Month == birthDate.Month && date.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private static bool SamePair(
        EntityId first,
        EntityId second,
        EntityId candidateFirst,
        EntityId candidateSecond) =>
        (first == candidateFirst && second == candidateSecond)
        || (first == candidateSecond && second == candidateFirst);

    private static void RequireNamespacedId(
        EntityId id,
        string requiredPrefix,
        string label)
    {
        if (!id.IsValid
            || !id.Value.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            throw new SimulationValidationException(
                $"{label} '{id}' must use the '{requiredPrefix}' namespace.");
        }
    }

    private void ReplaceFrom(CharacterPregnancyWorldState source)
    {
        activePregnancies.Clear();
        activePregnancyByGestationalParent.Clear();
        activePregnancyByUnion.Clear();
        foreach (CharacterPregnancyState pregnancy in source.activePregnancies.Values)
        {
            activePregnancies.Add(pregnancy.PregnancyId, Clone(pregnancy));
            activePregnancyByGestationalParent.Add(
                pregnancy.GestationalParentCharacterId,
                pregnancy.PregnancyId);
            activePregnancyByUnion.Add(pregnancy.SourceUnionId, pregnancy.PregnancyId);
        }

        calendar = source.calendar;
    }

    private static CharacterPregnancyState Clone(CharacterPregnancyState value) =>
        value with { };
}

internal sealed record CharacterPregnancyWorldUpdatePlan(
    CharacterPregnancyWorldState Candidate);

internal sealed record CharacterPregnancyRegistrationPlan(
    CharacterPregnancyState Pregnancy,
    IReadOnlyList<EntityId> AffectedIds,
    CharacterPregnancyWorldUpdatePlan PregnancyPlan);

internal sealed record CharacterPregnancyBirthResolutionPlan(
    CharacterPregnancyState ResolvedPregnancy,
    CharacterPregnancyWorldUpdatePlan PregnancyPlan);
