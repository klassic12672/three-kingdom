using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterGuardianshipWorldState
    : IAuthoritativeCharacterGuardianshipWorldQuery
{
    private const int AdultAge = 18;

    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, CharacterGuardianshipState> guardianships = [];
    private readonly Dictionary<EntityId, EntityId> activeGuardianshipByWard = [];
    private CampaignCalendar calendar;

    public CharacterGuardianshipWorldState(
        CharacterGuardianshipWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException(
                "Character-guardianship snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException(
                "Authoritative character query cannot be null.");
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-guardianship calendar is invalid.");
        }

        this.calendar = calendar;
        ValidateSnapshotShape(snapshot);
        AddGuardianships(snapshot.Guardianships);
    }

    public IReadOnlyList<CharacterGuardianshipState> Guardianships =>
        guardianships.Values.Select(Clone).ToArray();

    public bool TryGetActivePrimaryGuardianshipForWard(
        EntityId wardCharacterId,
        [NotNullWhen(true)] out CharacterGuardianshipState? guardianship)
    {
        RequireCharacter(wardCharacterId, "Primary-guardianship query ward");
        if (activeGuardianshipByWard.TryGetValue(
                wardCharacterId,
                out EntityId guardianshipId))
        {
            guardianship = Clone(guardianships[guardianshipId]);
            return true;
        }

        guardianship = null;
        return false;
    }

    public IReadOnlyList<CharacterGuardianshipState> GetGuardianshipsInvolving(
        EntityId characterId)
    {
        RequireCharacter(characterId, "Character-guardianship query character");
        return guardianships.Values
            .Where(item => item.WardCharacterId == characterId
                || item.GuardianCharacterId == characterId)
            .Select(Clone)
            .ToArray();
    }

    public CharacterGuardianshipWorldSnapshot CaptureSnapshot() => new(
        CharacterGuardianshipContractVersions.Snapshot,
        guardianships.Values.Select(Clone).ToArray());

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-guardianship campaign calendar is invalid.");
        }

        if (value.Date.CompareTo(calendar.Date) < 0 || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Character-guardianship campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    internal CharacterGuardianshipEstablishmentPlan PrepareEstablishment(
        EstablishPrimaryGuardianshipAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException(
                "Primary-guardianship establishment action cannot be null.");
        }

        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Primary-guardianship resolution date or turn is invalid.");
        }

        if (!commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Primary-guardianship command or family-event identity is invalid.");
        }

        if (action.GuardianCharacterId == action.WardCharacterId)
        {
            throw new SimulationValidationException(
                "A character cannot be their own primary guardian.");
        }

        AuthoritativeCharacterProfile guardian = RequireCurrentCharacter(
            action.GuardianCharacterId,
            resolutionDate,
            "Primary guardian");
        AuthoritativeCharacterProfile ward = RequireCurrentCharacter(
            action.WardCharacterId,
            resolutionDate,
            "Primary-guardianship ward");
        if (guardian.Condition.VitalStatus != CharacterVitalStatus.Alive
            || guardian.Condition.IsIncapacitated
            || guardian.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            throw new SimulationValidationException(
                $"Primary guardian '{guardian.CharacterId}' must be living, capable, and free.");
        }

        if (CalculateAge(guardian.BirthDate, resolutionDate) < AdultAge
            || guardian.BirthDate.CompareTo(ward.BirthDate) >= 0)
        {
            throw new SimulationValidationException(
                $"Primary guardian '{guardian.CharacterId}' must be at least 18 and born before ward '{ward.CharacterId}'.");
        }

        if (ward.Condition.VitalStatus != CharacterVitalStatus.Alive
            || CalculateAge(ward.BirthDate, resolutionDate) >= AdultAge)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship ward '{ward.CharacterId}' must be living and under 18.");
        }

        EntityId? currentId = activeGuardianshipByWard.TryGetValue(
            ward.CharacterId,
            out EntityId storedId)
                ? storedId
                : null;
        if (action.ExpectedCurrentPrimaryGuardianshipId != currentId)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship expected-current ID is stale for ward '{ward.CharacterId}'.");
        }

        if (currentId is not null)
        {
            throw new SimulationValidationException(
                $"Ward '{ward.CharacterId}' already has an active primary guardianship.");
        }

        EnforceRetainedCapacity(guardian.CharacterId, "guardian");
        EnforceRetainedCapacity(ward.CharacterId, "ward");
        CharacterGuardianshipState established = new(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(
                eventId,
                ward.CharacterId,
                guardian.CharacterId),
            ward.CharacterId,
            guardian.CharacterId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        CharacterGuardianshipWorldSnapshot updated = CaptureSnapshot() with
        {
            Guardianships =
            [
                .. guardianships.Values.Select(Clone),
                Clone(established),
            ],
        };
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0
                ? resolutionDate
                : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        return new CharacterGuardianshipEstablishmentPlan(
            Clone(established),
            new CharacterGuardianshipWorldUpdatePlan(
                new CharacterGuardianshipWorldState(
                    updated,
                    characters,
                    candidateCalendar)));
    }

    internal CharacterGuardianshipTerminationPlan PrepareTermination(
        EndPrimaryGuardianshipAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException(
                "Primary-guardianship termination action cannot be null.");
        }

        ValidateResolutionCoordinates(
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            "termination");
        CharacterGuardianshipState active = RequireExpectedActiveGuardianship(
            action.WardCharacterId,
            action.ExpectedCurrentPrimaryGuardianshipId);
        switch (action.EndReason)
        {
            case CharacterGuardianshipEndReason.Revoked:
                break;
            case CharacterGuardianshipEndReason.GuardianUnavailable:
                AuthoritativeCharacterProfile guardian = RequireCurrentCharacter(
                    active.GuardianCharacterId,
                    resolutionDate,
                    "Primary guardian");
                if (IsEligibleGuardianCondition(guardian.Condition))
                {
                    throw new SimulationValidationException(
                        $"Primary guardian '{guardian.CharacterId}' is not unavailable.");
                }

                break;
            default:
                throw new SimulationValidationException(
                    $"Primary guardianship cannot be ended explicitly for reason '{action.EndReason}'.");
        }

        CharacterGuardianshipState ended = EndGuardianship(
            active,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            action.EndReason);
        return new CharacterGuardianshipTerminationPlan(
            Clone(ended),
            CreateUpdatePlan(
                ReplaceGuardianship(active.GuardianshipId, ended),
                resolutionDate,
                authoritativeTurnIndex));
    }

    internal CharacterGuardianshipReplacementPlan PrepareReplacement(
        ReplacePrimaryGuardianshipAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (action is null)
        {
            throw new SimulationValidationException(
                "Primary-guardianship replacement action cannot be null.");
        }

        ValidateResolutionCoordinates(
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            "replacement");
        CharacterGuardianshipState active = RequireExpectedActiveGuardianship(
            action.WardCharacterId,
            action.ExpectedCurrentPrimaryGuardianshipId);
        if (action.ReplacementGuardianCharacterId == action.WardCharacterId)
        {
            throw new SimulationValidationException(
                "A character cannot be their own replacement primary guardian.");
        }

        if (action.ReplacementGuardianCharacterId == active.GuardianCharacterId)
        {
            throw new SimulationValidationException(
                "A replacement primary guardian must differ from the current primary guardian.");
        }

        AuthoritativeCharacterProfile replacementGuardian = RequireCurrentCharacter(
            action.ReplacementGuardianCharacterId,
            resolutionDate,
            "Replacement primary guardian");
        AuthoritativeCharacterProfile ward = RequireCurrentCharacter(
            action.WardCharacterId,
            resolutionDate,
            "Primary-guardianship ward");
        if (!IsEligibleGuardianCondition(replacementGuardian.Condition))
        {
            throw new SimulationValidationException(
                $"Replacement primary guardian '{replacementGuardian.CharacterId}' must be living, capable, and free.");
        }

        if (CalculateAge(replacementGuardian.BirthDate, resolutionDate) < AdultAge
            || replacementGuardian.BirthDate.CompareTo(ward.BirthDate) >= 0)
        {
            throw new SimulationValidationException(
                $"Replacement primary guardian '{replacementGuardian.CharacterId}' must be at least 18 and born before ward '{ward.CharacterId}'.");
        }

        if (ward.Condition.VitalStatus != CharacterVitalStatus.Alive
            || CalculateAge(ward.BirthDate, resolutionDate) >= AdultAge)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship ward '{ward.CharacterId}' must be living and under 18.");
        }

        EnforceRetainedCapacity(ward.CharacterId, "ward");
        EnforceRetainedCapacity(replacementGuardian.CharacterId, "replacement guardian");
        EntityId replacementId = CharacterGuardianshipIds.DeriveGuardianshipId(
            eventId,
            ward.CharacterId,
            replacementGuardian.CharacterId);
        if (guardianships.ContainsKey(replacementId))
        {
            throw new SimulationValidationException(
                $"Replacement guardianship '{replacementId}' already exists.");
        }

        CharacterGuardianshipState ended = EndGuardianship(
            active,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            CharacterGuardianshipEndReason.Replaced);
        CharacterGuardianshipState replacement = new(
            CharacterGuardianshipContractVersions.State,
            replacementId,
            ward.CharacterId,
            replacementGuardian.CharacterId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
        IReadOnlyList<CharacterGuardianshipState> updated =
        [
            .. ReplaceGuardianship(active.GuardianshipId, ended),
            Clone(replacement),
        ];
        return new CharacterGuardianshipReplacementPlan(
            Clone(ended),
            Clone(replacement),
            CreateUpdatePlan(
                updated,
                resolutionDate,
                authoritativeTurnIndex));
    }

    internal CharacterGuardianshipComingOfAgePlan PrepareComingOfAgeTermination(
        EntityId wardCharacterId,
        EntityId expectedCurrentPrimaryGuardianshipId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex
            || commandId != CharacterComingOfAgeIds.DeriveCommandId(
                resolutionDate,
                wardCharacterId)
            || eventId != CharacterComingOfAgeIds.DeriveEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Coming-of-age guardianship termination coordinates or identities are invalid.");
        }

        AuthoritativeCharacterProfile ward = RequireCurrentCharacter(
            wardCharacterId,
            resolutionDate,
            "Coming-of-age ward");
        if (ward.Condition.VitalStatus != CharacterVitalStatus.Alive
            || !CharacterComingOfAgePlanner.IsComingOfAgeTransition(
                ward.BirthDate,
                resolutionDate))
        {
            throw new SimulationValidationException(
                $"Guardianship ward '{wardCharacterId}' is not living through an exact 17-to-18 transition.");
        }

        CharacterGuardianshipState active = RequireExpectedActiveGuardianship(
            wardCharacterId,
            expectedCurrentPrimaryGuardianshipId);
        CharacterGuardianshipState ended = EndGuardianship(
            active,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            CharacterGuardianshipEndReason.WardCameOfAge);
        return new CharacterGuardianshipComingOfAgePlan(
            Clone(ended),
            CreateUpdatePlan(
                ReplaceGuardianship(active.GuardianshipId, ended),
                resolutionDate,
                authoritativeTurnIndex));
    }

    internal CharacterGuardianshipDeathPlan PrepareCharacterDeath(
        EntityId deadCharacterId,
        IAuthoritativeCharacterWorldQuery candidateCharacters,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        ValidateDeathPreparation(
            deadCharacterId,
            candidateCharacters,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        CharacterGuardianshipState[] ended = guardianships.Values
            .Where(item => item.Status == CharacterGuardianshipStatus.Active
                && (item.WardCharacterId == deadCharacterId
                    || item.GuardianCharacterId == deadCharacterId))
            .Select(item => EndGuardianship(
                item,
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId,
                item.WardCharacterId == deadCharacterId
                    ? CharacterGuardianshipEndReason.WardDied
                    : CharacterGuardianshipEndReason.GuardianDied))
            .OrderBy(item => item.GuardianshipId)
            .Select(Clone)
            .ToArray();
        Dictionary<EntityId, CharacterGuardianshipState> endedById = ended
            .ToDictionary(item => item.GuardianshipId, Clone);
        CharacterGuardianshipState[] updated = guardianships.Values
            .Select(item => endedById.TryGetValue(
                    item.GuardianshipId,
                    out CharacterGuardianshipState? replacement)
                ? Clone(replacement)
                : Clone(item))
            .OrderBy(item => item.GuardianshipId)
            .ToArray();
        CharacterGuardianshipWorldState candidate = new(
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                updated),
            candidateCharacters,
            new CampaignCalendar(
                resolutionDate.CompareTo(calendar.Date) > 0
                    ? resolutionDate
                    : calendar.Date,
                Math.Max(calendar.TurnIndex, authoritativeTurnIndex)));
        return new CharacterGuardianshipDeathPlan(
            Array.AsReadOnly(ended.Select(Clone).ToArray()),
            new CharacterGuardianshipWorldUpdatePlan(candidate));
    }

    internal void ApplyPrepared(CharacterGuardianshipWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-guardianship update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private void ValidateResolutionCoordinates(
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId,
        string operation)
    {
        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship {operation} date or turn is invalid.");
        }

        if (!commandId.IsValid
            || eventId != CharacterFamilyIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                $"Primary-guardianship {operation} command or family-event identity is invalid.");
        }
    }

    private void ValidateDeathPreparation(
        EntityId deadCharacterId,
        IAuthoritativeCharacterWorldQuery candidateCharacters,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (candidateCharacters is null)
        {
            throw new SimulationValidationException(
                "Character-death guardianship preparation requires candidate characters.");
        }

        if (!deadCharacterId.IsValid
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex
            || !commandId.IsValid
            || eventId != CharacterConditionIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Character-death guardianship preparation requires exact current coordinates and condition-event identity.");
        }

        AuthoritativeCharacterProfile current = RequireCharacter(
            deadCharacterId,
            "Character-death guardianship target");
        if (current.Condition.VitalStatus != CharacterVitalStatus.Alive
            || !candidateCharacters.TryGetCharacterProfile(
                deadCharacterId,
                out AuthoritativeCharacterProfile? candidate)
            || candidate.Condition.VitalStatus != CharacterVitalStatus.Dead)
        {
            throw new SimulationValidationException(
                $"Character-death guardianship target '{deadCharacterId}' is stale or lacks an exact dead candidate.");
        }
    }

    private CharacterGuardianshipState RequireExpectedActiveGuardianship(
        EntityId wardCharacterId,
        EntityId expectedCurrentPrimaryGuardianshipId)
    {
        if (!activeGuardianshipByWard.TryGetValue(
                wardCharacterId,
                out EntityId currentId)
            || currentId != expectedCurrentPrimaryGuardianshipId)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship expected-current ID is stale for ward '{wardCharacterId}'.");
        }

        return guardianships[currentId];
    }

    private CharacterGuardianshipWorldUpdatePlan CreateUpdatePlan(
        IReadOnlyList<CharacterGuardianshipState> updated,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0
                ? resolutionDate
                : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        return new CharacterGuardianshipWorldUpdatePlan(
            new CharacterGuardianshipWorldState(
                new CharacterGuardianshipWorldSnapshot(
                    CharacterGuardianshipContractVersions.Snapshot,
                    updated),
                characters,
                candidateCalendar));
    }

    private IReadOnlyList<CharacterGuardianshipState> ReplaceGuardianship(
        EntityId guardianshipId,
        CharacterGuardianshipState replacement) => guardianships.Values
        .Select(item => item.GuardianshipId == guardianshipId
            ? Clone(replacement)
            : Clone(item))
        .ToArray();

    private static CharacterGuardianshipState EndGuardianship(
        CharacterGuardianshipState active,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId,
        CharacterGuardianshipEndReason endReason) => active with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndSourceCommandId = commandId,
            EndSourceEventId = eventId,
            EndReason = endReason,
        };

    private static bool IsEligibleGuardianCondition(CharacterConditionState condition) =>
        condition.VitalStatus == CharacterVitalStatus.Alive
        && !condition.IsIncapacitated
        && condition.CustodyStatus == CharacterCustodyStatus.Free;

    private static void ValidateSnapshotShape(CharacterGuardianshipWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterGuardianshipContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-guardianship snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Guardianships is null
            || snapshot.Guardianships.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Character-guardianship snapshot collection and entries cannot be null.");
        }
    }

    private void AddGuardianships(IReadOnlyList<CharacterGuardianshipState> source)
    {
        Dictionary<EntityId, int> retainedByCharacter = [];
        foreach (CharacterGuardianshipState guardianship in source)
        {
            ValidateGuardianship(guardianship);
            if (!guardianships.TryAdd(
                    guardianship.GuardianshipId,
                    Clone(guardianship)))
            {
                throw new SimulationValidationException(
                    $"Duplicate guardianship '{guardianship.GuardianshipId}'.");
            }

            if (guardianship.Status == CharacterGuardianshipStatus.Active
                && !activeGuardianshipByWard.TryAdd(
                    guardianship.WardCharacterId,
                    guardianship.GuardianshipId))
            {
                throw new SimulationValidationException(
                    $"Ward '{guardianship.WardCharacterId}' has more than one active primary guardianship.");
            }

            AddRetained(guardianship.WardCharacterId);
            AddRetained(guardianship.GuardianCharacterId);
        }

        void AddRetained(EntityId characterId)
        {
            int count = retainedByCharacter.TryGetValue(characterId, out int current)
                ? checked(current + 1)
                : 1;
            if (count > CharacterGuardianshipLimits.RetainedRecordsPerInvolvedCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{characterId}' exceeds the retained guardianship limit of "
                    + $"{CharacterGuardianshipLimits.RetainedRecordsPerInvolvedCharacter}.");
            }

            retainedByCharacter[characterId] = count;
        }
    }

    private void ValidateGuardianship(CharacterGuardianshipState guardianship)
    {
        if (guardianship.ContractVersion != CharacterGuardianshipContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Guardianship '{guardianship.GuardianshipId}' has unsupported contract version {guardianship.ContractVersion}.");
        }

        RequireNamespacedId(
            guardianship.GuardianshipId,
            "guardianship:",
            "Guardianship ID");
        RequireNamespacedId(
            guardianship.SourceCommandId,
            "command:",
            $"Guardianship '{guardianship.GuardianshipId}' source command ID");
        RequireNamespacedId(
            guardianship.SourceEventId,
            "event:",
            $"Guardianship '{guardianship.GuardianshipId}' source event ID");
        if (!Enum.IsDefined(guardianship.Status)
            || guardianship.WardCharacterId == guardianship.GuardianCharacterId)
        {
            throw new SimulationValidationException(
                $"Guardianship '{guardianship.GuardianshipId}' has invalid status or participants.");
        }

        ValidateRecordPoint(
            guardianship.EstablishedDate,
            guardianship.EstablishedTurnIndex,
            $"Guardianship '{guardianship.GuardianshipId}' establishment");
        if (guardianship.SourceEventId != CharacterFamilyIds.DeriveActionEventId(
                guardianship.EstablishedDate,
                guardianship.SourceCommandId)
            || guardianship.GuardianshipId != CharacterGuardianshipIds.DeriveGuardianshipId(
                guardianship.SourceEventId,
                guardianship.WardCharacterId,
                guardianship.GuardianCharacterId))
        {
            throw new SimulationValidationException(
                $"Guardianship '{guardianship.GuardianshipId}' identity does not match its source evidence.");
        }

        AuthoritativeCharacterProfile ward = RequireCharacter(
            guardianship.WardCharacterId,
            $"Guardianship '{guardianship.GuardianshipId}' ward");
        AuthoritativeCharacterProfile guardian = RequireCharacter(
            guardianship.GuardianCharacterId,
            $"Guardianship '{guardianship.GuardianshipId}' guardian");
        if (ward.BirthDate.CompareTo(guardianship.EstablishedDate) > 0
            || guardian.BirthDate.CompareTo(guardianship.EstablishedDate) > 0
            || guardian.BirthDate.CompareTo(ward.BirthDate) >= 0
            || CalculateAge(guardian.BirthDate, guardianship.EstablishedDate) < AdultAge
            || CalculateAge(ward.BirthDate, guardianship.EstablishedDate) >= AdultAge)
        {
            throw new SimulationValidationException(
                $"Guardianship '{guardianship.GuardianshipId}' has invalid historical age or birth evidence.");
        }

        ValidateTerminalFields(guardianship);
    }

    private void ValidateTerminalFields(CharacterGuardianshipState guardianship)
    {
        if (guardianship.Status == CharacterGuardianshipStatus.Active)
        {
            if (guardianship.EndDate is not null
                || guardianship.EndTurnIndex is not null
                || guardianship.EndSourceCommandId is not null
                || guardianship.EndSourceEventId is not null
                || guardianship.EndReason is not null)
            {
                throw new SimulationValidationException(
                    $"Active guardianship '{guardianship.GuardianshipId}' cannot contain terminal data.");
            }

            return;
        }

        if (guardianship.EndDate is not CampaignDate endDate
            || guardianship.EndTurnIndex is not long endTurn
            || guardianship.EndSourceCommandId is not EntityId endCommandId
            || guardianship.EndSourceEventId is not EntityId endEventId
            || guardianship.EndReason is not CharacterGuardianshipEndReason endReason
            || !Enum.IsDefined(endReason)
            || !endDate.IsValid
            || endDate.CompareTo(guardianship.EstablishedDate) < 0
            || endDate.CompareTo(calendar.Date) > 0
            || endTurn < guardianship.EstablishedTurnIndex
            || endTurn > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Ended guardianship '{guardianship.GuardianshipId}' lacks coherent terminal data.");
        }

        RequireNamespacedId(
            endCommandId,
            "command:",
            $"Guardianship '{guardianship.GuardianshipId}' terminal command ID");
        RequireNamespacedId(
            endEventId,
            "event:",
            $"Guardianship '{guardianship.GuardianshipId}' terminal event ID");
    }

    private void ValidateRecordPoint(
        CampaignDate date,
        long turnIndex,
        string description)
    {
        if (!date.IsValid
            || date.CompareTo(calendar.Date) > 0
            || turnIndex < 0
            || turnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"{description} date or turn is invalid.");
        }
    }

    private void EnforceRetainedCapacity(EntityId characterId, string role)
    {
        int count = guardianships.Values.Count(item =>
            item.WardCharacterId == characterId
            || item.GuardianCharacterId == characterId);
        if (count >= CharacterGuardianshipLimits.RetainedRecordsPerInvolvedCharacter)
        {
            throw new SimulationValidationException(
                $"Primary-guardianship {role} '{characterId}' is at the retained-record limit.");
        }
    }

    private AuthoritativeCharacterProfile RequireCurrentCharacter(
        EntityId characterId,
        CampaignDate resolutionDate,
        string label)
    {
        AuthoritativeCharacterProfile profile = RequireCharacter(characterId, label);
        if (profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' is not born by '{resolutionDate}'.");
        }

        return profile;
    }

    private AuthoritativeCharacterProfile RequireCharacter(
        EntityId characterId,
        string label)
    {
        if (!characterId.IsValid
            || !characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            throw new SimulationValidationException(
                $"{label} '{characterId}' does not exist.");
        }

        return profile;
    }

    private static void RequireNamespacedId(
        EntityId id,
        string requiredPrefix,
        string label)
    {
        if (!id.IsValid || !id.Value.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            throw new SimulationValidationException(
                $"{label} '{id}' must use the '{requiredPrefix}' namespace.");
        }
    }

    private static int CalculateAge(CampaignDate birthDate, CampaignDate currentDate)
    {
        int age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month
            || (currentDate.Month == birthDate.Month
                && currentDate.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private void ReplaceFrom(CharacterGuardianshipWorldState source)
    {
        guardianships.Clear();
        activeGuardianshipByWard.Clear();
        foreach (CharacterGuardianshipState guardianship in source.guardianships.Values)
        {
            guardianships.Add(guardianship.GuardianshipId, Clone(guardianship));
            if (guardianship.Status == CharacterGuardianshipStatus.Active)
            {
                activeGuardianshipByWard.Add(
                    guardianship.WardCharacterId,
                    guardianship.GuardianshipId);
            }
        }

        calendar = source.calendar;
    }

    private static CharacterGuardianshipState Clone(CharacterGuardianshipState value) =>
        value with { };
}

internal sealed record CharacterGuardianshipWorldUpdatePlan(
    CharacterGuardianshipWorldState Candidate);

internal sealed record CharacterGuardianshipEstablishmentPlan(
    CharacterGuardianshipState Guardianship,
    CharacterGuardianshipWorldUpdatePlan GuardianshipPlan);

internal sealed record CharacterGuardianshipTerminationPlan(
    CharacterGuardianshipState EndedGuardianship,
    CharacterGuardianshipWorldUpdatePlan GuardianshipPlan);

internal sealed record CharacterGuardianshipReplacementPlan(
    CharacterGuardianshipState EndedGuardianship,
    CharacterGuardianshipState ReplacementGuardianship,
    CharacterGuardianshipWorldUpdatePlan GuardianshipPlan);

internal sealed record CharacterGuardianshipComingOfAgePlan(
    CharacterGuardianshipState EndedPrimaryGuardianship,
    CharacterGuardianshipWorldUpdatePlan GuardianshipPlan);

internal sealed record CharacterGuardianshipDeathPlan(
    IReadOnlyList<CharacterGuardianshipState> EndedGuardianships,
    CharacterGuardianshipWorldUpdatePlan GuardianshipPlan);
