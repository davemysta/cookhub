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
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("13", 13)]
    [InlineData("209", 129)]
    [InlineData("1423", 863)]
    [InlineData("0130", 90)]
    [InlineData(" 59 ", 59)]
    [InlineData("99959", StepTime.MaxSeconds)]
    public void Parse_reads_digits_as_minutes_then_two_digits_of_seconds(string text, int expected)
    {
        Assert.Equal(expected, StepTime.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("90")]
    [InlineData("175")]
    [InlineData("100000")]
    [InlineData("1 23")]
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
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("1", "00:01")]
    [InlineData("13", "00:13")]
    [InlineData("209", "02:09")]
    [InlineData("1423", "14:23")]
    [InlineData("2:05", "02:05")]
    [InlineData("1:5", "1:5")]
    public void Normalize_writes_valid_times_as_mm_ss_and_leaves_the_rest(string? text, string? expected)
    {
        Assert.Equal(expected, StepTime.Normalize(text));
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
