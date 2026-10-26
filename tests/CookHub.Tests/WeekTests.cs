using CookHub.Services;

namespace CookHub.Tests;

public class WeekTests
{
    [Theory]
    [InlineData("2026-10-26", "2026-10-26")] // Monday
    [InlineData("2026-10-28", "2026-10-26")] // Wednesday
    [InlineData("2026-11-01", "2026-10-26")] // Sunday belongs to the week that started the Monday before
    [InlineData("2027-01-01", "2026-12-28")] // Across a year boundary
    public void StartOf_returns_the_Monday_of_the_week(string date, string expectedMonday)
    {
        Assert.Equal(DateOnly.Parse(expectedMonday), Week.StartOf(DateOnly.Parse(date)));
    }

    [Fact]
    public void Days_run_Monday_to_Sunday()
    {
        Assert.Equal(DayOfWeek.Monday, Week.Days[0]);
        Assert.Equal(DayOfWeek.Sunday, Week.Days[6]);
        Assert.Equal(6, Week.DayIndex(DayOfWeek.Sunday));
        Assert.Equal(7, Week.DayIndex(null));
    }
}
