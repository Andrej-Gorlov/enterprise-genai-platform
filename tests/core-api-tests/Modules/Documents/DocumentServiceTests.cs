
using System.Text.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using EnterpriseGenAI.Core.Api.Modules.Documents.Application;
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;
using EnterpriseGenAI.Core.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Documents;

public sealed class DocumentServiceTests
{
    private sealed class FailOnceSaveInterceptor : SaveChangesInterceptor
    {
        public bool FailNextSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("Имитация сбоя сохранения в PostgreSQL.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static IOptions<EmbeddingModelOptions> CreateEmbeddingModelOptions()
    {
        return Microsoft.Extensions.Options.Options.Create(
            new EmbeddingModelOptions
            {
                ModelId = "sentence-transformers/all-MiniLM-L6-v2",
                ModelRevision = "main"
            });
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        private int _storeOriginalCallCount;

        public bool ShouldFail { get; set; }
        public bool ShouldFailStore { get; set; }

        public string LastKey { get; private set; } = string.Empty;
        public DateTime? LastExpiry { get; private set; }

        public int StoreOriginalCallCount => Volatile.Read(ref _storeOriginalCallCount);

        public TaskCompletionSource? BothCallsStarted { get; set; }
        public TaskCompletionSource? ReleaseStoreCalls { get; set; }

        public string CreatePresignedUploadUrl(string temporaryObjectKey, DateTime expiresAtUtc)
        {
            if (ShouldFail)
                throw new InvalidOperationException("Имитация сбоя создания presigned URL.");

            LastKey = temporaryObjectKey;
            LastExpiry = expiresAtUtc;

            return $"https://fake-storage.local/{temporaryObjectKey}?signature=test";
        }

        public Task<long?> GetTemporaryObjectSizeAsync(string temporaryObjectKey, CancellationToken cancellationToken)
        {
            return Task.FromResult<long?>(1024L);
        }

        public async Task<StoredOriginalResult> StoreOriginalAsync(
            string temporaryObjectKey,
            Guid documentId,
            int contentVersion,
            long expectedSize,
            string expectedContentType,
            CancellationToken cancellationToken)
        {
            var callCount = Interlocked.Increment(ref _storeOriginalCallCount);

            // Синхронизация двух конкурентных запросов.
            if (BothCallsStarted is not null && ReleaseStoreCalls is not null)
            {
                if (callCount >= 2)
                    BothCallsStarted.TrySetResult();

                await ReleaseStoreCalls.Task.WaitAsync(cancellationToken);
            }

            if (ShouldFail || ShouldFailStore)
                throw new InvalidOperationException("Имитация сбоя сохранения оригинала.");

            return new StoredOriginalResult($"documents/{documentId:N}/v{contentVersion}/original", expectedSize, new string('a', 64));
        }
    }
    private static (DocumentService Service, AppDbContext DbContext, FakeDocumentStorage Storage) CreateService()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var dbContext = new AppDbContext(dbOptions);
        var storage = new FakeDocumentStorage();

        var storageOptions = Microsoft.Extensions.Options.Options.Create(new DocumentStorageOptions
        {
            InternalEndpoint = "http://localhost:9000",
            PublicEndpoint = "http://localhost:9000",
            BucketName = "test-bucket",
            AccessKey = "test-key",
            SecretKey = "test-secret",
            PresignedUrlExpiryMinutes = 10
        });

        var embeddingModel = new EmbeddingModelOptions
        {
            ModelId = "sentence-transformers/all-MiniLM-L6-v2",
            ModelRevision = "main"
        };

        var service = new DocumentService(dbContext, storage, Microsoft.Extensions.Options.Options.Create(embeddingModel), storageOptions);
        
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

    [Fact]
    public async Task ConfirmUpload_ValidDocument_ChangesStatusToUploaded()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        const string ownerId = "user-123";
        var regResponse = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        var confirmResponse = await service.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);

        Assert.Equal(DocumentStatus.Uploaded, confirmResponse.Status);
        Assert.Equal(1, storage.StoreOriginalCallCount);

        var dbDoc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        Assert.Equal(DocumentStatus.Uploaded, dbDoc.Status);
        Assert.NotNull(dbDoc.ObjectKey);
        Assert.NotNull(dbDoc.Checksum);
        Assert.Equal(64, dbDoc.Checksum.Length);
    }

