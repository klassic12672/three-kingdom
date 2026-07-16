using System.Text.Json;
using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class CharacterWoundWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 1, 1);
    private static readonly EntityId CharacterId = new("character:test/wound");

    [Fact]
    public void WoundUsesConditionEnvelopeAndRoundTripsExactOutcome()
    {
        CampaignSimulation simulation = CreateSimulation(CharacterConditionState.Default);
        ApplyCharacterWoundAction action = new(
            CharacterId,
            CharacterConditionState.Default,
            CharacterHealthStatus.Injured,
            ResultingIncapacitated: false);
        CampaignCommand command = Command("valid", action);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent resolved = Assert.Single(simulation.ResolveTurn());
        CharacterConditionActionResolvedEventPayload payload =
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(
                resolved.Payload);
        Assert.Equal(action, Assert.IsType<ApplyCharacterWoundAction>(payload.Action));
        CharacterConditionChangedOutcome outcome =
            Assert.IsType<CharacterConditionChangedOutcome>(payload.Outcome);
        Assert.Equal(CharacterHealthStatus.Injured, outcome.Change.CurrentCondition.HealthStatus);
        Assert.False(outcome.Change.CurrentCondition.IsIncapacitated);
        Assert.NotNull(outcome.MarriageChanges);
        Assert.Equal(
            Serialize(payload),
            Serialize(JsonSerializer.Deserialize<CharacterConditionActionResolvedEventPayload>(
                Serialize(payload),
                SimulationJson.CreateOptions())!));
    }

    [Theory]
    [InlineData(CharacterHealthStatus.Healthy, false)]
    [InlineData(CharacterHealthStatus.Injured, false)]
    [InlineData(CharacterHealthStatus.Ill, false)]
    [InlineData(CharacterHealthStatus.Critical, false)]
    public void WoundRejectsNoOpImprovementAndNonIncapacitatingCritical(
        CharacterHealthStatus resultingHealth,
        bool resultingIncapacitated)
    {
        CharacterConditionState current = CharacterConditionState.Default with
        {
            HealthStatus = CharacterHealthStatus.Injured,
        };
        CampaignSimulation simulation = CreateSimulation(current);
        CommandValidationResult validation = simulation.Submit(Command(
            $"invalid-{resultingHealth}",
            new ApplyCharacterWoundAction(
                CharacterId,
                current,
                resultingHealth,
                resultingIncapacitated)));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void WoundCannotRestoreCapacityAndStaleOrReplayedCommandsFail()
    {
        CharacterConditionState incapacitated = CharacterConditionState.Default with
        {
            IsIncapacitated = true,
        };
        CampaignSimulation capacity = CreateSimulation(incapacitated);
        Assert.False(capacity.Submit(Command(
            "restore-capacity",
            new ApplyCharacterWoundAction(
                CharacterId,
                incapacitated,
                CharacterHealthStatus.Injured,
                ResultingIncapacitated: false))).IsValid);

        CampaignSimulation simulation = CreateSimulation(CharacterConditionState.Default);
        CampaignCommand applied = Command(
            "stale-source",
            new ApplyCharacterWoundAction(
                CharacterId,
                CharacterConditionState.Default,
                CharacterHealthStatus.Injured,
                ResultingIncapacitated: false));
        Assert.True(simulation.Submit(applied).IsValid);
        _ = simulation.ResolveTurn();

        Assert.False(simulation.Submit(Command(
            "stale",
            new ApplyCharacterWoundAction(
                CharacterId,
                CharacterConditionState.Default,
                CharacterHealthStatus.Critical,
                ResultingIncapacitated: true))).IsValid);
        Assert.False(simulation.Submit(applied).IsValid);
    }

    private static CampaignSimulation CreateSimulation(
        CharacterConditionState condition)
    {
        EntityId nameKey = new("loc:test/wound");
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            [
                new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    CharacterId,
                    nameKey,
                    new CampaignDate(150, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(CharacterId),
                    null,
                    null,
                    []),
            ],
            [],
            [],
            [
                new CharacterState(
                    CharacterContractVersions.State,
                    CharacterId,
                    [],
                    [],
                    condition),
            ],
            [],
            []);
        return new CampaignSimulation(WorldState.Create(
            Date,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty));
    }

    private static CampaignCommand Command(
        string suffix,
        ApplyCharacterWoundAction action) => CampaignCommand.Create(
        new($"command:test/wound-{suffix.ToLowerInvariant()}"),
        CharacterConditionSystem.AuthoritativeActorId,
        Date,
        new CharacterConditionActionCommandPayload(action));

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        SimulationJson.CreateOptions());
}
