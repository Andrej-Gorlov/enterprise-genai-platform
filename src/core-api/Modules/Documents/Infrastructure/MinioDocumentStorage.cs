
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using EnterpriseGenAI.Core.Api.Modules.Documents.Application;
using Microsoft.Extensions.Options;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;

public sealed class MinioDocumentStorage : IDocumentStorage
{
    private readonly DocumentS3Clients _s3Clients;
    private readonly DocumentStorageOptions _options;

    public MinioDocumentStorage(DocumentS3Clients s3Clients, IOptions<DocumentStorageOptions> optionsAccessor)
    {
        _s3Clients = s3Clients ?? throw new ArgumentNullException(nameof(s3Clients));
        _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    }

    
    public string CreatePresignedUploadUrl(string temporaryObjectKey, DateTime expiresAtUtc)
    {
        ValidateTemporaryObjectKey(temporaryObjectKey);

        if (expiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Время истечения должно быть указано в UTC.", nameof(expiresAtUtc));
        }

        var nowUtc = DateTime.UtcNow;

        if (expiresAtUtc <= nowUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Время истечения должно относиться к будущему.");
        }

        if (expiresAtUtc > nowUtc.AddMinutes(_options.PresignedUrlExpiryMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Превышен максимальный срок действия подписанной ссылки.");
        }

        var endpoint = new Uri(_options.PublicEndpoint);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = temporaryObjectKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAtUtc,
            Protocol = endpoint.Scheme == Uri.UriSchemeHttps ? Protocol.HTTPS : Protocol.HTTP
        };

        return _s3Clients.PresigningClient.GetPreSignedURL(request);
    }


    public async Task<long?> GetTemporaryObjectSizeAsync(string temporaryObjectKey, CancellationToken cancellationToken)
    {
        ValidateTemporaryObjectKey(temporaryObjectKey);

        var request = new GetObjectMetadataRequest
        {
            BucketName = _options.BucketName,
            Key = temporaryObjectKey
        };

        try
        {
            var response = await _s3Clients.InternalClient.GetObjectMetadataAsync(request, cancellationToken);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    
    private static void ValidateTemporaryObjectKey(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        const string prefix = "uploads/";

        if (objectKey.Length > 1024 || !objectKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Некорректный ключ временного объекта.", nameof(objectKey));
        }

        var relativeKey = objectKey[prefix.Length..];

        if (relativeKey.Length == 0 ||
            relativeKey.Contains('\\') ||
            relativeKey.Any(char.IsControl) ||
            relativeKey.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException("Временный ключ содержит недопустимые сегменты.", nameof(objectKey));
        }
    }

}
