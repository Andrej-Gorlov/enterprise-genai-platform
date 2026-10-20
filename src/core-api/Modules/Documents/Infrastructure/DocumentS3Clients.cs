using Amazon.S3;
using Microsoft.Extensions.Options;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;

public sealed class DocumentS3Clients : IDisposable
{
    public IAmazonS3 InternalClient { get; }
    public IAmazonS3 PresigningClient { get; }

    public DocumentS3Clients(IOptions<DocumentStorageOptions> optionsAccessor)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        var options = optionsAccessor.Value;

        var internalConfig = new AmazonS3Config
        {
            ServiceURL = options.InternalEndpoint.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        var presigningConfig = new AmazonS3Config
        {
            ServiceURL = options.PublicEndpoint.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        InternalClient = new AmazonS3Client(options.AccessKey, options.SecretKey, internalConfig);
        PresigningClient = new AmazonS3Client(options.AccessKey, options.SecretKey, presigningConfig);
    }

    public void Dispose()
    {
        InternalClient?.Dispose();
        PresigningClient?.Dispose();
    }
}