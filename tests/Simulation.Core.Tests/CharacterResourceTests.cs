using System.Diagnostics;
using System.Text.Json;
using Simulation.Core;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterResourceTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly CampaignCalendar Calendar = new(Date, 10);
    private readonly ITestOutputHelper output;

    public CharacterResourceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void StableIdsUseVersionedLengthFramingAndRemainLayerSpecific()
    {
        EntityId characterId = Character(0);
        EntityId commandId = new("command:resource/golden");
        EntityId eventId = CharacterResourceIds.DeriveActionEventId(Date, commandId);
        EntityId transferId = CharacterResourceIds.DeriveWealthTransferId(eventId);

        Assert.Equal(
            "wealth_account:sha256/f3f1add6730f16a0151ebd802c78156b9605d29baaaa1e666f25fad9af8ab1a1",
            CharacterResourceIds.DeriveWealthAccountId(characterId).Value);
        Assert.Equal(
            "event:sha256/ab9782f1a46af4e3b1cebbff1cc80765a17a5cd76ecd1fe229fecdf72b6e6594",
            eventId.Value);
        Assert.Equal(
            "wealth_transfer:sha256/c8f8d763a9665736eadc4af7e49e083f4a37cb83258f4518a7b3499b4baee7eb",
            transferId.Value);
        Assert.Equal(
            "wealth_ledger_entry:sha256/79bfe3239102406ccf38cd9477ba0c0e57152650e771b3e91672b38479b6862e",
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                characterId,
                WealthLedgerDirection.Outgoing).Value);
        Assert.NotEqual(
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                characterId,
                WealthLedgerDirection.Outgoing),
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                characterId,
                WealthLedgerDirection.Incoming));
        Assert.Throws<ArgumentException>(() => CharacterResourceIds.DeriveWealthAccountId(default));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                characterId,
                (WealthLedgerDirection)999));
    }

    [Fact]
    public void SparseAccountsCanonicalizeZeroAndNeverPersistEmptyRows()
    {
        CharacterWorldState characters = CreateCharacters(3);
        CharacterResourceWorldState state = NewState(
            characters,
            Account(Character(0), 7));

        Assert.Equal(7, state.GetWealth(Character(0)));
        Assert.Equal(0, state.GetWealth(Character(1)));
        Assert.False(state.TryGetAccount(Character(1), out _));

        Apply(state, Character(0), Character(1), 7, "sparse");

        Assert.Equal(0, state.GetWealth(Character(0)));
        Assert.Equal(7, state.GetWealth(Character(1)));
        CharacterWealthAccountState account = Assert.Single(state.Accounts);
        Assert.Equal(Character(1), account.CharacterId);
        Assert.DoesNotContain(state.CaptureSnapshot().Accounts, item => item.Wealth == 0);
    }

    [Fact]
    public void ConstructionCanonicalizesShuffledInputAndQueriesAreDefensive()
    {
        CharacterWorldState characters = CreateCharacters(4);
        CharacterResourceWorldState state = NewState(
            characters,
            Account(Character(0), 20),
            Account(Character(2), 5));
        Apply(state, Character(0), Character(1), 3, "canonical-a");
        Apply(state, Character(2), Character(3), 2, "canonical-b");
        CharacterResourceWorldSnapshot snapshot = state.CaptureSnapshot();
        CharacterResourceWorldState restored = new(
            snapshot with
            {
                Accounts = snapshot.Accounts.Reverse().ToArray(),
                LedgerEntries = snapshot.LedgerEntries.Reverse().ToArray(),
                History = snapshot.History.Reverse().ToArray(),
            },
            characters,
            Calendar);

        Assert.Equal(Serialize(snapshot), Serialize(restored.CaptureSnapshot()));
        CharacterWealthAccountState[] queried =
            Assert.IsType<CharacterWealthAccountState[]>(restored.Accounts);
        queried[0] = queried[0] with { Wealth = 999 };
        WealthLedgerEntry[] entries = Assert.IsType<WealthLedgerEntry[]>(restored.LedgerEntries);
        entries[0] = entries[0] with { Amount = 999 };
        CharacterResourceWorldSnapshot captured = restored.CaptureSnapshot();
        CharacterWealthAccountState[] capturedAccounts =
            Assert.IsType<CharacterWealthAccountState[]>(captured.Accounts);
        capturedAccounts[0] = capturedAccounts[0] with { Wealth = 999 };

        Assert.Equal(17, restored.GetWealth(Character(0)));
        Assert.All(restored.LedgerEntries, item => Assert.NotEqual(999, item.Amount));
    }

    [Fact]
    public void ConstructionRejectsMalformedAccountsAndSnapshotShape()
    {
        CharacterWorldState characters = CreateCharacters(3);
        CharacterWealthAccountState valid = Account(Character(0), 10);

        AssertInvalid(CharacterResourceWorldSnapshot.Empty with { ContractVersion = 2 }, characters);
        AssertInvalid(CharacterResourceWorldSnapshot.Empty with { Accounts = null! }, characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with { Accounts = [valid, valid] },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                Accounts = [valid with { AccountId = new EntityId("wealth_account:wrong") }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                Accounts = [valid with { CharacterId = new EntityId("character:resource/missing") }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with { Accounts = [valid with { Wealth = 0 }] },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with { Accounts = [valid with { Wealth = -1 }] },
            characters);
    }

    [Fact]
    public void ConstructionRejectsMalformedLedgerAndCrossRecordState()
    {
        CharacterWorldState characters = CreateCharacters(3);
        TransferFixture fixture = Transfer(Character(0), Character(1), 2, "malformed");

        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing with { ContractVersion = 2 }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing with { EntryId = new EntityId("wealth_ledger_entry:wrong") }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing with { Amount = 0 }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing with
                {
                    CounterpartyCharacterId = fixture.Outgoing.CharacterId,
                }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing, fixture.Outgoing],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                LedgerEntries = [fixture.Outgoing, fixture.Incoming with { Amount = 3 }],
            },
            characters);
    }

    [Fact]
    public void ConstructionRejectsMalformedHistoryAndExcessRetention()
    {
        CharacterWorldState characters = CreateCharacters(3);
        CharacterWealthHistoryAggregate valid = CharacterWealthHistoryAggregate.Empty(Character(0)) with
        {
            FoldedOutgoingCount = 1,
            FoldedOutgoingAmount = 2,
            EarliestDate = Date,
            LatestDate = Date,
        };
        WealthLedgerEntry[] excessive = Enumerable.Range(
                0,
                CharacterResourceLimits.RecentLedgerEntriesPerCharacter + 1)
            .Select(index => Transfer(
                Character(0),
                Character(1),
                1,
                $"excess-{index:D3}").Outgoing)
            .ToArray();

        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                History = [CharacterWealthHistoryAggregate.Empty(Character(0))],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                History = [valid with { FoldedOutgoingAmount = -1 }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                History = [valid with { EarliestDate = Date.AddDays(1) }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with
            {
                History = [valid with
                {
                    FoldedIncomingCount = long.MaxValue,
                    FoldedIncomingAmount = long.MaxValue,
                    FoldedOutgoingCount = 1,
                }],
            },
            characters);
        AssertInvalid(
            CharacterResourceWorldSnapshot.Empty with { LedgerEntries = excessive },
            characters);
    }

    [Fact]
    public void SubmissionValidationEnforcesParticipantsAgencyAmountAndBalances()
    {
        Dictionary<EntityId, CharacterConditionState> conditions = new()
        {
            [Character(2)] = CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            [Character(3)] = CharacterConditionState.Default with
            {
                VitalStatus = CharacterVitalStatus.Dead,
                HealthStatus = CharacterHealthStatus.Critical,
                IsIncapacitated = true,
            },
        };
        CharacterWorldState characters = CreateCharacters(5, conditions);
        CharacterResourceWorldState state = NewState(
            characters,
            Account(Character(0), 5),
            Account(Character(1), long.MaxValue));

        AssertInvalid(
            state.ValidateAction(
                Character(0),
                new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(0), 1)),
                Date,
                Calendar.TurnIndex),
            "self_transfer");
        AssertInvalid(Validate(state, Character(0), Character(4), 0), "invalid_wealth_amount");
        AssertInvalid(Validate(state, Character(4), Character(0), 1), "insufficient_wealth");
        AssertInvalid(Validate(state, Character(0), Character(1), 1), "wealth_overflow");
        AssertInvalid(Validate(state, Character(2), Character(0), 1), "actor_incapacitated");
        AssertInvalid(Validate(state, Character(3), Character(0), 1), "actor_dead");
        AssertInvalid(Validate(state, Character(0), Character(3), 1), "recipient_dead");
        Assert.True(Validate(state, Character(0), Character(2), 1).IsValid);
        AssertInvalid(
            Validate(
                state,
                Character(0),
                new EntityId("character:resource/missing"),
                1),
            "unknown_recipient");
    }

    [Fact]
    public void SubmissionValidationRequiresBothParticipantsToBeBornByResolution()
    {
        Dictionary<EntityId, CampaignDate> birthDates = new()
        {
            [Character(0)] = Date,
            [Character(1)] = Date,
        };
        CharacterResourceWorldState state = NewState(
            CreateCharacters(2, birthDates: birthDates),
            Account(Character(0), 2));
        CampaignDate beforeBirth = Date.AddDays(-1);

        CommandValidationResult result = state.ValidateAction(
            Character(0),
            new CharacterResourceActionCommandPayload(
                new TransferWealthAction(Character(1), 1)),
            beforeBirth,
            Calendar.TurnIndex);

        AssertInvalid(result, "actor_not_born");
        AssertInvalid(result, "recipient_not_born");
    }

    [Fact]
    public void ResolutionCancelsStaleUnderflowAndRecipientOverflowWithoutMutation()
    {
        CharacterWorldState characters = CreateCharacters(3);
        CharacterResourceWorldState underflow = NewState(
            characters,
            Account(Character(0), 1));
        PlannedTransfer insufficient = Plan(
            underflow,
            Character(0),
            Character(1),
            2,
            "cancel-underflow");
        Assert.Equal(
            WealthTransferCancellationReason.InsufficientWealth,
            Assert.IsType<WealthTransferCancelledOutcome>(insufficient.Payload.Outcome).Reason);
        string underflowBefore = Serialize(underflow.CaptureSnapshot());
        underflow.ApplyOutcome(
            insufficient.Payload,
            Date,
            Calendar.TurnIndex,
            insufficient.CommandId,
            insufficient.EventId);
        Assert.Equal(underflowBefore, Serialize(underflow.CaptureSnapshot()));

        CharacterResourceWorldState overflow = NewState(
            characters,
            Account(Character(0), 1),
            Account(Character(1), long.MaxValue));
        PlannedTransfer overflowing = Plan(
            overflow,
            Character(0),
            Character(1),
            1,
            "cancel-overflow");
        Assert.Equal(
            WealthTransferCancellationReason.RecipientOverflow,
            Assert.IsType<WealthTransferCancelledOutcome>(overflowing.Payload.Outcome).Reason);
        string overflowBefore = Serialize(overflow.CaptureSnapshot());
        overflow.ApplyOutcome(
            overflowing.Payload,
            Date,
            Calendar.TurnIndex,
            overflowing.CommandId,
            overflowing.EventId);
        Assert.Equal(overflowBefore, Serialize(overflow.CaptureSnapshot()));
    }

    [Fact]
    public void SuccessfulTransferIsAtomicConservedAndProducesExactPair()
    {
        CharacterResourceWorldState state = NewState(
            CreateCharacters(3),
            Account(Character(0), 10),
            Account(Character(1), 3));
        PlannedTransfer planned = Plan(state, Character(0), Character(1), 4, "success");
        string before = Serialize(state.CaptureSnapshot());

        state.PrevalidateOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
        state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);

        Assert.Equal(6, state.GetWealth(Character(0)));
        Assert.Equal(7, state.GetWealth(Character(1)));
        Assert.Equal(13, state.Accounts.Sum(item => item.Wealth));
        WealthLedgerEntry[] pair = state.LedgerEntries.ToArray();
        Assert.Equal(2, pair.Length);
        Assert.Equal(
            [WealthLedgerDirection.Outgoing, WealthLedgerDirection.Incoming],
            pair.OrderBy(item => item.Direction).Select(item => item.Direction));
        Assert.Single(pair.Select(item => item.TransferId).Distinct());
    }

    [Fact]
    public void TamperedAndStaleOutcomesApplyNothing()
    {
        CharacterResourceWorldState state = NewState(
            CreateCharacters(3),
            Account(Character(0), 10));
        PlannedTransfer planned = Plan(state, Character(0), Character(1), 6, "stale-original");
        WealthTransferredOutcome original = Assert.IsType<WealthTransferredOutcome>(planned.Payload.Outcome);
        CharacterResourceActionResolvedEventPayload tampered = planned.Payload with
        {
            Outcome = original with { RecipientWealthAfter = original.RecipientWealthAfter + 1 },
        };
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => state.ApplyOutcome(
            tampered,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));

        Apply(state, Character(0), Character(2), 5, "stale-intervening");
        string afterIntervening = Serialize(state.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId));
        Assert.Equal(afterIntervening, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void RecentLedgerRetainsExactlySixtyFourAndFoldsBothDirections()
    {
        CharacterResourceWorldState state = NewState(
            CreateCharacters(2),
            Account(Character(0), 100));
        for (int index = 0; index < 65; index++)
        {
            Apply(state, Character(0), Character(1), 1, $"fold-{index:D3}");
        }

        Assert.Equal(64, state.GetLedgerEntries(Character(0)).Count);
        Assert.Equal(64, state.GetLedgerEntries(Character(1)).Count);
        Assert.True(state.TryGetHistory(Character(0), out CharacterWealthHistoryAggregate? outgoing));
        Assert.Equal(1, outgoing.FoldedOutgoingCount);
        Assert.Equal(1, outgoing.FoldedOutgoingAmount);
        Assert.Equal(Date, outgoing.EarliestDate);
        Assert.Equal(Date, outgoing.LatestDate);
        Assert.True(state.TryGetHistory(Character(1), out CharacterWealthHistoryAggregate? incoming));
        Assert.Equal(1, incoming.FoldedIncomingCount);
        Assert.Equal(1, incoming.FoldedIncomingAmount);
    }

    [Fact]
    public void CheckedFoldOverflowRejectsWholeApplication()
    {
        CharacterResourceWorldState seed = NewState(
            CreateCharacters(2),
            Account(Character(0), 100));
        for (int index = 0; index < 64; index++)
        {
            Apply(seed, Character(0), Character(1), 1, $"overflow-seed-{index:D3}");
        }

        CharacterResourceWorldSnapshot snapshot = seed.CaptureSnapshot() with
        {
            History = [CharacterWealthHistoryAggregate.Empty(Character(1)) with
            {
                FoldedIncomingCount = 1,
                FoldedIncomingAmount = long.MaxValue,
                EarliestDate = Date,
                LatestDate = Date,
            }],
        };
        CharacterResourceWorldState state = new(snapshot, CreateCharacters(2), Calendar);
        PlannedTransfer planned = Plan(state, Character(0), Character(1), 1, "overflow-trigger");
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void ClosedActionAndOutcomeUnionsRoundTripWithExplicitDiscriminators()
    {
        CharacterResourceWorldState state = NewState(
            CreateCharacters(2),
            Account(Character(0), 10));
        PlannedTransfer planned = Plan(state, Character(0), Character(1), 1, "json");
        JsonSerializerOptions options = SimulationJson.CreateOptions();

        string commandJson = JsonSerializer.Serialize(
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(1), 1)),
            options);
        CharacterResourceActionCommandPayload command =
            JsonSerializer.Deserialize<CharacterResourceActionCommandPayload>(commandJson, options)!;
        Assert.IsType<TransferWealthAction>(command.Action);
        Assert.Contains("transfer_wealth.v1", commandJson, StringComparison.Ordinal);

        string eventJson = JsonSerializer.Serialize(planned.Payload, options);
        CharacterResourceActionResolvedEventPayload restored =
            JsonSerializer.Deserialize<CharacterResourceActionResolvedEventPayload>(eventJson, options)!;
        Assert.IsType<TransferWealthAction>(restored.Action);
        Assert.IsType<WealthTransferredOutcome>(restored.Outcome);
        Assert.Contains("wealth_transferred.v1", eventJson, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CharacterResourceActionCommandPayload>(
                commandJson.Replace("transfer_wealth.v1", "transfer_wealth.v999", StringComparison.Ordinal),
                options));
    }

    [Fact]
    public void ThousandCharacterAndTransferFixtureRecordsRawMeasurementWithoutThreshold()
    {
        CharacterWorldState characters = CreateCharacters(1_000);
        CharacterResourceWorldState state = new(
            CharacterResourceWorldSnapshot.Empty with
            {
                Accounts = Enumerable.Range(0, 1_000)
                    .Select(index => Account(Character(index), 1_000))
                    .ToArray(),
            },
            characters,
            Calendar);
        Stopwatch transfers = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            Apply(
                state,
                Character(index),
                Character((index + 1) % 1_000),
                1,
                $"performance-{index:D4}");
        }

        transfers.Stop();
        Stopwatch queryAndSnapshot = Stopwatch.StartNew();
        Assert.Equal(1_000_000, state.Accounts.Sum(item => item.Wealth));
        Assert.Equal(2_000, state.LedgerEntries.Count);
        CharacterResourceWorldSnapshot snapshot = state.CaptureSnapshot();
        string serialized = Serialize(snapshot);
        queryAndSnapshot.Stop();
        Assert.Equal(1_000, snapshot.Accounts.Count);
        Assert.NotEmpty(serialized);
        WorldState emptyWorld = WorldState.Create(
            Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty);
        WorldSnapshot integratedSnapshot = emptyWorld.CaptureSnapshot() with
        {
            Calendar = Calendar,
            CharacterResources = snapshot,
        };
        CampaignSimulation integrated = new(WorldState.Restore(integratedSnapshot));
        Stopwatch checksumTimer = Stopwatch.StartNew();
        SimulationChecksum checksum = SimulationChecksum.Compute(
            integrated.World.CaptureSnapshot());
        checksumTimer.Stop();
        SaveEnvelope envelope = SaveEnvelope.Create(
            "0.1.0",
            [],
            integrated,
            DateTimeOffset.Parse(
                "2026-07-15T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));
        string savePath = Path.Combine(
            Path.GetTempPath(),
            $"sp04c2-performance-{Guid.NewGuid():N}.save.gz");
        long saveBytes;
        Stopwatch saveTimer = Stopwatch.StartNew();
        try
        {
            SaveStore store = new();
            store.SaveAtomic(savePath, envelope);
            saveTimer.Stop();
            saveBytes = new FileInfo(savePath).Length;
            Stopwatch loadTimer = Stopwatch.StartNew();
            SaveEnvelope loaded = store.Load(savePath);
            loadTimer.Stop();
            Assert.Equal(checksum.Value, loaded.Checksum);
            Assert.Equal(2_000, loaded.Snapshot.CharacterResources.LedgerEntries.Count);
            output.WriteLine(
                $"SP-04C2 full-world fixture: checksum_ms={checksumTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"save_ms={saveTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"load_ms={loadTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"save_bytes={saveBytes}; checksum={checksum.Value}");
        }
        finally
        {
            File.Delete(savePath);
        }

        output.WriteLine(
            $"SP-04C2 raw fixture: transfer_ms={transfers.Elapsed.TotalMilliseconds:F3}; "
            + $"query_snapshot_json_ms={queryAndSnapshot.Elapsed.TotalMilliseconds:F3}; "
            + $"json_chars={serialized.Length}");
    }

    private static CharacterResourceWorldState NewState(
        CharacterWorldState characters,
        params CharacterWealthAccountState[] accounts) => new(
        CharacterResourceWorldSnapshot.Empty with { Accounts = accounts },
        characters,
        Calendar);

    private static PlannedTransfer Plan(
        CharacterResourceWorldState state,
        EntityId actor,
        EntityId recipient,
        long amount,
        string suffix)
    {
        EntityId commandId = new($"command:resource/{suffix}");
        EntityId eventId = CharacterResourceIds.DeriveActionEventId(Date, commandId);
        CharacterResourceActionResolvedEventPayload payload = state.PlanAction(
            actor,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(recipient, amount)),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId);
        return new PlannedTransfer(payload, commandId, eventId);
    }

    private static PlannedTransfer Apply(
        CharacterResourceWorldState state,
        EntityId actor,
        EntityId recipient,
        long amount,
        string suffix)
    {
        PlannedTransfer planned = Plan(state, actor, recipient, amount, suffix);
        state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);
        return planned;
    }

    private static CommandValidationResult Validate(
        CharacterResourceWorldState state,
        EntityId actor,
        EntityId recipient,
        long amount) => state.ValidateAction(
        actor,
        new CharacterResourceActionCommandPayload(new TransferWealthAction(recipient, amount)),
        Date,
        Calendar.TurnIndex);

    private static CharacterWealthAccountState Account(EntityId characterId, long wealth) => new(
        CharacterResourceContractVersions.State,
        CharacterResourceIds.DeriveWealthAccountId(characterId),
        characterId,
        wealth);

    private static TransferFixture Transfer(
        EntityId source,
        EntityId recipient,
        long amount,
        string suffix)
    {
        EntityId commandId = new($"command:resource/{suffix}");
        EntityId eventId = CharacterResourceIds.DeriveActionEventId(Date, commandId);
        EntityId transferId = CharacterResourceIds.DeriveWealthTransferId(eventId);
        WealthTransferRecord transfer = new(
            CharacterResourceContractVersions.State,
            transferId,
            source,
            recipient,
            amount,
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId);
        WealthLedgerEntry outgoing = new(
            CharacterResourceContractVersions.State,
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                source,
                WealthLedgerDirection.Outgoing),
            transferId,
            source,
            recipient,
            WealthLedgerDirection.Outgoing,
            amount,
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId);
        WealthLedgerEntry incoming = new(
            CharacterResourceContractVersions.State,
            CharacterResourceIds.DeriveWealthLedgerEntryId(
                transferId,
                recipient,
                WealthLedgerDirection.Incoming),
            transferId,
            recipient,
            source,
            WealthLedgerDirection.Incoming,
            amount,
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId);
        return new TransferFixture(transfer, outgoing, incoming);
    }

    private static CharacterWorldState CreateCharacters(
        int count,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null,
        IReadOnlyDictionary<EntityId, CampaignDate>? birthDates = null)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, count)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:resource/character_{index:D4}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    birthDates is not null
                        && birthDates.TryGetValue(id, out CampaignDate birthDate)
                        ? birthDate
                        : new CampaignDate(160, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = definitions
            .Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                [],
                [],
                conditions is not null
                    && conditions.TryGetValue(
                        definition.Id,
                        out CharacterConditionState? condition)
                        ? condition
                        : CharacterConditionState.Default))
            .ToArray();
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                [],
                states,
                [],
                []),
            Date);
    }

    private static EntityId Character(int index) =>
        new($"character:resource/c{index:D4}");

    private static void AssertInvalid(
        CharacterResourceWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters) =>
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterResourceWorldState(snapshot, characters, Calendar));

    private static void AssertInvalid(CommandValidationResult result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record PlannedTransfer(
        CharacterResourceActionResolvedEventPayload Payload,
        EntityId CommandId,
        EntityId EventId);

    private sealed record TransferFixture(
        WealthTransferRecord Transfer,
        WealthLedgerEntry Outgoing,
        WealthLedgerEntry Incoming);
}
