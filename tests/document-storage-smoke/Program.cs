
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;
using Microsoft.Extensions.Options;

var accessKey = Environment.GetEnvironmentVariable("MINIO_APP_ACCESS_KEY") ?? throw new InvalidOperationException("MINIO_APP_ACCESS_KEY is missing.");
var secretKey = Environment.GetEnvironmentVariable("MINIO_APP_SECRET_KEY") ?? throw new InvalidOperationException("MINIO_APP_SECRET_KEY is missing.");
var bucketName = Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME") ?? throw new InvalidOperationException("MINIO_BUCKET_NAME is missing.");

Console.WriteLine("Переменные окружения загружены.");

var storageOptions = new DocumentStorageOptions
{
    InternalEndpoint = "http://localhost:9000",
    PublicEndpoint = "http://localhost:9000",
    BucketName = bucketName,
    AccessKey = accessKey,
    SecretKey = secretKey,
    PresignedUrlExpiryMinutes = 5
};

var optionsWrapper = Options.Create(storageOptions);

using var s3Clients = new DocumentS3Clients(optionsWrapper);

var documentStorage = new MinioDocumentStorage(s3Clients, optionsWrapper);
var objectKey = $"uploads/smoke/{Guid.NewGuid():N}.txt";
var permanentKey = $"documents/smoke/{Guid.NewGuid():N}/v1/original.txt";
var contentBytes = Encoding.UTF8.GetBytes("MinIO smoke test");

var docId = Guid.NewGuid();
var testSourceKeyA = $"uploads/smoke/{Guid.NewGuid():N}-a.txt";
var testSourceKeyB = $"uploads/smoke/{Guid.NewGuid():N}-b.txt";
var expectedPermanentKey = $"documents/{docId:N}/v1/original";

var docIdInvalidSize = Guid.NewGuid();
var invalidPermanentKey = $"documents/{docIdInvalidSize:N}/v1/original";

