using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class CharacterResourceCampaignTests
{
    private static readonly CampaignDate Date = new(191, 7, 14);

    [Fact]
    public void RegisteredCommandResolvesThroughExactEventAndAffectedIds()
    {
        CampaignSimulation simulation = CreateSimulation(10);
        EntityId source = Character(0);
        EntityId recipient = Character(1);
        EntityId commandId = new("command:resource/integration-success");
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            source,
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(recipient, 4)));

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterResourceActionResolvedEventPayload payload = Assert.IsType<
            CharacterResourceActionResolvedEventPayload>(campaignEvent.Payload);
        WealthTransferredOutcome outcome = Assert.IsType<WealthTransferredOutcome>(payload.Outcome);

        Assert.Equal(CharacterResourceIds.DeriveActionEventId(Date, commandId), campaignEvent.EventId);
        Assert.Equal(commandId, campaignEvent.CausalId);
        Assert.Equal(WorldState.GetCharacterResourceActionAffectedIds(payload), campaignEvent.AffectedIds);
        Assert.Contains(outcome.Transfer.TransferId, campaignEvent.AffectedIds);
        Assert.Contains(outcome.OutgoingEntry.EntryId, campaignEvent.AffectedIds);
        Assert.Contains(outcome.IncomingEntry.EntryId, campaignEvent.AffectedIds);
        Assert.Equal(6, simulation.World.CharacterResources.GetWealth(source));
        Assert.Equal(4, simulation.World.CharacterResources.GetWealth(recipient));
    }

    [Fact]
    public void CommandScheduledForLaterDayInTurnCommitsAgainstMonotonicResourceCalendar()
    {
        CampaignSimulation simulation = CreateSimulation(10);
        CampaignDate scheduledDate = Date.AddDays(2);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:resource/later-turn-day"),
            Character(0),
            scheduledDate,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(1), 1)));

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        Assert.Equal(scheduledDate, campaignEvent.ResolutionDate);
        Assert.Equal(9, simulation.World.CharacterResources.GetWealth(Character(0)));
        Assert.Equal(1, simulation.World.CharacterResources.GetWealth(Character(1)));
        Assert.All(
            simulation.World.CharacterResources.LedgerEntries,
            entry => Assert.Equal(scheduledDate, entry.ResolutionDate));
    }

    [Fact]
    public void ConcurrentValidatedSpendsCancelStaleOutcomeWithoutOverdraw()
    {
        CampaignSimulation simulation = CreateSimulation(10);
        EntityId source = Character(0);
        CampaignCommand first = CampaignCommand.Create(
            new EntityId("command:resource/concurrent-a"),
            source,
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(1), 8)));
        CampaignCommand second = CampaignCommand.Create(
            new EntityId("command:resource/concurrent-b"),
            source,
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(2), 8)));

        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);
        CharacterResourceActionResolvedEventPayload[] outcomes = simulation.ResolveTurn()
            .Select(campaignEvent => Assert.IsType<CharacterResourceActionResolvedEventPayload>(
                campaignEvent.Payload))
            .ToArray();

        Assert.Single(outcomes, outcome => outcome.Outcome is WealthTransferredOutcome);
        WealthTransferCancelledOutcome cancelled = Assert.IsType<WealthTransferCancelledOutcome>(
            Assert.Single(outcomes, outcome => outcome.Outcome is WealthTransferCancelledOutcome).Outcome);
        Assert.Equal(WealthTransferCancellationReason.InsufficientWealth, cancelled.Reason);
        Assert.Equal(2, simulation.World.CharacterResources.GetWealth(source));
        Assert.Equal(
            8,
            simulation.World.CharacterResources.GetWealth(Character(1))
            + simulation.World.CharacterResources.GetWealth(Character(2)));
        Assert.Equal(10, simulation.World.CharacterResources.Accounts.Sum(account => account.Wealth));
        Assert.Equal(2, simulation.World.CharacterResources.LedgerEntries.Count);
    }

    [Fact]
    public void ConcurrentValidIncomingTransfersCancelStaleRecipientOverflow()
    {
        CampaignSimulation simulation = CreateSimulation(
            (0, 1),
            (1, 1),
            (2, long.MaxValue - 1));
        CampaignCommand first = CampaignCommand.Create(
            new EntityId("command:resource/recipient-overflow-a"),
            Character(0),
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(2), 1)));
        CampaignCommand second = CampaignCommand.Create(
            new EntityId("command:resource/recipient-overflow-b"),
            Character(1),
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(2), 1)));

        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);
        CharacterResourceActionResolvedEventPayload[] outcomes = simulation.ResolveTurn()
            .Select(campaignEvent => Assert.IsType<CharacterResourceActionResolvedEventPayload>(
                campaignEvent.Payload))
            .ToArray();

        Assert.Single(outcomes, outcome => outcome.Outcome is WealthTransferredOutcome);
        WealthTransferCancelledOutcome cancelled = Assert.IsType<WealthTransferCancelledOutcome>(
            Assert.Single(outcomes, outcome => outcome.Outcome is WealthTransferCancelledOutcome).Outcome);
        Assert.Equal(WealthTransferCancellationReason.RecipientOverflow, cancelled.Reason);
        Assert.Equal(long.MaxValue, simulation.World.CharacterResources.GetWealth(Character(2)));
        Assert.Equal(
            1,
            simulation.World.CharacterResources.GetWealth(Character(0))
            + simulation.World.CharacterResources.GetWealth(Character(1)));
        Assert.Equal(2, simulation.World.CharacterResources.LedgerEntries.Count);
    }

    [Fact]
    public void ResourceStateIsCanonicalChecksumCoveredAndRestorable()
    {
        CampaignSimulation simulation = CreateSimulation(10);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:resource/checksum"),
            Character(0),
            Date,
            new CharacterResourceActionCommandPayload(new TransferWealthAction(Character(1), 3)));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.Single(simulation.ResolveTurn());
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum expected = SimulationChecksum.Compute(snapshot);
        CharacterResourceWorldSnapshot resources = snapshot.CharacterResources;
        WorldSnapshot shuffled = snapshot with
        {
            CharacterResources = resources with
            {
                Accounts = resources.Accounts.Reverse().ToArray(),
                LedgerEntries = resources.LedgerEntries.Reverse().ToArray(),
                History = resources.History.Reverse().ToArray(),
            },
        };

        Assert.Equal(expected, SimulationChecksum.Compute(shuffled));
        WorldState restored = WorldState.Restore(shuffled);
        Assert.Equal(expected, SimulationChecksum.Compute(restored.CaptureSnapshot()));
        CharacterWealthAccountState changedAccount = resources.Accounts[0] with
        {
            Wealth = resources.Accounts[0].Wealth + 1,
        };
        WorldSnapshot changed = snapshot with
        {
            CharacterResources = resources with
            {
                Accounts = resources.Accounts
                    .Select(account => account.CharacterId == changedAccount.CharacterId
                        ? changedAccount
                        : account)
                    .ToArray(),
            },
        };
        Assert.NotEqual(expected, SimulationChecksum.Compute(changed));
    }

    private static CampaignSimulation CreateSimulation(long sourceWealth) =>
        CreateSimulation((0, sourceWealth));

    private static CampaignSimulation CreateSimulation(params (int Index, long Wealth)[] accounts)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, 3)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:resource/integration_{index}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    new CampaignDate(160, 1, 1),
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
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            definitions.Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                [],
                [],
                CharacterConditionState.Default)).ToArray(),
            [],
            []);
        CharacterResourceWorldSnapshot resources = CharacterResourceWorldSnapshot.Empty with
        {
            Accounts = accounts.Select(account =>
                new CharacterWealthAccountState(
                    CharacterResourceContractVersions.State,
                    CharacterResourceIds.DeriveWealthAccountId(Character(account.Index)),
                    Character(account.Index),
                    account.Wealth)).ToArray(),
        };
        WorldState world = WorldState.Create(
            Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            resources);
        return new CampaignSimulation(world);
    }

    private static EntityId Character(int index) => new($"character:resource/integration_{index}");
}
