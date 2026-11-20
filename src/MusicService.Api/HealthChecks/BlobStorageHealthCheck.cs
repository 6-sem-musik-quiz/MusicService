using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MusicService.Repository.BlobStorage;

namespace MusicService.Api.HealthChecks;

// Part of /health/ready: lists at most one object in the bucket, which proves
// that Garage answers, the key is valid and it may read the bucket.
public sealed class BlobStorageHealthCheck(IServiceProvider services, BlobStorageOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var s3 = services.GetRequiredService<IAmazonS3>();
            await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.Bucket, MaxKeys = 1 }, cancellationToken);
            return HealthCheckResult.Healthy($"Bucket '{options.Bucket}' is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Bucket '{options.Bucket}' is not reachable: {ex.Message}", ex);
        }
    }
}
