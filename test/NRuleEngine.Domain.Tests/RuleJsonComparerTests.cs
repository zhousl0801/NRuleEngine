using System.Text.Json;
using NRuleEngine.Domain.Expressions;
using Xunit;

namespace NRuleEngine.Domain.Tests;

public sealed class RuleJsonComparerTests
{
    [Theory]
    [InlineData("1", "1.0", true)]
    [InlineData("10e-1", "1", true)]
    [InlineData("-1.20e1", "-12", true)]
    [InlineData("0.00100", "1e-3", true)]
    [InlineData("-0e20", "0", true)]
    [InlineData("1.0000000000000000000000000001", "1", false)]
    [InlineData("1.00000000000000000000000000001", "1", false)]
    [InlineData("1e-29", "0", false)]
    [InlineData("1e29", "1e28", false)]
    public void ComparesNumbersWithoutRoundingOrUnderflow(string expected, string actual, bool equal)
    {
        using var left = JsonDocument.Parse(expected);
        using var right = JsonDocument.Parse(actual);
        Assert.Equal(equal, RuleJsonComparer.Equals(left.RootElement, right.RootElement));
    }
}
