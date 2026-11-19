using CookHub.Services;

namespace CookHub.Tests;

public class StepTimeTests
{
    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("01:30", 90)]
    [InlineData("1:05", 65)]
    [InlineData(" 12:00 ", 720)]
    [InlineData("125:59", 7559)]
    public void Parse_reads_minutes_and_seconds(string text, int expected)
    {
        Assert.Equal(expected, StepTime.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("90")]
    [InlineData("1:60")]
    [InlineData("1:5")]
    [InlineData("1000:00")]
    [InlineData("-1:00")]
    [InlineData("1:00:00")]
    [InlineData("ab:cd")]
    public void Parse_returns_null_for_anything_else(string? text)
    {
        Assert.Null(StepTime.Parse(text));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("02:15", true)]
    [InlineData("2.15", false)]
    public void IsValid_allows_no_time_or_a_valid_time(string? text, bool expected)
    {
        Assert.Equal(expected, StepTime.IsValid(text));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(7559, "125:59")]
    public void Format_writes_two_digit_minutes_and_seconds(int seconds, string expected)
    {
        Assert.Equal(expected, StepTime.Format(seconds));
    }
}
