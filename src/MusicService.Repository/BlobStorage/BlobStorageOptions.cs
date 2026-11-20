namespace MusicService.Repository.BlobStorage;

// Where the song files live: Garage, an S3-compatible object store in the cluster.
// Bound from the "BlobStorage" config section, e.g. BlobStorage__ServiceUrl in Kubernetes.
// The key comes from OpenBao kv/quiz/blobstorage (see the gitops repo, README "BlobStorage").
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    // e.g. http://garage.quiz.svc.cluster.local:3900
    public string ServiceUrl { get; set; } = "";

    public string Bucket { get; set; } = "media";

    // Garage's s3_region. Only used to sign requests.
    public string Region { get; set; } = "garage";

    public string AccessKeyId { get; set; } = "";

    public string SecretAccessKey { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceUrl)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey);
}