    [Fact]
    public async Task ConfirmUpload_AlreadyUploaded_ReturnsExistingDocument()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        const string ownerId = "user-123";
        var regResponse = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        var firstConfirm = await service.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);
        var secondConfirm = await service.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);

        Assert.Equal(DocumentStatus.Uploaded, secondConfirm.Status);
        Assert.Equal(firstConfirm.Checksum, secondConfirm.Checksum);
        Assert.Equal(1, storage.StoreOriginalCallCount); // Не увеличился при повторном вызове
    }

    [Fact]
    public async Task ConfirmUpload_WrongOwner_ThrowsKeyNotFoundException()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        const string ownerId = "user-123";
        var regResponse = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmUploadAsync(regResponse.DocumentId, "intruder-999", CancellationToken.None));

        var dbDoc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        Assert.Equal(DocumentStatus.PendingUpload, dbDoc.Status); // Состояние не изменилось
        Assert.Equal(0, storage.StoreOriginalCallCount);
    }

    [Fact]
    public async Task ConfirmUpload_InvalidStatus_ThrowsInvalidOperationException()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        const string ownerId = "user-123";
        var regResponse = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        // Переводим документ вручную в статус Failed для теста
        var doc = await dbContext.Documents.SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        doc.MarkUploadFailed();
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None));

        var dbDoc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        Assert.Equal(DocumentStatus.Failed, dbDoc.Status); // Состояние не изменилось
        Assert.Equal(0, storage.StoreOriginalCallCount);
    }

    [Fact]
    public async Task ConfirmUpload_StorageFails_KeepsPendingUpload()
    {
        var (service, dbContext, storage) = CreateService();
        await using var context = dbContext;

        const string ownerId = "user-123";
        var regResponse = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        storage.ShouldFailStore = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None));

        var dbDoc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        Assert.Equal(DocumentStatus.PendingUpload, dbDoc.Status); // Состояние осталось PendingUpload
        Assert.Null(dbDoc.ObjectKey);
        Assert.Null(dbDoc.Checksum);
        Assert.Equal(1, storage.StoreOriginalCallCount);
    }

    [Fact]
    public async Task ConfirmUpload_DatabaseSaveFails_RetrySucceeds()
    {
        var interceptor = new FailOnceSaveInterceptor();
        var dbName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).AddInterceptors(interceptor).Options;

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

        await using var dbContext1 = new AppDbContext(dbOptions);
        var service1 = new DocumentService(dbContext1, storage, CreateEmbeddingModelOptions(), options);

        const string ownerId = "user-123";
        var regResponse = await service1.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512),  ownerId,  CancellationToken.None);

        interceptor.FailNextSave = true;

        await using var dbContext2 = new AppDbContext(dbOptions);
        var service2 = new DocumentService(dbContext2, storage, CreateEmbeddingModelOptions(), options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service2.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None));

        await using var dbContext3 = new AppDbContext(dbOptions);
        var docInDb = await dbContext3.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        Assert.Equal(DocumentStatus.PendingUpload, docInDb.Status);

        await using var dbContext4 = new AppDbContext(dbOptions);
        
        var service4 = new DocumentService(dbContext4, storage, CreateEmbeddingModelOptions(), options);

        var confirmResult = await service4.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);

        Assert.Equal(DocumentStatus.Uploaded, confirmResult.Status);

        await using var dbContext5 = new AppDbContext(dbOptions);
        var finalDoc = await dbContext5.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);
        
        Assert.Equal(DocumentStatus.Uploaded, finalDoc.Status);
        Assert.NotNull(finalDoc.ObjectKey);
        Assert.NotNull(finalDoc.Checksum);
        Assert.Equal(64, finalDoc.Checksum.Length);
        Assert.Equal(2, storage.StoreOriginalCallCount);
    }

    [Fact]
    public async Task ConfirmUpload_ConcurrentRequests_DetectsConcurrencyConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

        var storage = new FakeDocumentStorage
        {
            BothCallsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            ReleaseStoreCalls = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        var options = Microsoft.Extensions.Options.Options.Create(new DocumentStorageOptions
        {
            InternalEndpoint = "http://localhost:9000",
            PublicEndpoint = "http://localhost:9000",
            BucketName = "test-bucket",
            AccessKey = "test-key",
            SecretKey = "test-secret",
            PresignedUrlExpiryMinutes = 10
        });

        await using var setupContext = new AppDbContext(dbOptions);
        var setupService = new DocumentService(setupContext, storage, CreateEmbeddingModelOptions(), options);

        const string ownerId = "user-123";
        var regResponse = await setupService.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);

        await using var dbContext1 = new AppDbContext(dbOptions);
        await using var dbContext2 = new AppDbContext(dbOptions);

        var service1 = new DocumentService(dbContext1, storage, CreateEmbeddingModelOptions(), options);
        var service2 = new DocumentService(dbContext2, storage, CreateEmbeddingModelOptions(), options);

        try
        {
            var task1 = service1.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);
            var task2 = service2.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);

            var completedTask = await Task.WhenAny(storage.BothCallsStarted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            if (completedTask != storage.BothCallsStarted.Task)
            {
                throw new TimeoutException("Таймаут ожидания старта обоих вызовов StoreOriginalAsync.");
            }

            storage.ReleaseStoreCalls.TrySetResult();

            Task<DocumentResponse>[] tasks = [task1, task2];
            var results = await Task.WhenAll(tasks.Select(async t =>
            {
                try
                {
                    return (Success: true, Result: await t, Exception: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Result: (DocumentResponse?)null, Exception: ex);
                }
            }));

            var successes = results.Where(r => r.Success).ToList();
            var failures = results.Where(r => !r.Success).ToList();

            Assert.Single(successes);
            Assert.Single(failures);

            var concurrencyEx = Assert.IsType<DbUpdateConcurrencyException>(failures[0].Exception);
            Assert.NotNull(concurrencyEx);

            await using var verifyContext = new AppDbContext(dbOptions);
            var verifiedDoc = await verifyContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == regResponse.DocumentId);

            Assert.Equal(DocumentStatus.Uploaded, verifiedDoc.Status);
            Assert.NotNull(verifiedDoc.ObjectKey);
            Assert.NotNull(verifiedDoc.Checksum);

            int initialCallCount = storage.StoreOriginalCallCount;

            Assert.Equal(2, initialCallCount);

            await using var retryContext = new AppDbContext(dbOptions);

            var retryService = new DocumentService(retryContext, storage, CreateEmbeddingModelOptions(), options);

            var retryResponse = await retryService.ConfirmUploadAsync(regResponse.DocumentId, ownerId, CancellationToken.None);

            Assert.Equal(DocumentStatus.Uploaded, retryResponse.Status);
            Assert.Equal(initialCallCount, storage.StoreOriginalCallCount);
        }
        finally
        {
            // Важно: гарантированно освобождаем ReleaseStoreCalls, чтобы не оставлять задачи заблокированными при падении теста
            storage.ReleaseStoreCalls.TrySetResult();
        }
    }

    [Fact]
    public async Task QueueForIngestion_ValidUploadedDocument_CreatesJobAndOutbox()
    {
        var (service, dbContext, _) = CreateService();
        await using var _ = dbContext;

        const string ownerId = "user-123";
        var reg = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);
        await service.ConfirmUploadAsync(reg.DocumentId, ownerId, CancellationToken.None);

        await service.QueueForIngestionAsync(reg.DocumentId, ownerId, "corr-1", CancellationToken.None);

        var doc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == reg.DocumentId);
        Assert.Equal(DocumentStatus.Queued, doc.Status);
        Assert.Equal(1, doc.ProcessingVersion);

        var job = await dbContext.IngestionJobs.AsNoTracking().SingleAsync();
        Assert.Equal(reg.DocumentId, job.DocumentId);
        Assert.Equal(1, job.ContentVersion);
        Assert.Equal(1, job.ProcessingVersion);
        Assert.Equal(IngestionJobStatus.Queued, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.Equal("corr-1", job.CorrelationId);

        var outbox = await dbContext.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("DocumentIngestionRequested", outbox.EventType);
        Assert.Equal(1, outbox.SchemaVersion);
        Assert.Null(outbox.PublishedAt);

        using var payload = JsonDocument.Parse(outbox.Payload);
        var root = payload.RootElement;
        Assert.Equal(job.JobId, root.GetProperty("JobId").GetGuid());
        Assert.Equal(reg.DocumentId, root.GetProperty("DocumentId").GetGuid());
        Assert.Equal(1, root.GetProperty("ContentVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("ProcessingVersion").GetInt32());
    }

    [Fact]
    public async Task QueueForIngestion_RepeatedCall_DoesNotCreateDuplicates()
    {
        var (service, dbContext, _) = CreateService();
        await using var _ = dbContext;

        const string ownerId = "user-123";
        var reg = await service.RegisterUploadAsync(new RegisterDocumentRequest("doc.pdf", "application/pdf", 512), ownerId, CancellationToken.None);
        await service.ConfirmUploadAsync(reg.DocumentId, ownerId, CancellationToken.None);

        await service.QueueForIngestionAsync(reg.DocumentId, ownerId, null, CancellationToken.None);
        await service.QueueForIngestionAsync(reg.DocumentId, ownerId, null, CancellationToken.None);

        var doc = await dbContext.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == reg.DocumentId);
        Assert.Equal(DocumentStatus.Queued, doc.Status);
        Assert.Equal(1, doc.ProcessingVersion);

        Assert.Equal(1, await dbContext.IngestionJobs.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }
}

