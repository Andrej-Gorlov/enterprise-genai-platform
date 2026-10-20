
using System.Net;
using System.Text;
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

    public async Task<StoredOriginalResult> StoreOriginalAsync(
        string temporaryObjectKey,
        Guid documentId,
        int contentVersion,
        long expectedSize,
        string expectedContentType,
        CancellationToken cancellationToken)
    {
        // 1. Валидация входных параметров
        ValidateTemporaryObjectKey(temporaryObjectKey);

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор документа не может быть пустым.", nameof(documentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentType);

        const long maxAllowedBytes = 20L * 1024 * 1024; // 20 MiB лимит
        if (expectedSize > maxAllowedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize), "Размер файла превышает допустимый предел в 20 MiB.");
        }

        // 2. Чтение временного объекта через InternalClient с жестким лимитом памяти во время чтения потока
        var getRequest = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = temporaryObjectKey
        };

        byte[] fileBytes;
        using (var response = await _s3Clients.InternalClient.GetObjectAsync(getRequest, cancellationToken))
        using (var responseStream = response.ResponseStream)
        using (var memoryStream = new MemoryStream())
        {
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > maxAllowedBytes)
                {
                    throw new InvalidOperationException("Размер загруженного объекта превышает установленный лимит в 20 MiB.");
                }
                memoryStream.Write(buffer, 0, bytesRead);
            }

            fileBytes = memoryStream.ToArray();
        }

        // 3. Проверка фактического размера
        if (fileBytes.Length != expectedSize)
        {
            throw new InvalidOperationException($"Фактический размер файла ({fileBytes.Length}) не совпадает с ожидаемым ({expectedSize}).");
        }

        // 4. Валидация содержимого в зависимости от MIME-типа
        if (expectedContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<byte> pdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D];

            if (fileBytes.Length < pdfHeader.Length || !fileBytes.AsSpan(0, pdfHeader.Length).SequenceEqual(pdfHeader))
            {
                throw new InvalidOperationException("Недействительная сигнатура PDF-файла.");
            }
        }
        else if (
            expectedContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
            expectedContentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

                var text = strictUtf8.GetString(fileBytes);

                foreach (var c in text)
                {
                    if (c == '\0' || (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                    {
                        throw new InvalidOperationException("Текст содержит недопустимые управляющие символы.");
                    }
                }
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidOperationException("Файл содержит некорректную кодировку UTF-8.", ex);
            }
        }
        else
        {
            throw new ArgumentException("Неподдерживаемый MIME-тип документа.", nameof(expectedContentType));
        }

        // 5. Вычисление SHA-256 хэш-суммы
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(fileBytes);
        string computedChecksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // 6. Формирование постоянного ключа
        var permanentKey = $"documents/{documentId:N}/v{contentVersion}/original";

        // 7. Сохранение в MinIO с условием IfNoneMatch = "*"
        try
        {
            using var putStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = permanentKey,
                InputStream = putStream,
                ContentType = expectedContentType,
                IfNoneMatch = "*"
            };

            await _s3Clients.InternalClient.PutObjectAsync(putRequest, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // 8. Обработка HTTP 412 (объект уже существует) — проверяем идемпотентность

            var existingGetRequest = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = permanentKey
            };

            using var existingResponse = await _s3Clients.InternalClient.GetObjectAsync(existingGetRequest, cancellationToken);
            using var existingMemory = new MemoryStream();

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await existingResponse.ResponseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (existingMemory.Length > maxAllowedBytes - bytesRead)
                {
                    throw new InvalidOperationException("Существующий оригинал превышает лимит 20 MiB.");
                }
                await existingMemory.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            long existingSize = existingMemory.Length;
            string existingChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(existingMemory.ToArray())).ToLowerInvariant();

            if (existingSize == expectedSize && existingChecksum == computedChecksum)
            {
                // Успешный идемпотентный повтор
                return new StoredOriginalResult(permanentKey, expectedSize, computedChecksum);
            }

            // Конфликт: постоянный ключ уже существует с другим содержимым
            throw new InvalidOperationException("Конфликт версий: постоянный ключ уже существует с другим содержимым.");

        }
        return new StoredOriginalResult(permanentKey, expectedSize, computedChecksum);
    }
}
