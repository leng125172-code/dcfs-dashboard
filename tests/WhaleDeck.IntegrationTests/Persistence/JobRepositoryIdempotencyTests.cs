using WhaleDeck.Infrastructure.Persistence;

namespace WhaleDeck.IntegrationTests.Persistence;

public sealed class JobRepositoryIdempotencyTests
{
    [Fact]
    public void JsonRequestComparisonIgnoresJsonbObjectFormattingAndPropertyOrder()
    {
        const string submitted = "{\"resourceId\":null,\"parameters\":{},\"planHash\":null}";
        const string persistedJsonb = "{\"planHash\": null, \"parameters\": {}, \"resourceId\": null}";

        Assert.True(JobRepository.JsonRequestsEqual(submitted, persistedJsonb));
    }

    [Fact]
    public void JsonRequestComparisonStillRejectsDifferentOperations()
    {
        const string original = "{\"resourceId\":\"container-a\",\"parameters\":{\"force\":\"false\"}}";
        const string changed = "{\"parameters\":{\"force\":\"true\"},\"resourceId\":\"container-a\"}";

        Assert.False(JobRepository.JsonRequestsEqual(original, changed));
    }
}
