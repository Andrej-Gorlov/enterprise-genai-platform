
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amazon.S3;
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
var contentBytes = Encoding.UTF8.GetBytes("MinIO smoke test");

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
        var missingSize = await documentStorage.GetTemporaryObjectSizeAsync(missingKey,CancellationToken.None);

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

    Console.WriteLine("S3 SMOKE TEST: PASSED");
}
finally
{
    // 5. Удаление тестового объекта
    await s3Clients.InternalClient.DeleteObjectAsync(bucketName,objectKey);

    Console.WriteLine("PASS: Очистка тестовых объектов завершена.");
}
