using System.Threading.RateLimiting;

namespace IFS.AIWeb.IntegrationTests;

public sealed class RateLimitPolicyTests
{
    [Fact]
    public void Policy_UsesOneSecondSlidingSegmentsAndActualCeilingSeconds()
    {
        Assert.Equal(5, SummaryRateLimitPolicy.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), SummaryRateLimitPolicy.Window);
        Assert.Equal(60, SummaryRateLimitPolicy.SegmentsPerWindow);
        var options = SummaryRateLimitPolicy.Options();
        Assert.Equal(5, options.PermitLimit); Assert.Equal(TimeSpan.FromMinutes(1), options.Window); Assert.Equal(60, options.SegmentsPerWindow);
        Assert.Equal(0, options.QueueLimit); Assert.True(options.AutoReplenishment);
        Assert.Equal(43, SummaryRateLimitPolicy.RetryAfterSeconds(new RetryLease(TimeSpan.FromSeconds(42.1))));
        Assert.Equal(12, SummaryRateLimitPolicy.RetryAfterSeconds(new RetryLease(TimeSpan.FromSeconds(11.2))));
    }

    [Fact]
    public async Task SlidingWindow_BlocksSixthAcrossAnOldFixedWindowBoundaryThenReleasesCapacity()
    {
        using var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 10, QueueLimit = 0, AutoReplenishment = true });
        for (var index = 0; index < 5; index++) Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.False(limiter.AttemptAcquire().IsAcquired);
        await Task.Delay(550);
        Assert.False(limiter.AttemptAcquire().IsAcquired);
        await Task.Delay(550);
        Assert.True(limiter.AttemptAcquire().IsAcquired);
    }

    private sealed class RetryLease(TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name) { metadata = retryAfter; return true; }
            metadata = null; return false;
        }
    }
}