try
{
    // 1. Генерация presigned PUT URL
    var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);

    var presignedUrl = documentStorage.CreatePresignedUploadUrl(objectKey, expiresAtUtc);


    var generatedUri = new Uri(presignedUrl);

    if (generatedUri.Scheme != Uri.UriSchemeHttp || generatedUri.Host != "localhost" || generatedUri.Port != 9000)
    {
        throw new InvalidOperationException("Предварительно подписанный URL-адрес не совпадает с настроенной конечной точкой MinIO.");
    }

    Console.WriteLine($"PASS: Предварительно подписанная конечная точка URL = {generatedUri.GetLeftPart(UriPartial.Authority)}");

    Console.WriteLine("PASS: Presigned PUT URL generated.");

    // 2. Загрузка через HTTP PUT
    using var httpClient = new HttpClient();
    using var content = new ByteArrayContent(contentBytes);

    content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

    using var uploadResponse = await httpClient.PutAsync(presignedUrl, content);

    uploadResponse.EnsureSuccessStatusCode();

    Console.WriteLine($"PASS: HTTP PUT returned {(int)uploadResponse.StatusCode}.");

    // 3. Проверка размера загруженного объекта
    var actualSize = await documentStorage.GetTemporaryObjectSizeAsync(objectKey, CancellationToken.None);

    if (actualSize != contentBytes.Length)
    {
        throw new InvalidOperationException($"Size mismatch: expected {contentBytes.Length}, actual {actualSize}.");
    }

    Console.WriteLine($"PASS: Object size = {actualSize} bytes.");

    // 4. Проверка несуществующего объекта
    var missingKey = $"uploads/smoke/{Guid.NewGuid():N}-missing.txt";

    try
    {
        var missingSize = await documentStorage.GetTemporaryObjectSizeAsync(missingKey, CancellationToken.None);

        if (missingSize is not null)
        {
            throw new InvalidOperationException("Отсутствующий объект неожиданно вернул метаданные.");
        }

        Console.WriteLine("PASS: При отсутствии объекта возвращено значение null (HTTP 404).");
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
    {
        Console.WriteLine("PASS: При обращении к отсутствующему объекту получен ответ HTTP 403 (restricted IAM policy).");
    }

    // 5. Проверка защиты оригинала от перезаписи

    var originalBytes = Encoding.UTF8.GetBytes("Original version A");
    var modifiedBytes = Encoding.UTF8.GetBytes("Modified version B");

    async Task PutIfAbsentAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = permanentKey,
            InputStream = stream,
            ContentType = "text/plain",
            IfNoneMatch = "*"
        };

        await s3Clients.InternalClient.PutObjectAsync(request);
    }

    // Первая запись должна пройти успешно.
    await PutIfAbsentAsync(originalBytes);

    Console.WriteLine("PASS: Первичная запись оригинала выполнена.");

    // Повторная запись должна быть отклонена.
    var overwriteRejected = false;

    try
    {
        await PutIfAbsentAsync(modifiedBytes);
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
    {
        overwriteRejected = true;
    }

    if (!overwriteRejected)
    {
        throw new InvalidOperationException("Повторная запись оригинала не была заблокирована.");
    }

    Console.WriteLine("PASS: Перезапись оригинала заблокирована (HTTP 412).");

    // Проверяем, что оригинал не изменился.
    using var getResponse = await s3Clients.InternalClient.GetObjectAsync(bucketName, permanentKey);
    using var reader = new StreamReader(getResponse.ResponseStream, Encoding.UTF8);

    var storedContent = await reader.ReadToEndAsync();

    if (storedContent != "Original version A")
    {
        throw new InvalidOperationException("Содержимое оригинала изменилось после попытки перезаписи.");
    }

    Console.WriteLine("PASS: Содержимое оригинала осталось неизменным.");


// 6. Проверка полного сценария работы StoreOriginalAsync



    // Шаг 1: Загрузить TXT во временный ключ A
    var putUrlA = documentStorage.CreatePresignedUploadUrl(testSourceKeyA, DateTime.UtcNow.AddMinutes(5));
    using (var contentA = new ByteArrayContent(originalBytes))
    {
        contentA.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var resA = await httpClient.PutAsync(putUrlA, contentA);
        resA.EnsureSuccessStatusCode();
    }

    // Шаг 2: Выполнить StoreOriginalAsync
    var storeResult = await documentStorage.StoreOriginalAsync(
        temporaryObjectKey: testSourceKeyA,
        documentId: docId,
        contentVersion: 1,
        expectedSize: originalBytes.Length,
        expectedContentType: "text/plain",
        cancellationToken: CancellationToken.None);

    var expectedChecksum = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(originalBytes)
    ).ToLowerInvariant();

    if (storeResult.ObjectKey != expectedPermanentKey ||
        storeResult.Size != originalBytes.Length ||
        storeResult.Checksum != expectedChecksum)
    {
        throw new InvalidOperationException("StoreOriginalAsync returned incorrect metadata.");
    }
    Console.WriteLine("PASS: StoreOriginalAsync успешно закрепил оригинал с корректными метаданными.");

    // Шаг 3: Повторить StoreOriginalAsync с теми же данными (идемпотентность / успешный повтор)
    var storeResultRepeat = await documentStorage.StoreOriginalAsync(
        temporaryObjectKey: testSourceKeyA,
        documentId: docId,
        contentVersion: 1,
        expectedSize: originalBytes.Length,
        expectedContentType: "text/plain",
        cancellationToken: CancellationToken.None);

    if (storeResultRepeat.ObjectKey != expectedPermanentKey ||
        storeResultRepeat.Size != originalBytes.Length ||
        storeResultRepeat.Checksum != expectedChecksum)
    {
        throw new InvalidOperationException("Повторный вызов StoreOriginalAsync вернул некорректные метаданные.");
    }
    Console.WriteLine("PASS: Повторный вызов StoreOriginalAsync с теми же данными завершился успешным идемпотентным возвратом.");

    // Шаг 4: Загрузить другой текст во второй временный ключ и попытаться закрепить с тем же documentId и версией (конфликт)
    var putUrlB = documentStorage.CreatePresignedUploadUrl(testSourceKeyB, DateTime.UtcNow.AddMinutes(5));
    using (var contentB = new ByteArrayContent(modifiedBytes))
    {
        contentB.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var resB = await httpClient.PutAsync(putUrlB, contentB);
        resB.EnsureSuccessStatusCode();
    }

    var conflictDetected = false;
    try
    {
        await documentStorage.StoreOriginalAsync(
            temporaryObjectKey: testSourceKeyB,
            documentId: docId,
            contentVersion: 1,
            expectedSize: modifiedBytes.Length,
            expectedContentType: "text/plain",
            cancellationToken: CancellationToken.None);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Конфликт версий:", StringComparison.Ordinal))
    {
        conflictDetected = true;
    }

    if (!conflictDetected)
    {
        throw new InvalidOperationException("Конфликт версий оригинала не был обнаружен и отклонен.");
    }
    Console.WriteLine("PASS: Конфликт при попытке перезаписать постоянный ключ другим содержимым успешно отклонен.");

    // Шаг 5: Прочитать постоянный объект и проверить сохранность байтов (исходное содержимое)
    using (var getVerifyResponse = await s3Clients.InternalClient.GetObjectAsync(bucketName, expectedPermanentKey))
    using (var verifyMemory = new MemoryStream())
    {
        await getVerifyResponse.ResponseStream.CopyToAsync(verifyMemory);
        var storedBytes = verifyMemory.ToArray();

        if (!storedBytes.AsSpan().SequenceEqual(originalBytes))
        {
            throw new InvalidOperationException("Содержимое постоянного объекта было повреждено или изменено.");
        }
    }
    Console.WriteLine("PASS: Побайтовая проверка подтвердила сохранность исходного содержимого.");

    // Шаг 6: Неверный ожидаемый размер

    var invalidSizeDetected = false;

    try
    {
        await documentStorage.StoreOriginalAsync(
            temporaryObjectKey: testSourceKeyA,
            documentId: docIdInvalidSize,
            contentVersion: 1,
            expectedSize: originalBytes.Length + 999,
            expectedContentType: "text/plain",
            cancellationToken: CancellationToken.None);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Фактический размер файла", StringComparison.Ordinal))
    {
        invalidSizeDetected = true;
    }

    if (!invalidSizeDetected)
    {
        throw new InvalidOperationException("Неверный ожидаемый размер не был отклонён.");
    }

    Console.WriteLine("PASS: Invalid file size rejected.");

    // Доказываем, что StoreOriginalAsync не создал постоянный объект.
    // Условная запись пройдёт только если ключ ещё свободен.

    using (var probeStream = new MemoryStream(Encoding.UTF8.GetBytes("absence probe")))
    {
        var probeRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = invalidPermanentKey,
            InputStream = probeStream,
            ContentType = "text/plain",
            IfNoneMatch = "*"
        };

        await s3Clients.InternalClient.PutObjectAsync(probeRequest);
    }
    Console.WriteLine("PASS: No original was created after size validation failure.");

}
finally
{
    string[] keysToDelete =
    [
        objectKey,
        permanentKey,
        testSourceKeyA,
        testSourceKeyB,
        expectedPermanentKey,
        invalidPermanentKey
    ];

    var cleanupErrors = new List<Exception>();

    foreach (var key in keysToDelete)
    {
        try
        {
            await s3Clients.InternalClient.DeleteObjectAsync(bucketName, key);
        }
        catch (Exception ex)
        {
            cleanupErrors.Add(new InvalidOperationException( $"Не удалось удалить тестовый объект: {key}", ex));
        }
    }

    if (cleanupErrors.Count > 0)
    {
        throw new AggregateException("Очистка MinIO завершилась с ошибками.", cleanupErrors);
    }
    Console.WriteLine("PASS: Cleanup completed.");
}

Console.WriteLine("S3 SMOKE TEST: PASSED");