using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class CalendarAndIdTests
{
    [Theory]
    [InlineData("character:liu_bei", true)]
    [InlineData("region:china/xu-zhou", true)]
    [InlineData("Character:liu_bei", false)]
    [InlineData("missing_namespace", false)]
    [InlineData("region:", false)]
    [InlineData("region:bad value", false)]
    public void EntityId_ValidatesNamespacedOrdinalSyntax(string value, bool expected)
    {
        Assert.Equal(expected, EntityId.TryParse(value, out _));
    }

    [Fact]
    public void EntityId_UsesOrdinalOrdering()
    {
        EntityId[] actual =
        [
            new("test:z"),
            new("test:a10"),
            new("test:a2"),
        ];

        Array.Sort(actual);
        Assert.Equal(["test:a10", "test:a2", "test:z"], actual.Select(item => item.Value));
    }

    [Fact]
    public void Calendar_AlternatesThreeAndFourDayTurns()
    {
        CampaignCalendar calendar = new(new CampaignDate(200, 1, 1), 0);

        Assert.Equal(3, calendar.DaysInCurrentTurn);
        calendar = calendar.NextTurn();
        Assert.Equal(new CampaignDate(200, 1, 4), calendar.Date);
        Assert.Equal(4, calendar.DaysInCurrentTurn);
        calendar = calendar.NextTurn();
        Assert.Equal(new CampaignDate(200, 1, 8), calendar.Date);
    }

    [Theory]
    [InlineData(2000, 2, 28, 1, 2000, 2, 29)]
    [InlineData(1900, 2, 28, 1, 1900, 3, 1)]
    [InlineData(1999, 12, 31, 1, 2000, 1, 1)]
    [InlineData(2400, 2, 29, -1, 2400, 2, 28)]
    public void DateArithmetic_UsesProjectOwnedProlepticGregorianRules(
        int year,
        int month,
        int day,
        int delta,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        Assert.Equal(
            new CampaignDate(expectedYear, expectedMonth, expectedDay),
            new CampaignDate(year, month, day).AddDays(delta));
    }

    [Fact]
    public void DateArithmetic_RemainsExactAcrossFourHundredYears()
    {
        CampaignDate start = new(1600, 3, 1);
        Assert.Equal(new CampaignDate(2000, 3, 1), start.AddDays(146_097));
    }

    [Fact]
    public void DuplicatePersistentEntityIds_FailBeforeWorldCreation()
    {
        SyntheticEntitySnapshot entity = new(new EntityId("test:duplicate"), SimulationTier.Full, 1, 1, 1, []);

        Assert.Throws<SimulationValidationException>(() =>
            WorldState.Create(new CampaignDate(200, 1, 1), 1, [entity, entity]));
    }
}
