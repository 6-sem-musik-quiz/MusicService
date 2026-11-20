using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MusicService.Api.HealthChecks;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Extensions;

namespace MusicService.Api.Tests;

public class BlobStorageHealthCheckTests
{
    [Fact]
    public async Task Unhealthy_WhenNotConfigured()
    {
        var options = new BlobStorageOptions();
        var services = new ServiceCollection().AddBlobStorage(options).BuildServiceProvider();

        var result = await new BlobStorageHealthCheck(services, options).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not configured", result.Description);
    }

    [Fact]
    public async Task Healthy_WhenBucketCanBeListed()
    {
        var s3 = new StubS3(fail: false);
        var result = await Check(s3);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("media", s3.LastRequest?.BucketName);
        Assert.Equal(1, s3.LastRequest?.MaxKeys);
    }

    [Fact]
    public async Task Unhealthy_WhenS3Fails()
    {
        var result = await Check(new StubS3(fail: true));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static Task<HealthCheckResult> Check(IAmazonS3 s3)
    {
        var options = new BlobStorageOptions { Bucket = "media" };
        var services = new ServiceCollection().AddSingleton(s3).BuildServiceProvider();
        return new BlobStorageHealthCheck(services, options).CheckHealthAsync(new HealthCheckContext());
    }

    // An S3 client that never touches the network.
    private sealed class StubS3(bool fail)
        : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost:1", ForcePathStyle = true })
    {
        public ListObjectsV2Request? LastRequest { get; private set; }

        public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return fail
                ? Task.FromException<ListObjectsV2Response>(new AmazonS3Exception("Access Denied"))
                : Task.FromResult(new ListObjectsV2Response());
        }
    }
}
