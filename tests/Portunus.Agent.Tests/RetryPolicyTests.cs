using System;
using System.Linq;

using Portunus.Agent.Fetch;

using Xunit;

namespace Portunus.Agent.Tests;

[Trait("Category", "unit")]
public sealed class RetryPolicyTests
{
    private static readonly TimeSpan First = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Max = TimeSpan.FromMinutes(10);

    [Fact]
    public void Next_FailuresInARow_DoubleFromFirstWithinAFifthAndNeverBelowFirst()
    {
        RetryPolicy policy = new(First, Max, new Random(7));

        TimeSpan[] pauses = [.. Enumerable.Range(0, 8).Select(_ => policy.Next(FailureClass.Unreachable))];

        TimeSpan[] expected = [First, First * 2, First * 4, First * 8, First * 16, Max, Max, Max];
        for (int index = 0; index < pauses.Length; index++)
        {
            Assert.InRange(pauses[index], Larger(expected[index] * 0.8, First), expected[index] * 1.2);
        }
    }

    [Fact]
    public void Next_RateLimit_IsMaxAtOnce()
    {
        RetryPolicy policy = new(First, Max, new Random(7));

        TimeSpan pause = policy.Next(FailureClass.RateLimited);
        TimeSpan after = policy.Next(FailureClass.Unreachable);

        Assert.InRange(pause, Max * 0.8, Max * 1.2);
        Assert.InRange(after, Max * 0.8, Max * 1.2);
    }

    [Fact]
    public void Next_ManyPauses_SpreadEitherWay()
    {
        RetryPolicy[] policies = [.. Enumerable.Range(0, 200).Select(seed => new RetryPolicy(First, Max, new Random(seed)))];

        TimeSpan[] seconds = [.. policies.Select(policy => { policy.Next(FailureClass.Unreachable); return policy.Next(FailureClass.Unreachable); })];

        Assert.Contains(seconds, pause => pause < First * 2);
        Assert.Contains(seconds, pause => pause > First * 2);
    }

    [Theory]
    [InlineData("[ERROR] (429) Too Many Requests", "RateLimited")]
    [InlineData("[ERROR] (401) Unauthorized", "Refused")]
    [InlineData("[ERROR] (403) Forbidden", "Refused")]
    [InlineData("[ERROR] could not reach the server", "Unreachable")]
    [InlineData("", "Unreachable")]
    public void Classify_StandardError_AnswersItsClass(string standardError, string expected)
    {
        Assert.Equal(Enum.Parse<FailureClass>(expected), OpRunner.Classify(System.Text.Encoding.UTF8.GetBytes(standardError)));
    }

    private static TimeSpan Larger(TimeSpan one, TimeSpan other)
    {
        return one > other ? one : other;
    }
}