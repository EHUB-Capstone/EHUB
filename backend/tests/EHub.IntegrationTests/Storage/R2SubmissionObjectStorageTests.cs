using EHub.Infrastructure.Options;
using EHub.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.IntegrationTests.Storage;

public sealed class R2SubmissionObjectStorageTests
{
    private static R2SubmissionObjectStorage CreateStorage() => new(
        Options.Create(new R2Options
        {
            AccountId = "acct123",
            AccessKeyId = "test-access-key",
            SecretAccessKey = "test-secret-key",
            BucketName = "ehub-test"
        }),
        NullLogger<R2SubmissionObjectStorage>.Instance);

    [Fact]
    public void CreatePresignedUpload_ProducesShortLivedPutUrlBoundToObjectKey()
    {
        using var storage = CreateStorage();
        var key = "submissions/abc/checkpoint-1/file.pdf";

        var upload = storage.CreatePresignedUpload(key, "application/pdf", 1024, TimeSpan.FromMinutes(10));

        var uri = new Uri(upload.Url);
        uri.Scheme.Should().Be("https");
        uri.Host.Should().Be("acct123.r2.cloudflarestorage.com");
        uri.AbsolutePath.Should().Be($"/ehub-test/{key}");
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["X-Amz-Expires"].Should().Be("600");
        query["X-Amz-Signature"].Should().NotBeNullOrEmpty();
        // Signing content-length makes R2 reject a body whose size differs from the declared size.
        query["X-Amz-SignedHeaders"].Should().Be("content-length;content-type;host");
        query["X-Amz-Credential"].Should().Contain("test-access-key").And.NotContain("test-secret-key");
        upload.Url.Should().NotContain("test-secret-key");
        upload.Headers.Should().Contain("Content-Type", "application/pdf");
    }

    [Fact]
    public void CreatePresignedDownloadUrl_IsShortLivedAndForcesAttachmentWithSafeFileName()
    {
        using var storage = CreateStorage();

        var url = storage.CreatePresignedDownloadUrl("submissions/abc/file.pdf", "Báo cáo \"final\".pdf", "application/pdf", TimeSpan.FromMinutes(5));

        var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        query["X-Amz-Expires"].Should().Be("300");
        query["response-content-type"].Should().Be("application/pdf");
        query["response-content-disposition"].Should().StartWith("attachment;")
            .And.Contain("filename*=UTF-8''B%C3%A1o%20c%C3%A1o%20%22final%22.pdf");
        url.Should().NotContain("test-secret-key");
    }

    [Theory]
    [InlineData("report.pdf", "attachment; filename=\"report.pdf\"; filename*=UTF-8''report.pdf")]
    [InlineData("a\"b;c.pdf", "attachment; filename=\"a_b_c.pdf\"; filename*=UTF-8''a%22b%3Bc.pdf")]
    [InlineData("../../evil.pdf", "attachment; filename=\"evil.pdf\"; filename*=UTF-8''evil.pdf")]
    [InlineData("ev\r\nil.pdf", "attachment; filename=\"evil.pdf\"; filename*=UTF-8''evil.pdf")]
    public void BuildContentDisposition_NeverLeaksPathsQuotesOrControlCharacters(string name, string expected) =>
        R2SubmissionObjectStorage.BuildContentDisposition(name).Should().Be(expected);
}
