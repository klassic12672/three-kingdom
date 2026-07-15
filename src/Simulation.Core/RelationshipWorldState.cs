using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Simulation.Core;

public sealed class RelationshipWorldState : IAuthoritativeRelationshipWorldQuery
{
    private const int DetailedRelationshipLimit = 64;
    private const int ArchivedRelationshipLimit = 128;
    private const int MemoryLimit = 16;

    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, SubjectRelationshipHistory> subjects = [];
    private readonly HashSet<EntityId> retainedMemoryIds = [];
    private readonly HashSet<MemorySourceIdentity> retainedSourceIdentities = [];

    public RelationshipWorldState(
        RelationshipWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException("Relationship-world snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException("Authoritative character query cannot be null.");
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException("Relationship-world campaign calendar is invalid.");
        }

        ValidateSnapshotShape(snapshot);
        HashSet<EntityId> memoryIds = [];
        HashSet<MemorySourceIdentity> sourceIdentities = [];
        foreach (SubjectRelationshipHistory source in snapshot.Subjects)
        {
            SubjectRelationshipHistory canonical = ValidateAndCloneSubject(
                source,
                calendar,
                memoryIds,
                sourceIdentities);
            if (!subjects.TryAdd(canonical.SubjectCharacterId, canonical))
            {
                throw new SimulationValidationException(
                    $"Duplicate relationship history for subject '{canonical.SubjectCharacterId}'.");
            }
        }

        retainedMemoryIds.UnionWith(memoryIds);
        retainedSourceIdentities.UnionWith(sourceIdentities);
    }

    public IReadOnlyList<SubjectRelationshipHistory> Subjects =>
        subjects.Values.Select(Clone).ToArray();

