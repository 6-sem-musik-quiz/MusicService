using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Repository.BlobStorage;

namespace MusicService.Repository.Extensions;

public static class BlobStorageExtensions
{
    // Registers the S3 client for Garage (the C4 component "BlobStorageClient").
    // The client is built on first use, so the app still starts without the config
    // (tests, local runs); the /health/ready check then reports it as unhealthy.
    public static IServiceCollection AddBlobStorage(this IServiceCollection services, BlobStorageOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IAmazonS3>(_ =>
        {
            if (!options.IsConfigured)
                throw new InvalidOperationException(
                    $"Blob storage is not configured: set {BlobStorageOptions.SectionName}:ServiceUrl, AccessKeyId and SecretAccessKey.");

            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region,
                ForcePathStyle = true, // Garage serves buckets by path, not by subdomain
                // Only send/check checksums when S3 demands them; newer SDK defaults add
                // checksum headers that S3-compatible stores don't always expect.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), config);
        });

        return services;
    }
}
