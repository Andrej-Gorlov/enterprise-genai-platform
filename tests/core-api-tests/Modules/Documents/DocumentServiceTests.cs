
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using EnterpriseGenAI.Core.Api.Modules.Documents.Application;
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Documents;

public sealed class DocumentServiceTests
{
    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public bool ShouldFail { get; set; }

        public string LastKey { get; private set; } = string.Empty;

        public DateTime? LastExpiry { get; private set; }

        public string CreatePresignedUploadUrl(string temporaryObjectKey, DateTime expiresAtUtc)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("Имитация сбоя системы хранения данных.");
            }

            LastKey = temporaryObjectKey;
            LastExpiry = expiresAtUtc;

            return $"https://fake-storage.local/{temporaryObjectKey}?signature=test";
        }

        public Task<long?> GetTemporaryObjectSizeAsync(string temporaryObjectKey, CancellationToken cancellationToken)
        {
            return Task.FromResult<long?>(1024L);
        }
    }

    private static (DocumentService Service, AppDbContext DbContext, FakeDocumentStorage Storage) CreateService()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;

        var dbContext = new AppDbContext(dbOptions);
        var storage = new FakeDocumentStorage();

        var options = Microsoft.Extensions.Options.Options.Create(new DocumentStorageOptions
        {
            InternalEndpoint = "http://localhost:9000",
            PublicEndpoint = "http://localhost:9000",
            BucketName = "test-bucket",
            AccessKey = "test-key",
            SecretKey = "test-secret",
            PresignedUrlExpiryMinutes = 10
        });

        var service = new DocumentService(dbContext, storage, options);

        return (service, dbContext, storage);
    }

    [Theory]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("README.md", "text/markdown")]
    public async Task RegisterUpload_ValidDocument_CreatesPendingDocument(string fileName, string contentType)
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest(fileName, contentType, 1024);

        const string ownerId = "user-123";

        var response = await service.RegisterUploadAsync(request, ownerId, CancellationToken.None);

        Assert.Equal(DocumentStatus.PendingUpload, response.Status);
        Assert.Equal(1, response.ContentVersion);
        Assert.StartsWith("uploads/", storage.LastKey);
        Assert.Equal(response.UploadExpiresAtUtc, storage.LastExpiry);
        Assert.Contains(storage.LastKey, response.UploadUrl);

        var savedDoc = await dbContext.Documents.SingleAsync(d => d.DocumentId == response.DocumentId);

        Assert.Equal(DocumentStatus.PendingUpload, savedDoc.Status);
        Assert.Equal(ownerId, savedDoc.OwnerId);
        Assert.Equal(fileName, savedDoc.FileName);
        Assert.Equal(contentType, savedDoc.ContentType);
        Assert.Equal(1024, savedDoc.Size);

        Assert.Equal(1, savedDoc.ContentVersion);
        Assert.Equal(0, savedDoc.ProcessingVersion);
        Assert.Null(savedDoc.ReadyProcessingVersion);

        Assert.Null(savedDoc.ObjectKey);
        Assert.Null(savedDoc.Checksum);

        Assert.Equal(storage.LastKey, savedDoc.TemporaryObjectKey);
        Assert.Equal(response.UploadExpiresAtUtc, savedDoc.UploadExpiresAtUtc);

        Assert.Equal(1, await dbContext.Documents.CountAsync());
    }

    [Theory]
    [InlineData("file.exe", "application/octet-stream")]
    [InlineData("doc.pdf", "text/plain")]
    [InlineData("", "application/pdf")]
    [InlineData("   ", "text/plain")]
    [InlineData("path/to/file.pdf", "application/pdf")]
    [InlineData("file\\name.txt", "text/plain")]
    public async Task RegisterUpload_InvalidFile_ThrowsException(string fileName, string contentType)
    {
        var (service, dbContext, _) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest(fileName, contentType, 1024);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterUploadAsync(request, "user-123", CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(21 * 1024 * 1024)]
    public async Task RegisterUpload_InvalidSize_ThrowsException(long size)
    {
        var (service, dbContext, _) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest("test.pdf", "application/pdf", size);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RegisterUploadAsync(request, "user-123", CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterUpload_EmptyOwner_ThrowsArgumentException(string ownerId)
    {
        var (service, dbContext, _) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest("test.pdf", "application/pdf", 1024);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterUploadAsync(request, ownerId, CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Fact]
    public async Task RegisterUpload_NullOwner_ThrowsArgumentNullException()
    {
        var (service, dbContext, _) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest("test.pdf", "application/pdf", 1024);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RegisterUploadAsync( request, null!, CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Fact]
    public async Task RegisterUpload_TooLongOwner_ThrowsArgumentException()
    {
        var (service, dbContext, _) = CreateService();
        await using var context = dbContext;

        var request = new RegisterDocumentRequest("test.pdf", "application/pdf", 1024);
        var ownerId = new string('x', 257);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterUploadAsync( request, ownerId, CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }

    [Fact]
    public async Task RegisterUpload_PresigningFails_DoesNotPersistDocument()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        storage.ShouldFail = true;

        var request = new RegisterDocumentRequest("test.pdf", "application/pdf", 1024);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterUploadAsync(request, "user-123", CancellationToken.None));

        Assert.Equal(0, await dbContext.Documents.CountAsync());
    }
}