    public bool TryGetSubjectHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SubjectRelationshipHistory? history)
    {
        if (subjects.TryGetValue(subjectCharacterId, out SubjectRelationshipHistory? stored))
        {
            history = Clone(stored);
            return true;
        }

        history = null;
        return false;
    }

    public RelationshipWorldSnapshot CaptureSnapshot() => new(
        RelationshipContractVersions.Snapshot,
        subjects.Values.Select(Clone).ToArray());

    internal CommandValidationResult ValidateAction(
        EntityId subjectCharacterId,
        RelationshipActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex) => ValidateActionCore(
            subjectCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            validateResultBounds: true);

    private CommandValidationResult ValidateActionCore(
        EntityId subjectCharacterId,
        RelationshipActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        bool validateResultBounds,
        bool requireNonZeroImpact = true)
    {
        List<ValidationIssue> issues = [];
        if (!subjectCharacterId.IsValid)
        {
            issues.Add(new("invalid_subject", "Relationship subject ID is invalid."));
        }

        if (!resolutionDate.IsValid)
        {
            issues.Add(new("invalid_resolution_date", "Relationship resolution date is invalid."));
        }

        if (authoritativeTurnIndex < 0)
        {
            issues.Add(new("invalid_turn_index", "Relationship turn index cannot be negative."));
        }

        if (payload is null)
        {
            issues.Add(new("invalid_payload", "Relationship action payload cannot be null."));
            return new(false, issues);
        }

        if (payload.Impact is null)
        {
            issues.Add(new("invalid_impact", "Relationship impact cannot be null."));
        }

        if (!payload.TargetCharacterId.IsValid)
        {
            issues.Add(new("invalid_target", "Relationship target ID is invalid."));
        }

        if (!payload.MeaningId.IsValid)
        {
            issues.Add(new("invalid_meaning", "Relationship memory meaning ID is invalid."));
        }

        AuthoritativeCharacterProfile? subject = null;
        AuthoritativeCharacterProfile? target = null;
        bool subjectExists = subjectCharacterId.IsValid
            && characters.TryGetCharacterProfile(subjectCharacterId, out subject);
        bool targetExists = payload.TargetCharacterId.IsValid
            && characters.TryGetCharacterProfile(payload.TargetCharacterId, out target);
        if (!subjectExists)
        {
            issues.Add(new("unknown_subject", $"Relationship subject '{subjectCharacterId}' does not exist."));
        }

        if (!targetExists)
        {
            issues.Add(new("unknown_target", $"Relationship target '{payload.TargetCharacterId}' does not exist."));
        }

        if (subjectCharacterId == payload.TargetCharacterId)
        {
            issues.Add(new("self_relationship", "Relationship subject and target must be different characters."));
        }

        if (requireNonZeroImpact && payload.Impact is not null && !payload.Impact.HasAnyChange)
        {
            issues.Add(new("empty_impact", "A relationship action requires at least one non-zero impact."));
        }

        if (payload.Impact is not null && !ImpactCanRepresentBoundedTransition(payload.Impact))
        {
            issues.Add(new(
                "invalid_impact_delta",
                "Relationship impact contains a delta that cannot occur between supported bounded states."));
        }

        if (payload.InitialSeverity is < 1 or > 100)
        {
            issues.Add(new("invalid_severity", "Relationship memory severity must be from 1 through 100."));
        }

        if (!Enum.IsDefined(payload.Publicity))
        {
            issues.Add(new("invalid_publicity", "Relationship memory publicity is invalid."));
        }

        if (payload.DecayIntervalTurns < 0)
        {
            issues.Add(new("invalid_decay", "Relationship memory decay interval cannot be negative."));
        }

        ValidateWitnesses(payload, subjectCharacterId, resolutionDate, issues);

        bool participantsBorn = subjectExists
            && targetExists
            && resolutionDate.IsValid
            && subject!.BirthDate.CompareTo(resolutionDate) <= 0
            && target!.BirthDate.CompareTo(resolutionDate) <= 0;
        if (subjectExists && targetExists && resolutionDate.IsValid && !participantsBorn)
        {
            issues.Add(new(
                "participant_not_born",
                "Relationship participants must be born by the authoritative resolution date."));
        }

        if (payload.Impact is not null)
        {
            if (participantsBorn
                && payload.Impact.Attraction != 0
                && (CalculateAge(subject!.BirthDate, resolutionDate) < 18
                    || CalculateAge(target!.BirthDate, resolutionDate) < 18))
            {
                issues.Add(new(
                    "underage_attraction",
                    "A non-zero attraction impact requires both characters to be at least 18 on the resolution date."));
            }

            if (validateResultBounds)
            {
                RelationshipDimensions current = GetCurrentDimensions(subjectCharacterId, payload.TargetCharacterId);
                try
                {
                    RelationshipDimensions result = ApplyImpact(current, payload.Impact);
                    if (!DimensionsAreInRange(result))
                    {
                        issues.Add(new(
                            "relationship_bounds",
                            "Relationship impact would produce a result outside the supported dimension bounds."));
                    }
                }
                catch (OverflowException)
                {
                    issues.Add(new(
                        "numeric_overflow",
                        "Relationship impact exceeds the supported numeric range."));
                }
            }
        }

        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    internal RelationshipActionResolvedEventPayload PlanAction(
        EntityId subjectCharacterId,
        RelationshipActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        CommandValidationResult validation = ValidateAction(
            subjectCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex);
        if (!validation.IsValid)
        {
            throw new SimulationValidationException(
                string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }

        ValidateId(commandId, "Relationship action command ID");
        ValidateId(eventId, "Relationship action event ID");
        EntityId expectedEventId = DeriveEventId(resolutionDate, commandId);
        if (eventId != expectedEventId)
        {
            throw new SimulationValidationException(
                $"Relationship action event ID '{eventId}' does not match command '{commandId}'.");
        }

        EntityId relationshipId = RelationshipIds.DeriveRelationshipId(
            subjectCharacterId,
            payload.TargetCharacterId);
        ConsequentialMemory memory = new(
            RelationshipContractVersions.Memory,
            RelationshipIds.DeriveMemoryId(resolutionDate, commandId),
            subjectCharacterId,
            payload.TargetCharacterId,
            payload.WitnessIds.ToArray(),
            resolutionDate,
            authoritativeTurnIndex,
            payload.MeaningId,
            payload.InitialSeverity,
            payload.Publicity,
            payload.DecayIntervalTurns,
            payload.Impact with { },
            eventId,
            RelationshipMemorySourceKind.RelationshipAction,
            RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1,
            0);
        return new RelationshipActionResolvedEventPayload(
            relationshipId,
            subjectCharacterId,
            payload.TargetCharacterId,
            memory);
    }

    internal void Apply(CampaignEvent campaignEvent, long authoritativeTurnIndex)
    {
        if (campaignEvent is null)
        {
            throw new SimulationValidationException("Relationship action event cannot be null.");
        }

        if (campaignEvent.Payload is not RelationshipActionResolvedEventPayload payload)
        {
            throw new SimulationValidationException(
                $"Event payload '{campaignEvent.Payload?.GetType().Name ?? "null"}' is not a relationship action result.");
        }

        ValidateResolvedEvent(campaignEvent, payload, authoritativeTurnIndex);
        ConsequentialMemory memory = payload.Memory;
        MemorySourceIdentity sourceIdentity = GetSourceIdentity(memory);
        if (retainedMemoryIds.Contains(memory.MemoryId)
            || retainedSourceIdentities.Contains(sourceIdentity))
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' or its source event already exists.");
        }

        SubjectRelationshipHistory current = subjects.TryGetValue(
            payload.SubjectCharacterId,
            out SubjectRelationshipHistory? stored)
            ? Clone(stored)
            : new SubjectRelationshipHistory(
                RelationshipContractVersions.State,
                payload.SubjectCharacterId,
                [],
                [],
                DistantRelationshipHistoryAggregate.Empty);

        try
        {
            SubjectRelationshipHistory updated = ApplyToSubject(
                current,
                payload,
                campaignEvent.ResolutionDate,
                authoritativeTurnIndex);
            foreach (ConsequentialMemory previous in current.DetailedRelationships
                .SelectMany(relationship => relationship.Memories))
            {
                retainedMemoryIds.Remove(previous.MemoryId);
                retainedSourceIdentities.Remove(GetSourceIdentity(previous));
            }

            foreach (ConsequentialMemory retained in updated.DetailedRelationships
                .SelectMany(relationship => relationship.Memories))
            {
                retainedMemoryIds.Add(retained.MemoryId);
                retainedSourceIdentities.Add(GetSourceIdentity(retained));
            }

            subjects[payload.SubjectCharacterId] = updated;
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Relationship history for '{payload.SubjectCharacterId}' exceeded its supported counters: {exception.Message}");
        }
    }

    internal RelationshipWorldUpdatePlan PrepareCharacterActionConsequences(
        CharacterActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId eventId)
    {
        if (payload is null || payload.RelationshipMemoryConsequences is null)
        {
            throw new SimulationValidationException(
                "Character action relationship consequences cannot be null.");
        }

        ValidateId(eventId, "Character action event ID");
        if (!resolutionDate.IsValid || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character action relationship consequence date or turn is invalid.");
        }

        for (int index = 0; index < payload.RelationshipMemoryConsequences.Count; index++)
        {
            RelationshipMemoryConsequenceSpecification specification =
                payload.RelationshipMemoryConsequences[index]
                ?? throw new SimulationValidationException(
                    "Character action relationship consequences cannot contain null entries.");
            if (specification.ContractVersion != CareerContractVersions.RelationshipConsequence
                || specification.ConsequenceId
                    != CareerIds.DeriveRelationshipConsequenceId(eventId, index))
            {
                throw new SimulationValidationException(
                    $"Character action relationship consequence {index} has an unsupported contract or deterministic identity.");
            }

        }

        return PrepareSourceEventConsequences(
            payload.RelationshipMemoryConsequences,
            RelationshipMemorySourceKind.CharacterAction,
            (sourceEventId, index) => CareerIds.DeriveRelationshipConsequenceId(
                sourceEventId,
                index),
            resolutionDate,
            authoritativeTurnIndex,
            eventId,
            "Character action");
    }

    internal RelationshipMemoryConsequenceSpecification PlanHarmfulConsequence(
        EntityId eventId,
        EntityId consequenceId,
        EntityId subjectCharacterId,
        EntityId targetCharacterId,
        EntityId meaningId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        RelationshipDimensions current = GetCurrentDimensions(
            subjectCharacterId,
            targetCharacterId);
        RelationshipImpact impact = CreateBoundedHarmfulImpact(current);
        RelationshipMemoryConsequenceSpecification consequence = new(
            RelationshipContractVersions.Consequence,
            consequenceId,
            subjectCharacterId,
            targetCharacterId,
            impact,
            meaningId,
            75,
            MemoryPublicity.Participants,
            0,
            []);
        ValidateHarmfulConsequence(
            consequence,
            subjectCharacterId,
            targetCharacterId,
            resolutionDate,
            authoritativeTurnIndex);
        ValidateId(eventId, "Harmful relationship consequence event ID");
        return Clone(consequence);
    }

    internal RelationshipWorldUpdatePlan PrepareHouseholdDecisionConsequence(
        HouseholdDecisionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId eventId)
    {
        if (payload?.RelationshipMemoryConsequence is null)
        {
            throw new SimulationValidationException(
                "Household decision requires one harmful relationship consequence.");
        }

        return PrepareSourceEventConsequences(
            [payload.RelationshipMemoryConsequence],
            RelationshipMemorySourceKind.HouseholdDecision,
            (sourceEventId, index) => HouseholdDecisionIds.DeriveRelationshipConsequenceId(
                sourceEventId,
                index),
            resolutionDate,
            authoritativeTurnIndex,
            eventId,
            "Household decision",
            requireHarmful: true);
    }

    internal RelationshipWorldUpdatePlan PrepareCharacterMarriageConsequence(
        CharacterMarriageActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId eventId)
    {
        if (payload?.RelationshipMemoryConsequence is null)
        {
            throw new SimulationValidationException(
                "Coerced character-marriage action requires one harmful relationship consequence.");
        }

        return PrepareSourceEventConsequences(
            [payload.RelationshipMemoryConsequence],
            RelationshipMemorySourceKind.CharacterMarriageAction,
            (sourceEventId, index) => CharacterMarriageIds.DeriveRelationshipConsequenceId(
                sourceEventId,
                index),
            resolutionDate,
            authoritativeTurnIndex,
            eventId,
            "Coerced character-marriage action",
            requireHarmful: true);
    }

    internal RelationshipWorldUpdatePlan PrepareCharacterConditionConsequence(
        CharacterConditionActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId eventId)
    {
        if (payload?.Action is not EnterCharacterCustodyAction
            || payload.RelationshipMemoryConsequence is null)
        {
            throw new SimulationValidationException(
                "Custody entry requires one harmful relationship consequence.");
        }

        return PrepareSourceEventConsequences(
            [payload.RelationshipMemoryConsequence],
            RelationshipMemorySourceKind.CharacterCondition,
            (sourceEventId, index) => CharacterConditionIds.DeriveRelationshipConsequenceId(
                sourceEventId,
                index),
            resolutionDate,
            authoritativeTurnIndex,
            eventId,
            "Character custody entry",
            requireHarmful: true);
    }

    private RelationshipWorldUpdatePlan PrepareSourceEventConsequences(
        IReadOnlyList<RelationshipMemoryConsequenceSpecification> specifications,
        RelationshipMemorySourceKind sourceKind,
        Func<EntityId, int, EntityId> deriveConsequenceId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId eventId,
        string description,
        bool requireHarmful = false)
    {
        if (specifications is null
            || specifications.Any(item => item is null)
            || specifications.Count > RelationshipLimits.ConsequencesPerSourceEvent)
        {
            throw new SimulationValidationException(
                $"{description} relationship consequences are null or exceed the supported limit.");
        }

        if (!Enum.IsDefined(sourceKind)
            || sourceKind is RelationshipMemorySourceKind.RelationshipAction)
        {
            throw new SimulationValidationException(
                $"{description} relationship consequence source kind is invalid.");
        }

        ValidateId(eventId, $"{description} event ID");
        if (!resolutionDate.IsValid || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException(
                $"{description} relationship consequence date or turn is invalid.");
        }

        RelationshipWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            new CampaignCalendar(resolutionDate, authoritativeTurnIndex));
        for (int index = 0; index < specifications.Count; index++)
        {
            RelationshipMemoryConsequenceSpecification specification = specifications[index];
            if (specification.ContractVersion != RelationshipContractVersions.Consequence
                || specification.ConsequenceId != deriveConsequenceId(eventId, index))
            {
                throw new SimulationValidationException(
                    $"{description} relationship consequence {index} has an unsupported contract or deterministic identity.");
            }

            if (requireHarmful)
            {
                ValidateHarmfulConsequence(
                    specification,
                    specification.SubjectCharacterId,
                    specification.TargetCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex);
            }

            ConsequentialMemory memory = new(
                RelationshipContractVersions.Memory,
                RelationshipIds.DeriveMemoryId(
                    eventId,
                    specification.SubjectCharacterId,
                    specification.TargetCharacterId,
                    index),
                specification.SubjectCharacterId,
                specification.TargetCharacterId,
                specification.WitnessIds?.ToArray()
                    ?? throw new SimulationValidationException(
                        $"{description} relationship consequence witnesses cannot be null."),
                resolutionDate,
                authoritativeTurnIndex,
                specification.MeaningId,
                specification.InitialSeverity,
                specification.Publicity,
                specification.DecayIntervalTurns,
                specification.Impact is null
                    ? throw new SimulationValidationException(
                        $"{description} relationship consequence impact cannot be null.")
                    : specification.Impact with { },
                eventId,
                sourceKind,
                RelationshipMemoryIdentityScheme.SourceEventV2,
                index);
            candidate.ApplyGenericMemory(memory, resolutionDate, authoritativeTurnIndex);
        }

        return new RelationshipWorldUpdatePlan(candidate);
    }

    internal void ApplyPrepared(RelationshipWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException("Prepared relationship update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private void ApplyGenericMemory(
        ConsequentialMemory memory,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        ValidateCurrentMemory(memory, validateResultBounds: true);
        MemorySourceIdentity sourceIdentity = GetSourceIdentity(memory);
        if (retainedMemoryIds.Contains(memory.MemoryId)
            || retainedSourceIdentities.Contains(sourceIdentity))
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' or its source consequence already exists.");
        }

        EntityId relationshipId = RelationshipIds.DeriveRelationshipId(
            memory.SubjectCharacterId,
            memory.TargetCharacterId);
        RelationshipActionResolvedEventPayload wrapper = new(
            relationshipId,
            memory.SubjectCharacterId,
            memory.TargetCharacterId,
            memory);
        SubjectRelationshipHistory current = subjects.TryGetValue(
            memory.SubjectCharacterId,
            out SubjectRelationshipHistory? stored)
            ? Clone(stored)
            : new SubjectRelationshipHistory(
                RelationshipContractVersions.State,
                memory.SubjectCharacterId,
                [],
                [],
                DistantRelationshipHistoryAggregate.Empty);
        try
        {
            SubjectRelationshipHistory updated = ApplyToSubject(
                current,
                wrapper,
                resolutionDate,
                authoritativeTurnIndex);
            foreach (ConsequentialMemory previous in current.DetailedRelationships
                .SelectMany(relationship => relationship.Memories))
            {
                retainedMemoryIds.Remove(previous.MemoryId);
                retainedSourceIdentities.Remove(GetSourceIdentity(previous));
            }

            foreach (ConsequentialMemory retained in updated.DetailedRelationships
                .SelectMany(relationship => relationship.Memories))
            {
                retainedMemoryIds.Add(retained.MemoryId);
                retainedSourceIdentities.Add(GetSourceIdentity(retained));
            }

            subjects[memory.SubjectCharacterId] = updated;
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Relationship history for '{memory.SubjectCharacterId}' exceeded its supported counters: {exception.Message}");
        }
    }

    public static int GetEffectiveSeverity(ConsequentialMemory memory, long authoritativeTurnIndex)
    {
        if (memory is null)
        {
            throw new SimulationValidationException("Relationship memory cannot be null.");
        }

        if (authoritativeTurnIndex < memory.RecordedTurnIndex)
        {
            throw new SimulationValidationException(
                "Effective memory severity cannot be derived before its recorded turn.");
        }

        if (memory.DecayIntervalTurns == 0)
        {
            return memory.InitialSeverity;
        }

        long completedIntervals = (authoritativeTurnIndex - memory.RecordedTurnIndex)
            / memory.DecayIntervalTurns;
        return completedIntervals >= memory.InitialSeverity
            ? 0
            : memory.InitialSeverity - (int)completedIntervals;
    }

    private SubjectRelationshipHistory ApplyToSubject(
        SubjectRelationshipHistory current,
        RelationshipActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<DetailedDirectionalRelationship> detailed = current.DetailedRelationships
            .Select(Clone)
            .ToList();
        List<ArchivedDirectionalRelationshipSummary> archived = current.ArchivedRelationships
            .Select(Clone)
            .ToList();
        DetailedDirectionalRelationship? existing = detailed
            .SingleOrDefault(item => item.RelationshipId == payload.RelationshipId);
        ArchivedDirectionalRelationshipSummary? reactivated = archived
            .SingleOrDefault(item => item.RelationshipId == payload.RelationshipId);
        if (existing is not null)
        {
            detailed.Remove(existing);
        }

        if (reactivated is not null)
        {
            archived.Remove(reactivated);
        }

        RelationshipDimensions dimensions = existing?.Dimensions
            ?? reactivated?.Dimensions
            ?? RelationshipDimensions.Zero;
        FoldedMemorySummary folded = existing?.FoldedMemories
            ?? reactivated?.FoldedMemories
            ?? FoldedMemorySummary.Empty;
        List<ConsequentialMemory> memories = existing?.Memories.Select(Clone).ToList() ?? [];
        dimensions = ApplyImpact(dimensions, payload.Memory.AppliedImpact);
        memories.Add(Clone(payload.Memory));
        memories = OrderMemories(memories, authoritativeTurnIndex).ToList();
        while (memories.Count > MemoryLimit)
        {
            ConsequentialMemory evicted = memories[^1];
            memories.RemoveAt(memories.Count - 1);
            folded = AddFoldedMemory(
                folded,
                GetEffectiveSeverity(evicted, authoritativeTurnIndex),
                evicted.ResolutionDate);
        }

        long importance = CalculateImportance(dimensions, memories, authoritativeTurnIndex);
        detailed.Add(new DetailedDirectionalRelationship(
            RelationshipContractVersions.State,
            payload.RelationshipId,
            payload.SubjectCharacterId,
            payload.TargetCharacterId,
            dimensions,
            importance,
            resolutionDate,
            authoritativeTurnIndex,
            memories.Select(Clone).ToArray(),
            Clone(folded)));
        detailed = OrderDetailed(detailed).ToList();

        while (detailed.Count > DetailedRelationshipLimit)
        {
            DetailedDirectionalRelationship evicted = detailed[^1];
            detailed.RemoveAt(detailed.Count - 1);
            FoldedMemorySummary allMemories = Clone(evicted.FoldedMemories);
            foreach (ConsequentialMemory evictedMemory in evicted.Memories)
            {
                allMemories = AddFoldedMemory(
                    allMemories,
                    GetEffectiveSeverity(evictedMemory, authoritativeTurnIndex),
                    evictedMemory.ResolutionDate);
            }

            archived.Add(new ArchivedDirectionalRelationshipSummary(
                RelationshipContractVersions.State,
                evicted.RelationshipId,
                evicted.SubjectCharacterId,
                evicted.TargetCharacterId,
                Clone(evicted.Dimensions),
                evicted.RecordedImportance,
                evicted.LastChangeDate,
                evicted.LastChangeTurnIndex,
                allMemories));
        }

        archived = OrderArchived(archived).ToList();
        DistantRelationshipHistoryAggregate distant = Clone(current.DistantHistory);
        while (archived.Count > ArchivedRelationshipLimit)
        {
            ArchivedDirectionalRelationshipSummary evicted = archived[^1];
            archived.RemoveAt(archived.Count - 1);
            distant = AddDistantHistory(distant, evicted);
        }

        return new SubjectRelationshipHistory(
            RelationshipContractVersions.State,
            current.SubjectCharacterId,
            detailed.Select(Clone).ToArray(),
            archived.Select(Clone).ToArray(),
            distant);
    }

    private void ValidateResolvedEvent(
        CampaignEvent campaignEvent,
        RelationshipActionResolvedEventPayload payload,
        long authoritativeTurnIndex)
    {
        if (campaignEvent.ContractVersion != ContractVersions.CampaignEvent)
        {
            throw new SimulationValidationException(
                $"Unsupported campaign event contract version {campaignEvent.ContractVersion}.");
        }

        ValidateId(campaignEvent.EventId, "Relationship action event ID");
        if (campaignEvent.CausalId is not EntityId commandId || !commandId.IsValid)
        {
            throw new SimulationValidationException("Relationship action event requires a valid causal command ID.");
        }

        if (campaignEvent.EventId != DeriveEventId(campaignEvent.ResolutionDate, commandId))
        {
            throw new SimulationValidationException(
                "Relationship action event ID does not match its causal command ID.");
        }

        if (!campaignEvent.ResolutionDate.IsValid || authoritativeTurnIndex < 0)
        {
            throw new SimulationValidationException("Relationship action event date or turn is invalid.");
        }

        ConsequentialMemory memory = payload.Memory
            ?? throw new SimulationValidationException("Relationship action event memory cannot be null.");
        if (!payload.SubjectCharacterId.IsValid
            || !payload.TargetCharacterId.IsValid
            || payload.SubjectCharacterId == payload.TargetCharacterId)
        {
            throw new SimulationValidationException(
                "Relationship action event requires different valid subject and target character IDs.");
        }

        EntityId expectedRelationshipId = RelationshipIds.DeriveRelationshipId(
            payload.SubjectCharacterId,
            payload.TargetCharacterId);
        if (payload.RelationshipId != expectedRelationshipId
            || memory.MemoryId != RelationshipIds.DeriveMemoryId(campaignEvent.ResolutionDate, commandId)
            || memory.SourceEventId != campaignEvent.EventId
            || memory.SourceKind != RelationshipMemorySourceKind.RelationshipAction
            || memory.IdentityScheme != RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1
            || memory.ConsequenceIndex != 0
            || memory.SubjectCharacterId != payload.SubjectCharacterId
            || memory.TargetCharacterId != payload.TargetCharacterId
            || memory.ResolutionDate != campaignEvent.ResolutionDate
            || memory.RecordedTurnIndex != authoritativeTurnIndex)
        {
            throw new SimulationValidationException(
                "Relationship action event has inconsistent relationship, memory, date, turn, or source causality.");
        }

        EntityId[] expectedAffected =
        [
            payload.SubjectCharacterId,
            payload.TargetCharacterId,
            payload.RelationshipId,
            memory.MemoryId,
        ];
        expectedAffected = expectedAffected.Distinct().Order().ToArray();
        if (campaignEvent.AffectedIds is null
            || !campaignEvent.AffectedIds.SequenceEqual(expectedAffected))
        {
            throw new SimulationValidationException(
                "Relationship action event affected IDs do not match its exact consequences.");
        }

        RelationshipActionCommandPayload action = new(
            payload.TargetCharacterId,
            memory.AppliedImpact,
            memory.MeaningId,
            memory.InitialSeverity,
            memory.Publicity,
            memory.DecayIntervalTurns,
            memory.WitnessIds);
        CommandValidationResult validation = ValidateAction(
            payload.SubjectCharacterId,
            action,
            campaignEvent.ResolutionDate,
            authoritativeTurnIndex);
        if (!validation.IsValid)
        {
            throw new SimulationValidationException(
                string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }

        ValidateCurrentMemory(memory, validateResultBounds: true);
    }

    private SubjectRelationshipHistory ValidateAndCloneSubject(
        SubjectRelationshipHistory subject,
        CampaignCalendar calendar,
        ISet<EntityId> memoryIds,
        ISet<MemorySourceIdentity> sourceIdentities)
    {
        if (subject is null)
        {
            throw new SimulationValidationException("Relationship subject history cannot be null.");
        }

        if (subject.ContractVersion != RelationshipContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Unsupported relationship subject contract version {subject.ContractVersion}.");
        }

        ValidateCharacter(subject.SubjectCharacterId, "Relationship subject");
        if (subject.DetailedRelationships is null
            || subject.ArchivedRelationships is null
            || subject.DistantHistory is null
            || subject.DetailedRelationships.Any(item => item is null)
            || subject.ArchivedRelationships.Any(item => item is null))
        {
            throw new SimulationValidationException(
                $"Relationship history for '{subject.SubjectCharacterId}' has null state.");
        }

        if (subject.DetailedRelationships.Count > DetailedRelationshipLimit
            || subject.ArchivedRelationships.Count > ArchivedRelationshipLimit)
        {
            throw new SimulationValidationException(
                $"Relationship history for '{subject.SubjectCharacterId}' exceeds its bounded record limits.");
        }

        HashSet<EntityId> relationshipIds = [];
        HashSet<EntityId> targets = [];
        List<DetailedDirectionalRelationship> detailed = [];
        foreach (DetailedDirectionalRelationship relationship in subject.DetailedRelationships)
        {
            detailed.Add(ValidateAndCloneDetailed(
                subject.SubjectCharacterId,
                relationship,
                calendar,
                relationshipIds,
                targets,
                memoryIds,
                sourceIdentities));
        }

        List<ArchivedDirectionalRelationshipSummary> archived = [];
        foreach (ArchivedDirectionalRelationshipSummary relationship in subject.ArchivedRelationships)
        {
            archived.Add(ValidateAndCloneArchived(
                subject.SubjectCharacterId,
                relationship,
                calendar,
                relationshipIds,
                targets));
        }

        _ = characters.TryGetCharacterProfile(
            subject.SubjectCharacterId,
            out AuthoritativeCharacterProfile? subjectProfile);
        ValidateDistantHistory(subject.DistantHistory, calendar, subjectProfile!.BirthDate);
        if (detailed.Count == 0
            && archived.Count == 0
            && subject.DistantHistory == DistantRelationshipHistoryAggregate.Empty)
        {
            throw new SimulationValidationException(
                $"Relationship history for '{subject.SubjectCharacterId}' cannot be empty.");
        }

        return new SubjectRelationshipHistory(
            RelationshipContractVersions.State,
            subject.SubjectCharacterId,
            OrderDetailed(detailed).Select(Clone).ToArray(),
            OrderArchived(archived).Select(Clone).ToArray(),
            Clone(subject.DistantHistory));
    }

    private DetailedDirectionalRelationship ValidateAndCloneDetailed(
        EntityId subjectCharacterId,
        DetailedDirectionalRelationship relationship,
        CampaignCalendar calendar,
        ISet<EntityId> relationshipIds,
        ISet<EntityId> targets,
        ISet<EntityId> memoryIds,
        ISet<MemorySourceIdentity> sourceIdentities)
    {
        ValidateRelationshipIdentity(
            subjectCharacterId,
            relationship.ContractVersion,
            relationship.RelationshipId,
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationshipIds,
            targets);
        ValidateDimensions(relationship.Dimensions, relationship.RelationshipId);
        ValidateChangePoint(
            relationship.LastChangeDate,
            relationship.LastChangeTurnIndex,
            calendar,
            relationship.RelationshipId);
        ValidateParticipantsAtChange(
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationship.Dimensions,
            relationship.LastChangeDate,
            relationship.RelationshipId);
        ValidateFoldedSummary(relationship.FoldedMemories, calendar.Date, "Folded memory summary");
        ValidateFoldedParticipantTimeline(
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationship.LastChangeDate,
            relationship.FoldedMemories,
            relationship.RelationshipId);
        if (relationship.Memories is null
            || relationship.Memories.Count > MemoryLimit
            || relationship.Memories.Any(item => item is null))
        {
            throw new SimulationValidationException(
                $"Detailed relationship '{relationship.RelationshipId}' has invalid retained memories.");
        }

        List<ConsequentialMemory> memories = [];
        foreach (ConsequentialMemory memory in relationship.Memories)
        {
            memories.Add(ValidateAndCloneMemory(
                relationship,
                memory,
                calendar,
                memoryIds,
                sourceIdentities));
        }

        if (memories.Count == 0)
        {
            throw new SimulationValidationException(
                $"Detailed relationship '{relationship.RelationshipId}' has no retained consequential memory.");
        }

        memories = OrderMemories(memories, relationship.LastChangeTurnIndex).ToList();
        CampaignDate newestMemoryDate = memories.Max(memory => memory.ResolutionDate);
        if (relationship.FoldedMemories.LatestDate is CampaignDate foldedLatest)
        {
            newestMemoryDate = Max(newestMemoryDate, foldedLatest);
        }

        if (newestMemoryDate != relationship.LastChangeDate)
        {
            throw new SimulationValidationException(
                $"Detailed relationship '{relationship.RelationshipId}' has no memory at its last change date.");
        }

        long expectedImportance = CalculateImportance(
            relationship.Dimensions,
            memories,
            relationship.LastChangeTurnIndex);
        if (relationship.RecordedImportance != expectedImportance)
        {
            throw new SimulationValidationException(
                $"Detailed relationship '{relationship.RelationshipId}' has inconsistent recorded importance.");
        }

        return relationship with
        {
            Dimensions = Clone(relationship.Dimensions),
            Memories = memories.Select(Clone).ToArray(),
            FoldedMemories = Clone(relationship.FoldedMemories),
        };
    }

    private ArchivedDirectionalRelationshipSummary ValidateAndCloneArchived(
        EntityId subjectCharacterId,
        ArchivedDirectionalRelationshipSummary relationship,
        CampaignCalendar calendar,
        ISet<EntityId> relationshipIds,
        ISet<EntityId> targets)
    {
        ValidateRelationshipIdentity(
            subjectCharacterId,
            relationship.ContractVersion,
            relationship.RelationshipId,
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationshipIds,
            targets);
        ValidateDimensions(relationship.Dimensions, relationship.RelationshipId);
        ValidateChangePoint(
            relationship.LastChangeDate,
            relationship.LastChangeTurnIndex,
            calendar,
            relationship.RelationshipId);
        ValidateParticipantsAtChange(
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationship.Dimensions,
            relationship.LastChangeDate,
            relationship.RelationshipId);
        ValidateFoldedSummary(relationship.FoldedMemories, calendar.Date, "Archived memory summary");
        ValidateFoldedParticipantTimeline(
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationship.LastChangeDate,
            relationship.FoldedMemories,
            relationship.RelationshipId);
        if (relationship.FoldedMemories.MemoryCount == 0)
        {
            throw new SimulationValidationException(
                $"Archived relationship '{relationship.RelationshipId}' has no folded memory history.");
        }

        if (relationship.FoldedMemories.LatestDate != relationship.LastChangeDate)
        {
            throw new SimulationValidationException(
                $"Archived relationship '{relationship.RelationshipId}' has no memory at its last change date.");
        }

        long dimensionImportance = CalculateDimensionImportance(relationship.Dimensions);
        if (relationship.RecordedImportance < dimensionImportance
            || !IsPossibleSeverityTotal(
                relationship.RecordedImportance - dimensionImportance,
                relationship.FoldedMemories.MemoryCount))
        {
            throw new SimulationValidationException(
                $"Archived relationship '{relationship.RelationshipId}' has invalid recorded importance.");
        }

        return Clone(relationship);
    }

    private ConsequentialMemory ValidateAndCloneMemory(
        DetailedDirectionalRelationship relationship,
        ConsequentialMemory memory,
        CampaignCalendar calendar,
        ISet<EntityId> memoryIds,
        ISet<MemorySourceIdentity> sourceIdentities)
    {
        if (memory.SubjectCharacterId != relationship.SubjectCharacterId
            || memory.TargetCharacterId != relationship.TargetCharacterId
            || memory.RecordedTurnIndex < 0
            || memory.RecordedTurnIndex > relationship.LastChangeTurnIndex
            || !memory.ResolutionDate.IsValid
            || memory.ResolutionDate.CompareTo(relationship.LastChangeDate) > 0
            || memory.ResolutionDate.CompareTo(calendar.Date) > 0)
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' has inconsistent subject, target, date, or turn state.");
        }

        MemorySourceIdentity sourceIdentity = GetSourceIdentity(memory);
        if (!memoryIds.Add(memory.MemoryId) || !sourceIdentities.Add(sourceIdentity))
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' collides with another retained memory or source event.");
        }

        ValidateCurrentMemory(memory, validateResultBounds: false);

        return Clone(memory);
    }

    private void ValidateCurrentMemory(ConsequentialMemory memory, bool validateResultBounds)
    {
        if (memory is null || memory.ContractVersion != RelationshipContractVersions.Memory)
        {
            throw new SimulationValidationException(
                $"Unsupported relationship memory contract version {memory?.ContractVersion}.");
        }

        ValidateId(memory.MemoryId, "Relationship memory ID");
        ValidateId(memory.MeaningId, $"Relationship memory '{memory.MemoryId}' meaning ID");
        ValidateId(memory.SourceEventId, $"Relationship memory '{memory.MemoryId}' source event ID");
        if (!Enum.IsDefined(memory.SourceKind)
            || !Enum.IsDefined(memory.IdentityScheme)
            || memory.ConsequenceIndex < 0)
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' has invalid source metadata.");
        }

        bool validIdentity = memory.IdentityScheme switch
        {
            RelationshipMemoryIdentityScheme.LegacyRelationshipActionV1 =>
                memory.SourceKind == RelationshipMemorySourceKind.RelationshipAction
                && memory.ConsequenceIndex == 0
                && TryGetCausalCommandId(
                    memory.SourceEventId,
                    memory.ResolutionDate,
                    out EntityId commandId)
                && memory.MemoryId == RelationshipIds.DeriveMemoryId(
                    memory.ResolutionDate,
                    commandId),
            RelationshipMemoryIdentityScheme.SourceEventV2 =>
                memory.SourceKind is RelationshipMemorySourceKind.CharacterAction
                    or RelationshipMemorySourceKind.HouseholdDecision
                    or RelationshipMemorySourceKind.CharacterMarriageAction
                    or RelationshipMemorySourceKind.CharacterCondition
                && memory.MemoryId == RelationshipIds.DeriveMemoryId(
                    memory.SourceEventId,
                    memory.SubjectCharacterId,
                    memory.TargetCharacterId,
                    memory.ConsequenceIndex),
            _ => false,
        };
        if (!validIdentity)
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' has invalid deterministic identity or source causality.");
        }

        RelationshipActionCommandPayload action = new(
            memory.TargetCharacterId,
            memory.AppliedImpact,
            memory.MeaningId,
            memory.InitialSeverity,
            memory.Publicity,
            memory.DecayIntervalTurns,
            memory.WitnessIds);
        CommandValidationResult validation = ValidateActionCore(
            memory.SubjectCharacterId,
            action,
            memory.ResolutionDate,
            memory.RecordedTurnIndex,
            validateResultBounds,
            requireNonZeroImpact:
                memory.SourceKind == RelationshipMemorySourceKind.RelationshipAction);
        if (!validation.IsValid)
        {
            throw new SimulationValidationException(
                $"Relationship memory '{memory.MemoryId}' is invalid: "
                + string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }
    }

    private static void ValidateSnapshotShape(RelationshipWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != RelationshipContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported relationship-world snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Subjects is null || snapshot.Subjects.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Relationship-world subject histories and entries cannot be null.");
        }
    }

    private void ValidateRelationshipIdentity(
        EntityId owningSubjectId,
        int contractVersion,
        EntityId relationshipId,
        EntityId subjectCharacterId,
        EntityId targetCharacterId,
        ISet<EntityId> relationshipIds,
        ISet<EntityId> targets)
    {
        if (contractVersion != RelationshipContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Unsupported relationship state contract version {contractVersion}.");
        }

        if (subjectCharacterId != owningSubjectId)
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' does not belong to its containing subject.");
        }

        ValidateCharacter(subjectCharacterId, "Relationship subject");
        ValidateCharacter(targetCharacterId, "Relationship target");
        if (subjectCharacterId == targetCharacterId)
        {
            throw new SimulationValidationException("A directional relationship cannot target its subject.");
        }

        if (relationshipId != RelationshipIds.DeriveRelationshipId(subjectCharacterId, targetCharacterId))
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' does not have its exact deterministic identity.");
        }

        if (!relationshipIds.Add(relationshipId) || !targets.Add(targetCharacterId))
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' collides with another relationship for this subject.");
        }
    }

    private void ValidateWitnesses(
        RelationshipActionCommandPayload payload,
        EntityId subjectCharacterId,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (payload.WitnessIds is null)
        {
            issues.Add(new("invalid_witnesses", "Relationship witness IDs cannot be null."));
            return;
        }

        if (payload.Publicity == MemoryPublicity.Witnessed)
        {
            if (payload.WitnessIds.Count is < 1 or > 32)
            {
                issues.Add(new(
                    "invalid_witness_count",
                    "Witnessed memories require from 1 through 32 witnesses."));
            }
        }
        else if (payload.WitnessIds.Count != 0)
        {
            issues.Add(new(
                "unexpected_witnesses",
                "Only Witnessed memories may store witness IDs."));
        }

        EntityId? previous = null;
        foreach (EntityId witnessId in payload.WitnessIds)
        {
            if (!witnessId.IsValid)
            {
                issues.Add(new("invalid_witness", "Relationship witness ID is invalid."));
            }

            if (previous is EntityId previousId && previousId.CompareTo(witnessId) >= 0)
            {
                issues.Add(new(
                    "noncanonical_witnesses",
                    "Relationship witness IDs must be unique and in ordinal canonical order."));
                break;
            }

            if (witnessId == subjectCharacterId || witnessId == payload.TargetCharacterId)
            {
                issues.Add(new(
                    "participant_witness",
                    "Relationship witnesses must exclude the subject and target."));
            }

            if (witnessId.IsValid)
            {
                if (!characters.TryGetCharacterProfile(
                        witnessId,
                        out AuthoritativeCharacterProfile? witness))
                {
                    issues.Add(new(
                        "unknown_witness",
                        $"Relationship witness '{witnessId}' does not exist."));
                }
                else if (resolutionDate.IsValid
                    && witness.BirthDate.CompareTo(resolutionDate) > 0)
                {
                    issues.Add(new(
                        "witness_not_born",
                        $"Relationship witness '{witnessId}' is born after the authoritative resolution date."));
                }
            }

            previous = witnessId;
        }
    }

    private RelationshipDimensions GetCurrentDimensions(EntityId subjectId, EntityId targetId)
    {
        if (!subjects.TryGetValue(subjectId, out SubjectRelationshipHistory? history))
        {
            return RelationshipDimensions.Zero;
        }

        DetailedDirectionalRelationship? detailed = history.DetailedRelationships
            .SingleOrDefault(item => item.TargetCharacterId == targetId);
        if (detailed is not null)
        {
            return detailed.Dimensions;
        }

        ArchivedDirectionalRelationshipSummary? archived = history.ArchivedRelationships
            .SingleOrDefault(item => item.TargetCharacterId == targetId);
        return archived?.Dimensions ?? RelationshipDimensions.Zero;
    }

    private void ValidateCharacter(EntityId id, string description)
    {
        ValidateId(id, $"{description} ID");
        if (!characters.TryGetCharacterProfile(id, out _))
        {
            throw new SimulationValidationException($"{description} '{id}' does not exist.");
        }
    }

    private void ValidateParticipantsAtChange(
        EntityId subjectCharacterId,
        EntityId targetCharacterId,
        RelationshipDimensions dimensions,
        CampaignDate lastChangeDate,
        EntityId relationshipId)
    {
        if (!characters.TryGetCharacterProfile(subjectCharacterId, out AuthoritativeCharacterProfile? subject)
            || !characters.TryGetCharacterProfile(targetCharacterId, out AuthoritativeCharacterProfile? target))
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' has unavailable participants.");
        }

        if (subject.BirthDate.CompareTo(lastChangeDate) > 0
            || target.BirthDate.CompareTo(lastChangeDate) > 0)
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' predates one of its participants.");
        }

        if (dimensions.Attraction != 0
            && (CalculateAge(subject.BirthDate, lastChangeDate) < 18
                || CalculateAge(target.BirthDate, lastChangeDate) < 18))
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' contains attraction before both participants were adults.");
        }
    }

    private void ValidateFoldedParticipantTimeline(
        EntityId subjectCharacterId,
        EntityId targetCharacterId,
        CampaignDate lastChangeDate,
        FoldedMemorySummary foldedMemories,
        EntityId relationshipId)
    {
        if (foldedMemories.MemoryCount == 0)
        {
            return;
        }

        _ = characters.TryGetCharacterProfile(subjectCharacterId, out AuthoritativeCharacterProfile? subject);
        _ = characters.TryGetCharacterProfile(targetCharacterId, out AuthoritativeCharacterProfile? target);
        CampaignDate earliest = foldedMemories.EarliestDate!.Value;
        CampaignDate latest = foldedMemories.LatestDate!.Value;
        if (subject!.BirthDate.CompareTo(earliest) > 0
            || target!.BirthDate.CompareTo(earliest) > 0
            || latest.CompareTo(lastChangeDate) > 0)
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' has folded memory dates outside its participant timeline.");
        }
    }

    private static void ValidateDimensions(RelationshipDimensions dimensions, EntityId relationshipId)
    {
        if (dimensions is null || !DimensionsAreInRange(dimensions))
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' has dimensions outside supported bounds.");
        }
    }

    private static void ValidateChangePoint(
        CampaignDate date,
        long turnIndex,
        CampaignCalendar calendar,
        EntityId relationshipId)
    {
        if (!date.IsValid
            || date.CompareTo(calendar.Date) > 0
            || turnIndex < 0
            || turnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Relationship '{relationshipId}' has an invalid last-change date or turn.");
        }
    }

    private static void ValidateFoldedSummary(
        FoldedMemorySummary summary,
        CampaignDate currentDate,
        string description)
    {
        if (summary is null || summary.MemoryCount < 0 || summary.TotalEffectiveSeverity < 0)
        {
            throw new SimulationValidationException($"{description} has invalid counters.");
        }

        if (summary.MemoryCount == 0)
        {
            if (summary.TotalEffectiveSeverity != 0
                || summary.EarliestDate is not null
                || summary.LatestDate is not null)
            {
                throw new SimulationValidationException($"{description} empty state is inconsistent.");
            }

            return;
        }

        if (!IsPossibleSeverityTotal(summary.TotalEffectiveSeverity, summary.MemoryCount)
            || summary.EarliestDate is not CampaignDate earliest
            || summary.LatestDate is not CampaignDate latest
            || !earliest.IsValid
            || !latest.IsValid
            || earliest.CompareTo(latest) > 0
            || latest.CompareTo(currentDate) > 0)
        {
            throw new SimulationValidationException($"{description} dates or severity totals are inconsistent.");
        }
    }

    private static void ValidateDistantHistory(
        DistantRelationshipHistoryAggregate aggregate,
        CampaignCalendar calendar,
        CampaignDate subjectBirthDate)
    {
        if (aggregate.RelationshipCount < 0
            || aggregate.MemoryCount < 0
            || aggregate.TotalRecordedImportance < 0
            || aggregate.TotalEffectiveMemorySeverity < 0
            || aggregate.LatestChangeTurnIndex < 0
            || aggregate.LatestChangeTurnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException("Distant relationship history has invalid counters.");
        }

        if (aggregate.RelationshipCount == 0)
        {
            if (aggregate != DistantRelationshipHistoryAggregate.Empty)
            {
                throw new SimulationValidationException("Empty distant relationship history is inconsistent.");
            }

            return;
        }

        if (aggregate.MemoryCount < aggregate.RelationshipCount
            || !IsPossibleSeverityTotal(
                aggregate.TotalEffectiveMemorySeverity,
                aggregate.MemoryCount)
            || aggregate.EarliestDate is not CampaignDate earliest
            || aggregate.LatestDate is not CampaignDate latest
            || !earliest.IsValid
            || !latest.IsValid
            || earliest.CompareTo(subjectBirthDate) < 0
            || earliest.CompareTo(latest) > 0
            || latest.CompareTo(calendar.Date) > 0)
        {
            throw new SimulationValidationException("Distant relationship history is inconsistent.");
        }
    }

    private static bool DimensionsAreInRange(RelationshipDimensions dimensions) =>
        dimensions.Affection is >= 0 and <= 100
        && dimensions.Trust is >= 0 and <= 100
        && dimensions.Respect is >= 0 and <= 100
        && dimensions.Attraction is >= 0 and <= 100
        && dimensions.Obligation is >= 0 and <= 100
        && dimensions.Fear is >= 0 and <= 100
        && dimensions.Resentment is >= 0 and <= 100
        && dimensions.Rivalry is >= 0 and <= 100
        && dimensions.Compatibility is >= -100 and <= 100;

    private static bool ImpactCanRepresentBoundedTransition(RelationshipImpact impact) =>
        impact.Affection is >= -100 and <= 100
        && impact.Trust is >= -100 and <= 100
        && impact.Respect is >= -100 and <= 100
        && impact.Attraction is >= -100 and <= 100
        && impact.Obligation is >= -100 and <= 100
        && impact.Fear is >= -100 and <= 100
        && impact.Resentment is >= -100 and <= 100
        && impact.Rivalry is >= -100 and <= 100
        && impact.Compatibility is >= -200 and <= 200;

    private static RelationshipDimensions ApplyImpact(
        RelationshipDimensions dimensions,
        RelationshipImpact impact) => new(
        checked(dimensions.Affection + impact.Affection),
        checked(dimensions.Trust + impact.Trust),
        checked(dimensions.Respect + impact.Respect),
        checked(dimensions.Attraction + impact.Attraction),
        checked(dimensions.Obligation + impact.Obligation),
        checked(dimensions.Fear + impact.Fear),
        checked(dimensions.Resentment + impact.Resentment),
        checked(dimensions.Rivalry + impact.Rivalry),
        checked(dimensions.Compatibility + impact.Compatibility));

    private static RelationshipImpact CreateBoundedHarmfulImpact(
        RelationshipDimensions current)
    {
        if (current.Resentment < 100)
        {
            return new(0, 0, 0, 0, 0, 0, Math.Min(25, 100 - current.Resentment), 0, 0);
        }

        if (current.Fear < 100)
        {
            return new(0, 0, 0, 0, 0, Math.Min(25, 100 - current.Fear), 0, 0, 0);
        }

        if (current.Rivalry < 100)
        {
            return new(0, 0, 0, 0, 0, 0, 0, Math.Min(25, 100 - current.Rivalry), 0);
        }

        if (current.Trust > 0)
        {
            return new(0, -Math.Min(25, current.Trust), 0, 0, 0, 0, 0, 0, 0);
        }

        if (current.Affection > 0)
        {
            return new(-Math.Min(25, current.Affection), 0, 0, 0, 0, 0, 0, 0, 0);
        }

        if (current.Respect > 0)
        {
            return new(0, 0, -Math.Min(25, current.Respect), 0, 0, 0, 0, 0, 0);
        }

        if (current.Compatibility > -100)
        {
            return new(0, 0, 0, 0, 0, 0, 0, 0, -Math.Min(25, current.Compatibility + 100));
        }

        return new RelationshipImpact(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private void ValidateHarmfulConsequence(
        RelationshipMemoryConsequenceSpecification consequence,
        EntityId expectedSubjectCharacterId,
        EntityId expectedTargetCharacterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        if (consequence is null
            || consequence.Impact is null
            || consequence.SubjectCharacterId != expectedSubjectCharacterId
            || consequence.TargetCharacterId != expectedTargetCharacterId
            || consequence.Impact.Affection > 0
            || consequence.Impact.Trust > 0
            || consequence.Impact.Respect > 0
            || consequence.Impact.Attraction != 0
            || consequence.Impact.Compatibility > 0
            || consequence.Impact.Fear < 0
            || consequence.Impact.Resentment < 0
            || consequence.Impact.Rivalry < 0)
        {
            throw new SimulationValidationException(
                "Coercive relationship consequences must leave attraction unchanged, cannot improve other positive dimensions, and cannot reduce fear, resentment, or rivalry.");
        }

        RelationshipActionCommandPayload validationPayload = new(
            consequence.TargetCharacterId,
            consequence.Impact,
            consequence.MeaningId,
            consequence.InitialSeverity,
            consequence.Publicity,
            consequence.DecayIntervalTurns,
            consequence.WitnessIds);
        CommandValidationResult validation = ValidateActionCore(
            consequence.SubjectCharacterId,
            validationPayload,
            resolutionDate,
            authoritativeTurnIndex,
            validateResultBounds: true,
            requireNonZeroImpact: false);
        if (!validation.IsValid)
        {
            throw new SimulationValidationException(
                "Coercive relationship consequence is invalid: "
                + string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }
    }

    private static long CalculateImportance(
        RelationshipDimensions dimensions,
        IEnumerable<ConsequentialMemory> memories,
        long authoritativeTurnIndex)
    {
        long importance = CalculateDimensionImportance(dimensions);
        foreach (ConsequentialMemory memory in memories)
        {
            importance = checked(importance + GetEffectiveSeverity(memory, authoritativeTurnIndex));
        }

        return importance;
    }

    private static long CalculateDimensionImportance(RelationshipDimensions dimensions) => checked(
        (long)dimensions.Affection
        + dimensions.Trust
        + dimensions.Respect
        + dimensions.Attraction
        + dimensions.Obligation
        + dimensions.Fear
        + dimensions.Resentment
        + dimensions.Rivalry
        + Math.Abs((long)dimensions.Compatibility));

    private static IEnumerable<ConsequentialMemory> OrderMemories(
        IEnumerable<ConsequentialMemory> memories,
        long authoritativeTurnIndex) => memories
        .OrderByDescending(memory => GetEffectiveSeverity(memory, authoritativeTurnIndex))
        .ThenByDescending(memory => memory.RecordedTurnIndex)
        .ThenBy(memory => memory.MemoryId);

    private static IEnumerable<DetailedDirectionalRelationship> OrderDetailed(
        IEnumerable<DetailedDirectionalRelationship> relationships) => relationships
        .OrderByDescending(item => item.RecordedImportance)
        .ThenByDescending(item => item.LastChangeTurnIndex)
        .ThenBy(item => item.RelationshipId);

    private static IEnumerable<ArchivedDirectionalRelationshipSummary> OrderArchived(
        IEnumerable<ArchivedDirectionalRelationshipSummary> relationships) => relationships
        .OrderByDescending(item => item.RecordedImportance)
        .ThenByDescending(item => item.LastChangeTurnIndex)
        .ThenBy(item => item.RelationshipId);

    private static FoldedMemorySummary AddFoldedMemory(
        FoldedMemorySummary summary,
        int effectiveSeverity,
        CampaignDate date) => new(
        checked(summary.MemoryCount + 1),
        checked(summary.TotalEffectiveSeverity + effectiveSeverity),
        Min(summary.EarliestDate, date),
        Max(summary.LatestDate, date));

    private static DistantRelationshipHistoryAggregate AddDistantHistory(
        DistantRelationshipHistoryAggregate aggregate,
        ArchivedDirectionalRelationshipSummary relationship)
    {
        CampaignDate relationshipEarliest = relationship.FoldedMemories.EarliestDate is CampaignDate foldedEarliest
            ? Min(foldedEarliest, relationship.LastChangeDate)
            : relationship.LastChangeDate;
        CampaignDate relationshipLatest = relationship.FoldedMemories.LatestDate is CampaignDate foldedLatest
            ? Max(foldedLatest, relationship.LastChangeDate)
            : relationship.LastChangeDate;
        return new DistantRelationshipHistoryAggregate(
            checked(aggregate.RelationshipCount + 1),
            checked(aggregate.MemoryCount + relationship.FoldedMemories.MemoryCount),
            checked(aggregate.TotalRecordedImportance + relationship.RecordedImportance),
            checked(aggregate.TotalEffectiveMemorySeverity + relationship.FoldedMemories.TotalEffectiveSeverity),
            Min(aggregate.EarliestDate, relationshipEarliest),
            Max(aggregate.LatestDate, relationshipLatest),
            Math.Max(aggregate.LatestChangeTurnIndex, relationship.LastChangeTurnIndex));
    }

    private static bool IsPossibleSeverityTotal(long total, long count)
    {
        if (total < 0 || count < 0 || (count == 0 && total != 0))
        {
            return false;
        }

        return count != 0 && (total / count < 100 || (total / count == 100 && total % count == 0))
            || count == 0;
    }

    private static CampaignDate Min(CampaignDate left, CampaignDate right) =>
        left.CompareTo(right) <= 0 ? left : right;

    private static CampaignDate Max(CampaignDate left, CampaignDate right) =>
        left.CompareTo(right) >= 0 ? left : right;

    private static CampaignDate? Min(CampaignDate? left, CampaignDate right) =>
        left is CampaignDate value ? Min(value, right) : right;

    private static CampaignDate? Max(CampaignDate? left, CampaignDate right) =>
        left is CampaignDate value ? Max(value, right) : right;

    internal static EntityId DeriveEventId(CampaignDate resolutionDate, EntityId commandId)
    {
        ValidateId(commandId, "Relationship action command ID");
        if (!resolutionDate.IsValid)
        {
            throw new SimulationValidationException("Relationship action event date is invalid.");
        }

        string date = FormatDate(resolutionDate);
        string value = $"event:relationship_action/{date}/{commandId.Value.Replace(':', '/')}";
        if (!EntityId.TryParse(value, out EntityId eventId))
        {
            throw new SimulationValidationException(
                "Relationship action command ID is too long to derive a valid source event ID.");
        }

        return eventId;
    }

    private static bool TryGetCausalCommandId(
        EntityId eventId,
        CampaignDate resolutionDate,
        out EntityId commandId)
    {
        string prefix = $"event:relationship_action/{FormatDate(resolutionDate)}/";
        string value = eventId.Value;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            commandId = default;
            return false;
        }

        string path = value[prefix.Length..];
        int separator = path.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0
            || !EntityId.TryParse($"{path[..separator]}:{path[(separator + 1)..]}", out commandId))
        {
            commandId = default;
            return false;
        }

        return DeriveEventId(resolutionDate, commandId) == eventId;
    }

    private static string FormatDate(CampaignDate date) => string.Concat(
        date.Year.ToString("D4", CultureInfo.InvariantCulture),
        "-",
        date.Month.ToString("D2", CultureInfo.InvariantCulture),
        "-",
        date.Day.ToString("D2", CultureInfo.InvariantCulture));

    private static int CalculateAge(CampaignDate birthDate, CampaignDate currentDate)
    {
        int age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month
            || (currentDate.Month == birthDate.Month && currentDate.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private static void ValidateId(EntityId id, string description)
    {
        if (!id.IsValid)
        {
            throw new SimulationValidationException($"{description} is invalid.");
        }
    }

    private static SubjectRelationshipHistory Clone(SubjectRelationshipHistory subject) => subject with
    {
        DetailedRelationships = subject.DetailedRelationships.Select(Clone).ToArray(),
        ArchivedRelationships = subject.ArchivedRelationships.Select(Clone).ToArray(),
        DistantHistory = Clone(subject.DistantHistory),
    };

    private static DetailedDirectionalRelationship Clone(DetailedDirectionalRelationship relationship) =>
        relationship with
        {
            Dimensions = Clone(relationship.Dimensions),
            Memories = relationship.Memories.Select(Clone).ToArray(),
            FoldedMemories = Clone(relationship.FoldedMemories),
        };

    private static ArchivedDirectionalRelationshipSummary Clone(
        ArchivedDirectionalRelationshipSummary relationship) => relationship with
        {
            Dimensions = Clone(relationship.Dimensions),
            FoldedMemories = Clone(relationship.FoldedMemories),
        };

    private static ConsequentialMemory Clone(ConsequentialMemory memory) => memory with
    {
        WitnessIds = memory.WitnessIds.ToArray(),
        AppliedImpact = memory.AppliedImpact with { },
    };

    private static RelationshipMemoryConsequenceSpecification Clone(
        RelationshipMemoryConsequenceSpecification consequence) => consequence with
        {
            Impact = consequence.Impact with { },
            WitnessIds = consequence.WitnessIds.ToArray(),
        };

    private static RelationshipDimensions Clone(RelationshipDimensions dimensions) => dimensions with { };

    private static FoldedMemorySummary Clone(FoldedMemorySummary summary) => summary with { };

    private static DistantRelationshipHistoryAggregate Clone(
        DistantRelationshipHistoryAggregate aggregate) => aggregate with { };

    private static MemorySourceIdentity GetSourceIdentity(ConsequentialMemory memory) =>
        new(memory.SourceEventId, memory.ConsequenceIndex);

    private void ReplaceFrom(RelationshipWorldState candidate)
    {
        subjects.Clear();
        foreach (SubjectRelationshipHistory subject in candidate.subjects.Values)
        {
            subjects.Add(subject.SubjectCharacterId, Clone(subject));
        }

        retainedMemoryIds.Clear();
        retainedMemoryIds.UnionWith(candidate.retainedMemoryIds);
        retainedSourceIdentities.Clear();
        retainedSourceIdentities.UnionWith(candidate.retainedSourceIdentities);
    }
}

internal readonly record struct MemorySourceIdentity(EntityId SourceEventId, int ConsequenceIndex);

internal sealed record RelationshipWorldUpdatePlan(RelationshipWorldState Candidate);
