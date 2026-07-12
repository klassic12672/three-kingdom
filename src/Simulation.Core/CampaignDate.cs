using System.Text.Json.Serialization;

namespace Simulation.Core;

public readonly record struct CampaignDate : IComparable<CampaignDate>
{
    private static readonly int[] DaysBeforeMonth = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];

    [JsonConstructor]
    public CampaignDate(int year, int month, int day)
    {
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        int daysInMonth = GetDaysInMonth(year, month);
        if (day is < 1 || day > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Day { get; }

    [JsonIgnore]
    public bool IsValid => Year is >= 1 and <= 9999
        && Month is >= 1 and <= 12
        && Day >= 1
        && Day <= GetDaysInMonth(Year, Month);

    public static bool IsLeapYear(int year) => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);

    public static int GetDaysInMonth(int year, int month) => month switch
    {
        2 => IsLeapYear(year) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        >= 1 and <= 12 => 31,
        _ => throw new ArgumentOutOfRangeException(nameof(month)),
    };

    public CampaignDate AddDays(int days)
    {
        long target = checked(ToDayNumber() + days);
        if (target is < 0 or > 3_652_058)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        int low = 1;
        int high = 9999;
        while (low <= high)
        {
            int candidate = low + ((high - low) / 2);
            long firstDay = DaysBeforeYear(candidate);
            long nextYear = candidate == 9999 ? 3_652_059 : DaysBeforeYear(candidate + 1);
            if (target < firstDay)
            {
                high = candidate - 1;
            }
            else if (target >= nextYear)
            {
                low = candidate + 1;
            }
            else
            {
                int dayOfYear = (int)(target - firstDay);
                int month = 1;
                while (dayOfYear >= GetDaysInMonth(candidate, month))
                {
                    dayOfYear -= GetDaysInMonth(candidate, month++);
                }

                return new CampaignDate(candidate, month, dayOfYear + 1);
            }
        }

        throw new InvalidOperationException("Date conversion failed.");
    }

    public int CompareTo(CampaignDate other) => ToDayNumber().CompareTo(other.ToDayNumber());

    public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

    private long ToDayNumber() => DaysBeforeYear(Year) + DaysBeforeMonth[Month - 1] + Day - 1
        + (Month > 2 && IsLeapYear(Year) ? 1 : 0);

    private static long DaysBeforeYear(int year)
    {
        long prior = year - 1L;
        return (prior * 365) + (prior / 4) - (prior / 100) + (prior / 400);
    }
}

public readonly record struct CampaignCalendar(CampaignDate Date, long TurnIndex)
{
    public int DaysInCurrentTurn => TurnIndex % 2 == 0 ? 3 : 4;

    public IEnumerable<CampaignDate> CurrentTurnDays()
    {
        for (int offset = 0; offset < DaysInCurrentTurn; offset++)
        {
            yield return Date.AddDays(offset);
        }
    }

    public CampaignCalendar NextTurn() => new(Date.AddDays(DaysInCurrentTurn), checked(TurnIndex + 1));
}

public enum ResolutionPhase
{
    StartOfDay = 0,
    Commands = 100,
    Systems = 200,
    BackgroundCommit = 300,
    EndOfDay = 400,
}
