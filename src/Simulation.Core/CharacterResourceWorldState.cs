using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;

namespace Simulation.Core;

public sealed class CharacterResourceWorldState : IAuthoritativeCharacterResourceWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, CharacterWealthAccountState> accounts = [];
    private readonly SortedDictionary<EntityId, WealthLedgerEntry> ledgerEntries = [];
    private readonly SortedDictionary<EntityId, CharacterWealthHistoryAggregate> history = [];
    private CampaignCalendar calendar;

    public CharacterResourceWorldState(
        CharacterResourceWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException("Character-resource snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException("Authoritative character query cannot be null.");
        ValidateCalendar(calendar);
        this.calendar = calendar;

        ValidateSnapshotShape(snapshot);
        AddAccounts(snapshot.Accounts);
        AddLedgerEntries(snapshot.LedgerEntries);
        AddHistory(snapshot.History);
        ValidateCrossRecordState();
        ValidateRetentionBounds();
    }

    public IReadOnlyList<CharacterWealthAccountState> Accounts =>
        accounts.Values.Select(Clone).ToArray();

    public IReadOnlyList<WealthLedgerEntry> LedgerEntries => OrderedLedgerEntries()
        .Select(Clone)
        .ToArray();

    public IReadOnlyList<CharacterWealthHistoryAggregate> History =>
        history.Values.Select(Clone).ToArray();

    public long GetWealth(EntityId characterId)
    {
        ValidateCharacter(characterId, "Character wealth query");
        return GetStoredWealth(characterId);
    }

    public bool TryGetAccount(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterWealthAccountState? account)
    {
        if (accounts.TryGetValue(characterId, out CharacterWealthAccountState? stored))
        {
            account = Clone(stored);
            return true;
        }

        account = null;
        return false;
    }

    public IReadOnlyList<WealthLedgerEntry> GetLedgerEntries(EntityId characterId)
    {
        ValidateCharacter(characterId, "Character wealth-ledger query");
        return OrderedLedgerEntries()
            .Where(item => item.CharacterId == characterId)
            .Select(Clone)
            .ToArray();
    }

    public bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterWealthHistoryAggregate? aggregate)
    {
        if (history.TryGetValue(characterId, out CharacterWealthHistoryAggregate? stored))
        {
            aggregate = Clone(stored);
            return true;
        }

        aggregate = null;
        return false;
    }

    public CharacterResourceWorldSnapshot CaptureSnapshot() => new(
        CharacterResourceContractVersions.Snapshot,
        accounts.Values.Select(Clone).ToArray(),
        OrderedLedgerEntries().Select(Clone).ToArray(),
        history.Values.Select(Clone).ToArray());

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        ValidateCalendar(value);
        if (value.Date.CompareTo(calendar.Date) < 0 || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Character-resource campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    public CommandValidationResult ValidateAction(
        EntityId actingCharacterId,
        CharacterResourceActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues,
            validateBalances: true);
        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterResourceActionResolvedEventPayload PlanAction(
        EntityId actingCharacterId,
        CharacterResourceActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues,
            validateBalances: false);
        ThrowIfInvalid(issues);
        ValidateId(commandId, "Character-resource command ID");
        ValidateId(eventId, "Character-resource event ID");
        if (eventId != CharacterResourceIds.DeriveActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                $"Character-resource event ID '{eventId}' does not match command '{commandId}'.");
        }

        TransferWealthAction action = payload.Action as TransferWealthAction
            ?? throw new SimulationValidationException("Unsupported character-resource action type.");
        long sourceWealth = GetStoredWealth(actingCharacterId);
        long recipientWealth = GetStoredWealth(action.RecipientCharacterId);
        ICharacterResourceActionOutcome outcome;
        if (sourceWealth < action.Amount)
        {
            outcome = new WealthTransferCancelledOutcome(
                CharacterResourceContractVersions.Outcome,
                WealthTransferCancellationReason.InsufficientWealth);
        }
        else if (recipientWealth > long.MaxValue - action.Amount)
        {
            outcome = new WealthTransferCancelledOutcome(
                CharacterResourceContractVersions.Outcome,
                WealthTransferCancellationReason.RecipientOverflow);
        }
        else
        {
            EntityId transferId = CharacterResourceIds.DeriveWealthTransferId(eventId);
            WealthTransferRecord transfer = new(
                CharacterResourceContractVersions.State,
                transferId,
                actingCharacterId,
                action.RecipientCharacterId,
                action.Amount,
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId);
            WealthLedgerEntry outgoing = CreateLedgerEntry(
                transfer,
                actingCharacterId,
                action.RecipientCharacterId,
                WealthLedgerDirection.Outgoing);
            WealthLedgerEntry incoming = CreateLedgerEntry(
                transfer,
                action.RecipientCharacterId,
                actingCharacterId,
                WealthLedgerDirection.Incoming);
            outcome = new WealthTransferredOutcome(
                CharacterResourceContractVersions.Outcome,
                transfer,
                checked(sourceWealth - action.Amount),
                checked(recipientWealth + action.Amount),
                outgoing,
                incoming);
        }

        return new CharacterResourceActionResolvedEventPayload(
            actingCharacterId,
            Clone(action),
            Clone(outcome));
    }

    public void PrevalidateOutcome(
        CharacterResourceActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        _ = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
    }

    internal CharacterResourceWorldUpdatePlan PrepareOutcome(
        CharacterResourceActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null)
        {
            throw new SimulationValidationException(
                "Character-resource action outcome payload cannot be null.");
        }

        CharacterResourceActionResolvedEventPayload expected = PlanAction(
            payload.ActingCharacterId,
            new CharacterResourceActionCommandPayload(payload.Action),
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        string expectedJson = JsonSerializer.Serialize(expected, SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Character-resource action outcome does not match the exact deterministic plan.");
        }

        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterResourceWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            candidateCalendar);
        if (payload.Outcome is WealthTransferredOutcome transfer)
        {
            candidate.CommitTransfer(transfer);
        }
        else if (payload.Outcome is not WealthTransferCancelledOutcome)
        {
            throw new SimulationValidationException(
                "Unsupported character-resource action outcome type.");
        }

        return new CharacterResourceWorldUpdatePlan(candidate);
    }

    internal CharacterResourceInheritancePlan PrepareInheritance(
        EntityId sourceCharacterId,
        EntityId recipientCharacterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        ValidateId(sourceCharacterId, "Inheritance source character ID");
        ValidateId(recipientCharacterId, "Inheritance recipient character ID");
        ValidateId(commandId, "Inheritance command ID");
        ValidateId(eventId, "Inheritance resource event ID");
        if (!resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Inheritance wealth transfer cannot precede resource state.");
        }

        ValidateCharacterAtDate(
            sourceCharacterId,
            resolutionDate,
            "Inheritance source");
        ValidateCharacterAtDate(
            recipientCharacterId,
            resolutionDate,
            "Inheritance recipient");
        if (sourceCharacterId == recipientCharacterId
            || eventId != CharacterResourceIds.DeriveActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                "Inheritance wealth transfer has invalid participants or deterministic event identity.");
        }

        long amount = GetStoredWealth(sourceCharacterId);
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterResourceWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            candidateCalendar);
        if (amount == 0)
        {
            return new CharacterResourceInheritancePlan(
                null,
                new CharacterResourceWorldUpdatePlan(candidate));
        }

        long recipientWealth = GetStoredWealth(recipientCharacterId);
        if (recipientWealth > long.MaxValue - amount)
        {
            throw new SimulationValidationException(
                "Inheritance wealth transfer would overflow recipient wealth.");
        }

        WealthTransferRecord transfer = new(
            CharacterResourceContractVersions.State,
            CharacterResourceIds.DeriveWealthTransferId(eventId),
            sourceCharacterId,
            recipientCharacterId,
            amount,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        WealthTransferredOutcome outcome = new(
            CharacterResourceContractVersions.Outcome,
            transfer,
            0,
            checked(recipientWealth + amount),
            CreateLedgerEntry(
                transfer,
                sourceCharacterId,
                recipientCharacterId,
                WealthLedgerDirection.Outgoing),
            CreateLedgerEntry(
                transfer,
                recipientCharacterId,
                sourceCharacterId,
                WealthLedgerDirection.Incoming));
        candidate.CommitTransfer(outcome);
        return new CharacterResourceInheritancePlan(
            (WealthTransferredOutcome)Clone(outcome),
            new CharacterResourceWorldUpdatePlan(candidate));
    }

    internal void ApplyOutcome(
        CharacterResourceActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        CharacterResourceWorldUpdatePlan plan = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        ApplyPrepared(plan);
    }

    internal void ApplyPrepared(CharacterResourceWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-resource update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private void CommitTransfer(WealthTransferredOutcome outcome)
    {
        ValidateTransferredOutcome(outcome);
        long sourceBefore = GetStoredWealth(outcome.Transfer.SourceCharacterId);
        long recipientBefore = GetStoredWealth(outcome.Transfer.RecipientCharacterId);
        BigInteger totalBefore = (BigInteger)sourceBefore + recipientBefore;
        BigInteger totalAfter = (BigInteger)outcome.SourceWealthAfter + outcome.RecipientWealthAfter;
        if (totalBefore != totalAfter)
        {
            throw new SimulationValidationException(
                "Character wealth transfer does not conserve personal wealth.");
        }

        SetWealth(outcome.Transfer.SourceCharacterId, outcome.SourceWealthAfter);
        SetWealth(outcome.Transfer.RecipientCharacterId, outcome.RecipientWealthAfter);
        if (!ledgerEntries.TryAdd(outcome.OutgoingEntry.EntryId, Clone(outcome.OutgoingEntry))
            || !ledgerEntries.TryAdd(outcome.IncomingEntry.EntryId, Clone(outcome.IncomingEntry)))
        {
            throw new SimulationValidationException(
                $"Duplicate wealth transfer '{outcome.Transfer.TransferId}'.");
        }

        EnforceLedgerBound(outcome.Transfer.SourceCharacterId);
        EnforceLedgerBound(outcome.Transfer.RecipientCharacterId);
        ValidateCrossRecordState();
        ValidateRetentionBounds();
    }

    private void ValidateTransferredOutcome(WealthTransferredOutcome outcome)
    {
        if (outcome.ContractVersion != CharacterResourceContractVersions.Outcome
            || outcome.Transfer is null
            || outcome.OutgoingEntry is null
            || outcome.IncomingEntry is null)
        {
            throw new SimulationValidationException(
                "Wealth-transfer outcome has an invalid version or null record.");
        }

        ValidateTransferRecord(outcome.Transfer);
        ValidateLedgerEntry(outcome.OutgoingEntry);
        ValidateLedgerEntry(outcome.IncomingEntry);
        if (outcome.OutgoingEntry.Direction != WealthLedgerDirection.Outgoing
            || outcome.IncomingEntry.Direction != WealthLedgerDirection.Incoming
            || !EntriesFormPair(outcome.OutgoingEntry, outcome.IncomingEntry)
            || !EntryMatchesTransfer(outcome.OutgoingEntry, outcome.Transfer)
            || !EntryMatchesTransfer(outcome.IncomingEntry, outcome.Transfer))
        {
            throw new SimulationValidationException(
                "Wealth-transfer outcome ledger entries do not form the exact transfer pair.");
        }

        long sourceBefore = GetStoredWealth(outcome.Transfer.SourceCharacterId);
        long recipientBefore = GetStoredWealth(outcome.Transfer.RecipientCharacterId);
        if (sourceBefore < outcome.Transfer.Amount
            || recipientBefore > long.MaxValue - outcome.Transfer.Amount
            || outcome.SourceWealthAfter != sourceBefore - outcome.Transfer.Amount
            || outcome.RecipientWealthAfter != recipientBefore + outcome.Transfer.Amount)
        {
            throw new SimulationValidationException(
                "Wealth-transfer outcome balances do not match current authoritative balances.");
        }
    }

    private void SetWealth(EntityId characterId, long wealth)
    {
        if (wealth < 0)
        {
            throw new SimulationValidationException("Character wealth cannot be negative.");
        }

        if (wealth == 0)
        {
            accounts.Remove(characterId);
            return;
        }

        accounts[characterId] = new CharacterWealthAccountState(
            CharacterResourceContractVersions.State,
            CharacterResourceIds.DeriveWealthAccountId(characterId),
            characterId,
            wealth);
    }

    private void EnforceLedgerBound(EntityId characterId)
    {
        WealthLedgerEntry[] entries = OrderedLedgerEntries()
            .Where(item => item.CharacterId == characterId)
            .ToArray();
        int excess = entries.Length - CharacterResourceLimits.RecentLedgerEntriesPerCharacter;
        for (int index = 0; index < excess; index++)
        {
            WealthLedgerEntry evicted = entries[index];
            CharacterWealthHistoryAggregate aggregate = history.TryGetValue(
                characterId,
                out CharacterWealthHistoryAggregate? stored)
                ? stored
                : CharacterWealthHistoryAggregate.Empty(characterId);
            CharacterWealthHistoryAggregate folded = Fold(aggregate, evicted);
            history[characterId] = folded;
            ledgerEntries.Remove(evicted.EntryId);
        }
    }

    private static CharacterWealthHistoryAggregate Fold(
        CharacterWealthHistoryAggregate aggregate,
        WealthLedgerEntry entry)
    {
        try
        {
            return entry.Direction switch
            {
                WealthLedgerDirection.Incoming => aggregate with
                {
                    FoldedIncomingCount = checked(aggregate.FoldedIncomingCount + 1),
                    FoldedIncomingAmount = checked(aggregate.FoldedIncomingAmount + entry.Amount),
                    EarliestDate = Earlier(aggregate.EarliestDate, entry.ResolutionDate),
                    LatestDate = Later(aggregate.LatestDate, entry.ResolutionDate),
                },
                WealthLedgerDirection.Outgoing => aggregate with
                {
                    FoldedOutgoingCount = checked(aggregate.FoldedOutgoingCount + 1),
                    FoldedOutgoingAmount = checked(aggregate.FoldedOutgoingAmount + entry.Amount),
                    EarliestDate = Earlier(aggregate.EarliestDate, entry.ResolutionDate),
                    LatestDate = Later(aggregate.LatestDate, entry.ResolutionDate),
                },
                _ => throw new SimulationValidationException(
                    "Wealth-ledger entry has an invalid direction."),
            };
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Character wealth history for '{aggregate.CharacterId}' exceeds Int64 capacity: {exception.Message}");
        }
    }

    private static CampaignDate Earlier(CampaignDate? current, CampaignDate candidate) =>
        current is null || candidate.CompareTo(current.Value) < 0 ? candidate : current.Value;

    private static CampaignDate Later(CampaignDate? current, CampaignDate candidate) =>
        current is null || candidate.CompareTo(current.Value) > 0 ? candidate : current.Value;

    private void ValidateActionEnvelope(
        EntityId actingCharacterId,
        CharacterResourceActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        ICollection<ValidationIssue> issues,
        bool validateBalances)
    {
        if (!actingCharacterId.IsValid)
        {
            issues.Add(new("invalid_actor", "Character-resource actor ID is invalid."));
        }

        if (!resolutionDate.IsValid)
        {
            issues.Add(new("invalid_resolution_date", "Character-resource resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new(
                "past_resolution_date",
                "Character-resource resolution date precedes resource state."));
        }

        if (authoritativeTurnIndex < calendar.TurnIndex)
        {
            issues.Add(new(
                "past_turn_index",
                "Character-resource action turn precedes resource state."));
        }

        if (payload is null)
        {
            issues.Add(new("invalid_payload", "Character-resource action payload cannot be null."));
            return;
        }

        if (payload.Action is not TransferWealthAction transfer)
        {
            issues.Add(new(
                "unsupported_character_resource_action",
                "Only transfer_wealth.v1 is registered for character resources."));
            return;
        }

        AuthoritativeCharacterProfile? actor = ValidateActionCharacter(
            actingCharacterId,
            resolutionDate,
            "actor",
            issues);
        if (actor?.Condition.IsIncapacitated == true)
        {
            issues.Add(new("actor_incapacitated", "Character-resource actor is incapacitated."));
        }

        _ = ValidateActionCharacter(
            transfer.RecipientCharacterId,
            resolutionDate,
            "recipient",
            issues);
        if (actingCharacterId.IsValid && actingCharacterId == transfer.RecipientCharacterId)
        {
            issues.Add(new("self_transfer", "A character cannot transfer wealth to themself."));
        }

        if (transfer.Amount <= 0)
        {
            issues.Add(new("invalid_wealth_amount", "Wealth-transfer amount must be positive."));
        }

        if (!validateBalances
            || transfer.Amount <= 0
            || !actingCharacterId.IsValid
            || !transfer.RecipientCharacterId.IsValid)
        {
            return;
        }

        long sourceWealth = GetStoredWealth(actingCharacterId);
        long recipientWealth = GetStoredWealth(transfer.RecipientCharacterId);
        if (sourceWealth < transfer.Amount)
        {
            issues.Add(new("insufficient_wealth", "Character-resource actor has insufficient wealth."));
        }

        if (recipientWealth > long.MaxValue - transfer.Amount)
        {
            issues.Add(new("wealth_overflow", "Wealth transfer would overflow recipient wealth."));
        }
    }

    private AuthoritativeCharacterProfile? ValidateActionCharacter(
        EntityId characterId,
        CampaignDate resolutionDate,
        string role,
        ICollection<ValidationIssue> issues)
    {
        if (!characterId.IsValid)
        {
            issues.Add(new($"invalid_{role}", $"Character-resource {role} ID is invalid."));
            return null;
        }

        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            issues.Add(new($"unknown_{role}", $"Character-resource {role} '{characterId}' does not exist."));
            return null;
        }

        if (resolutionDate.IsValid && profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new($"{role}_not_born", $"Character-resource {role} is not born by resolution."));
        }

        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new($"{role}_dead", $"Character-resource {role} is dead."));
        }

        return profile;
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<ValidationIssue> issues)
    {
        if (issues.Count == 0)
        {
            return;
        }

        throw new SimulationValidationException(string.Join(
            "; ",
            issues.Select(issue => $"{issue.Code}: {issue.Message}")));
    }

    private static void ValidateSnapshotShape(CharacterResourceWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterResourceContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-resource snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Accounts is null
            || snapshot.LedgerEntries is null
            || snapshot.History is null
            || snapshot.Accounts.Any(item => item is null)
            || snapshot.LedgerEntries.Any(item => item is null)
            || snapshot.History.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Character-resource snapshot collections and entries cannot be null.");
        }
    }

    private void AddAccounts(IReadOnlyList<CharacterWealthAccountState> source)
    {
        foreach (CharacterWealthAccountState account in source)
        {
            ValidateStateVersion(account.ContractVersion, "Character wealth account", account.CharacterId);
            ValidateCharacter(account.CharacterId, "Character wealth account owner");
            if (account.AccountId != CharacterResourceIds.DeriveWealthAccountId(account.CharacterId))
            {
                throw new SimulationValidationException(
                    $"Character wealth account '{account.AccountId}' does not have its exact deterministic ID.");
            }

            if (account.Wealth <= 0)
            {
                throw new SimulationValidationException(
                    "Sparse character wealth accounts must contain positive wealth.");
            }

            if (!accounts.TryAdd(account.CharacterId, Clone(account)))
            {
                throw new SimulationValidationException(
                    $"Duplicate character wealth account for '{account.CharacterId}'.");
            }
        }
    }

    private void AddLedgerEntries(IReadOnlyList<WealthLedgerEntry> source)
    {
        foreach (WealthLedgerEntry entry in source)
        {
            ValidateLedgerEntry(entry);
            if (!ledgerEntries.TryAdd(entry.EntryId, Clone(entry)))
            {
                throw new SimulationValidationException(
                    $"Duplicate character wealth-ledger entry '{entry.EntryId}'.");
            }
        }
    }

    private void AddHistory(IReadOnlyList<CharacterWealthHistoryAggregate> source)
    {
        foreach (CharacterWealthHistoryAggregate aggregate in source)
        {
            ValidateStateVersion(
                aggregate.ContractVersion,
                "Character wealth history",
                aggregate.CharacterId);
            ValidateCharacter(aggregate.CharacterId, "Character wealth history owner");
            if (aggregate.FoldedIncomingCount < 0
                || aggregate.FoldedIncomingAmount < 0
                || aggregate.FoldedOutgoingCount < 0
                || aggregate.FoldedOutgoingAmount < 0)
            {
                throw new SimulationValidationException(
                    "Character wealth-history counts and amounts cannot be negative.");
            }

            long totalCount;
            try
            {
                totalCount = aggregate.TotalFoldedCount;
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Character wealth-history count exceeds Int64 capacity: {exception.Message}");
            }

            if (totalCount == 0
                || (aggregate.FoldedIncomingCount == 0) != (aggregate.FoldedIncomingAmount == 0)
                || (aggregate.FoldedOutgoingCount == 0) != (aggregate.FoldedOutgoingAmount == 0)
                || aggregate.FoldedIncomingAmount < aggregate.FoldedIncomingCount
                || aggregate.FoldedOutgoingAmount < aggregate.FoldedOutgoingCount
                || aggregate.EarliestDate is null
                || aggregate.LatestDate is null
                || !aggregate.EarliestDate.Value.IsValid
                || !aggregate.LatestDate.Value.IsValid
                || aggregate.EarliestDate.Value.CompareTo(aggregate.LatestDate.Value) > 0
                || aggregate.LatestDate.Value.CompareTo(calendar.Date) > 0)
            {
                throw new SimulationValidationException(
                    $"Character wealth history for '{aggregate.CharacterId}' is malformed.");
            }

            ValidateCharacterAtDate(
                aggregate.CharacterId,
                aggregate.EarliestDate.Value,
                "Character wealth-history owner");

            if (!history.TryAdd(aggregate.CharacterId, Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate character wealth history for '{aggregate.CharacterId}'.");
            }
        }
    }

    private void ValidateLedgerEntry(WealthLedgerEntry entry)
    {
        ValidateStateVersion(entry.ContractVersion, "Character wealth-ledger entry", entry.EntryId);
        ValidateId(entry.EntryId, "Character wealth-ledger entry ID");
        ValidateId(entry.TransferId, "Character wealth-ledger transfer ID");
        ValidateId(entry.SourceCommandId, "Character wealth-ledger source command ID");
        ValidateId(entry.SourceEventId, "Character wealth-ledger source event ID");
        if (!Enum.IsDefined(entry.Direction) || entry.Amount <= 0)
        {
            throw new SimulationValidationException(
                $"Character wealth-ledger entry '{entry.EntryId}' has invalid direction or amount.");
        }

        ValidateRecordPoint(entry.ResolutionDate, entry.ResolutionTurnIndex, "Wealth-ledger entry");
        ValidateCharacterAtDate(entry.CharacterId, entry.ResolutionDate, "Wealth-ledger character");
        ValidateCharacterAtDate(
            entry.CounterpartyCharacterId,
            entry.ResolutionDate,
            "Wealth-ledger counterparty");
        if (entry.CharacterId == entry.CounterpartyCharacterId)
        {
            throw new SimulationValidationException(
                $"Character wealth-ledger entry '{entry.EntryId}' cannot be self-directed.");
        }

        if (entry.SourceEventId
                != CharacterResourceIds.DeriveActionEventId(entry.ResolutionDate, entry.SourceCommandId)
            || entry.TransferId != CharacterResourceIds.DeriveWealthTransferId(entry.SourceEventId)
            || entry.EntryId != CharacterResourceIds.DeriveWealthLedgerEntryId(
                entry.TransferId,
                entry.CharacterId,
                entry.Direction))
        {
            throw new SimulationValidationException(
                $"Character wealth-ledger entry '{entry.EntryId}' does not have exact deterministic IDs.");
        }
    }

    private void ValidateTransferRecord(WealthTransferRecord transfer)
    {
        ValidateStateVersion(transfer.ContractVersion, "Wealth transfer", transfer.TransferId);
        ValidateId(transfer.TransferId, "Wealth transfer ID");
        ValidateId(transfer.SourceCommandId, "Wealth transfer source command ID");
        ValidateId(transfer.SourceEventId, "Wealth transfer source event ID");
        ValidateRecordPoint(transfer.ResolutionDate, transfer.ResolutionTurnIndex, "Wealth transfer");
        ValidateCharacterAtDate(
            transfer.SourceCharacterId,
            transfer.ResolutionDate,
            "Wealth transfer source");
        ValidateCharacterAtDate(
            transfer.RecipientCharacterId,
            transfer.ResolutionDate,
            "Wealth transfer recipient");
        if (transfer.SourceCharacterId == transfer.RecipientCharacterId || transfer.Amount <= 0)
        {
            throw new SimulationValidationException(
                $"Wealth transfer '{transfer.TransferId}' has invalid participants or amount.");
        }

        if (transfer.SourceEventId
                != CharacterResourceIds.DeriveActionEventId(
                    transfer.ResolutionDate,
                    transfer.SourceCommandId)
            || transfer.TransferId
                != CharacterResourceIds.DeriveWealthTransferId(transfer.SourceEventId))
        {
            throw new SimulationValidationException(
                $"Wealth transfer '{transfer.TransferId}' does not have exact deterministic IDs.");
        }
    }

    private void ValidateCrossRecordState()
    {
        foreach (IGrouping<EntityId, WealthLedgerEntry> group in ledgerEntries.Values
                     .GroupBy(item => item.TransferId))
        {
            WealthLedgerEntry[] entries = group.ToArray();
            if (entries.Length > 2
                || (entries.Length == 2 && !EntriesFormPair(entries[0], entries[1])))
            {
                throw new SimulationValidationException(
                    $"Wealth-ledger transfer '{group.Key}' has inconsistent participant entries.");
            }
        }
    }

    private static bool EntriesFormPair(WealthLedgerEntry first, WealthLedgerEntry second)
    {
        WealthLedgerEntry outgoing = first.Direction == WealthLedgerDirection.Outgoing ? first : second;
        WealthLedgerEntry incoming = first.Direction == WealthLedgerDirection.Incoming ? first : second;
        return outgoing.Direction == WealthLedgerDirection.Outgoing
            && incoming.Direction == WealthLedgerDirection.Incoming
            && outgoing.TransferId == incoming.TransferId
            && outgoing.CharacterId == incoming.CounterpartyCharacterId
            && outgoing.CounterpartyCharacterId == incoming.CharacterId
            && outgoing.Amount == incoming.Amount
            && outgoing.ResolutionDate == incoming.ResolutionDate
            && outgoing.ResolutionTurnIndex == incoming.ResolutionTurnIndex
            && outgoing.SourceCommandId == incoming.SourceCommandId
            && outgoing.SourceEventId == incoming.SourceEventId;
    }

    private static bool EntryMatchesTransfer(
        WealthLedgerEntry entry,
        WealthTransferRecord transfer)
    {
        EntityId expectedCharacter = entry.Direction == WealthLedgerDirection.Outgoing
            ? transfer.SourceCharacterId
            : transfer.RecipientCharacterId;
        EntityId expectedCounterparty = entry.Direction == WealthLedgerDirection.Outgoing
            ? transfer.RecipientCharacterId
            : transfer.SourceCharacterId;
        return entry.TransferId == transfer.TransferId
            && entry.CharacterId == expectedCharacter
            && entry.CounterpartyCharacterId == expectedCounterparty
            && entry.Amount == transfer.Amount
            && entry.ResolutionDate == transfer.ResolutionDate
            && entry.ResolutionTurnIndex == transfer.ResolutionTurnIndex
            && entry.SourceCommandId == transfer.SourceCommandId
            && entry.SourceEventId == transfer.SourceEventId;
    }

    private void ValidateRetentionBounds()
    {
        foreach (IGrouping<EntityId, WealthLedgerEntry> group in ledgerEntries.Values
                     .GroupBy(item => item.CharacterId))
        {
            if (group.Count() > CharacterResourceLimits.RecentLedgerEntriesPerCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{group.Key}' exceeds the recent wealth-ledger bound.");
            }

            if (history.TryGetValue(group.Key, out CharacterWealthHistoryAggregate? aggregate))
            {
                CampaignDate earliestRetained = group.Min(item => item.ResolutionDate);
                if (aggregate.LatestDate!.Value.CompareTo(earliestRetained) > 0)
                {
                    throw new SimulationValidationException(
                        $"Character wealth history for '{group.Key}' is newer than retained ledger entries.");
                }
            }
        }
    }

    private void ValidateRecordPoint(CampaignDate date, long turnIndex, string label)
    {
        if (!date.IsValid
            || turnIndex < 0
            || date.CompareTo(calendar.Date) > 0
            || turnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException($"{label} has an invalid or future record point.");
        }
    }

    private void ValidateCharacterAtDate(EntityId characterId, CampaignDate date, string label)
    {
        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            throw new SimulationValidationException($"{label} '{characterId}' does not exist.");
        }

        if (profile.BirthDate.CompareTo(date) > 0)
        {
            throw new SimulationValidationException($"{label} '{characterId}' is not born by '{date}'.");
        }
    }

    private void ValidateCharacter(EntityId characterId, string label)
    {
        if (!characterId.IsValid || !characters.TryGetCharacterProfile(characterId, out _))
        {
            throw new SimulationValidationException($"{label} '{characterId}' does not exist.");
        }
    }

    private static void ValidateStateVersion(int version, string label, EntityId id)
    {
        if (version != CharacterResourceContractVersions.State)
        {
            throw new SimulationValidationException(
                $"{label} '{id}' has unsupported contract version {version}.");
        }
    }

    private static void ValidateId(EntityId id, string label)
    {
        if (!id.IsValid)
        {
            throw new SimulationValidationException($"{label} is invalid.");
        }
    }

    private static void ValidateCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-resource campaign calendar is invalid.");
        }
    }

    private long GetStoredWealth(EntityId characterId) =>
        accounts.TryGetValue(characterId, out CharacterWealthAccountState? account)
            ? account.Wealth
            : 0;

    private IEnumerable<WealthLedgerEntry> OrderedLedgerEntries() => ledgerEntries.Values
        .OrderBy(item => item.CharacterId)
        .ThenBy(item => item.ResolutionTurnIndex)
        .ThenBy(item => item.ResolutionDate)
        .ThenBy(item => item.EntryId);

    private static WealthLedgerEntry CreateLedgerEntry(
        WealthTransferRecord transfer,
        EntityId characterId,
        EntityId counterpartyCharacterId,
        WealthLedgerDirection direction) => new(
        CharacterResourceContractVersions.State,
        CharacterResourceIds.DeriveWealthLedgerEntryId(
            transfer.TransferId,
            characterId,
            direction),
        transfer.TransferId,
        characterId,
        counterpartyCharacterId,
        direction,
        transfer.Amount,
        transfer.ResolutionDate,
        transfer.ResolutionTurnIndex,
        transfer.SourceCommandId,
        transfer.SourceEventId);

    private void ReplaceFrom(CharacterResourceWorldState source)
    {
        accounts.Clear();
        foreach ((EntityId characterId, CharacterWealthAccountState account) in source.accounts)
        {
            accounts.Add(characterId, Clone(account));
        }

        ledgerEntries.Clear();
        foreach ((EntityId entryId, WealthLedgerEntry entry) in source.ledgerEntries)
        {
            ledgerEntries.Add(entryId, Clone(entry));
        }

        history.Clear();
        foreach ((EntityId characterId, CharacterWealthHistoryAggregate aggregate) in source.history)
        {
            history.Add(characterId, Clone(aggregate));
        }

        calendar = source.calendar;
    }

    private static CharacterWealthAccountState Clone(CharacterWealthAccountState value) =>
        value with { };

    private static WealthLedgerEntry Clone(WealthLedgerEntry value) => value with { };

    private static CharacterWealthHistoryAggregate Clone(CharacterWealthHistoryAggregate value) =>
        value with { };

    private static TransferWealthAction Clone(TransferWealthAction value) => value with { };

    private static ICharacterResourceActionOutcome Clone(ICharacterResourceActionOutcome value) =>
        value switch
        {
            WealthTransferredOutcome outcome => outcome with
            {
                Transfer = outcome.Transfer with { },
                OutgoingEntry = Clone(outcome.OutgoingEntry),
                IncomingEntry = Clone(outcome.IncomingEntry),
            },
            WealthTransferCancelledOutcome outcome => outcome with { },
            _ => throw new SimulationValidationException(
                "Unsupported character-resource action outcome type."),
        };
}

internal sealed record CharacterResourceWorldUpdatePlan(CharacterResourceWorldState Candidate);

internal sealed record CharacterResourceInheritancePlan(
    WealthTransferredOutcome? WealthTransfer,
    CharacterResourceWorldUpdatePlan ResourcePlan);
