using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Identity;
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using EnterpriseGenAI.Core.Api.Modules.Documents.Application;
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Documents;

public sealed class DocumentEndpointsTests
{
    private const string TrustedUserId = "dev-user-00000000-0000-0000-0000-000000000001";

    /// <summary>
    /// Development + DevelopmentIdentity, без ослабления Production.
    /// </summary>
    private sealed class DocumentsApiFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=unused",
                ["AiOrchestrator:BaseUrl"] = "http://ai-orchestrator.test",
                ["AiOrchestrator:TimeoutSeconds"] = "30",
                ["DocumentStorage:InternalEndpoint"] = "http://localhost:9000/",
                ["DocumentStorage:PublicEndpoint"] = "http://localhost:9000/",
                ["DocumentStorage:BucketName"] = "test",
                ["DocumentStorage:AccessKey"] = "test",
                ["DocumentStorage:SecretKey"] = "test",
                ["DocumentStorage:PresignedUrlExpiryMinutes"] = "10",
                ["DevelopmentIdentity:Enabled"] = "true",
                ["DevelopmentIdentity:UserId"] = TrustedUserId
            }));
            return base.CreateHost(builder);
        }

        private readonly string _databaseName = $"docs-api-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _databaseRoot = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

                services.AddDbContext<AppDbContext>(o =>
                    o.UseInMemoryDatabase(_databaseName, _databaseRoot));
            });
        }
    }

    private sealed class RecordingDocumentService : IDocumentService
    {
        public int RegisterCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public string? LastOwnerId { get; private set; }
        public Exception? ConfirmException { get; set; }

        public Task<RegisterDocumentResponse> RegisterUploadAsync(RegisterDocumentRequest request, string trustedOwnerId, CancellationToken cancellationToken)
        {
            RegisterCalls++;
            LastOwnerId = trustedOwnerId;
            return Task.FromResult(
                new RegisterDocumentResponse(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DocumentStatus.PendingUpload,
                1,
                DateTime.UtcNow.AddMinutes(10),
                "https://fake-storage.local/uploads/x?sig=1"));
        }

        public Task<DocumentResponse> ConfirmUploadAsync(Guid documentId, string trustedOwnerId, CancellationToken cancellationToken)
        {
            ConfirmCalls++;
            LastOwnerId = trustedOwnerId;
            if (ConfirmException is not null)
                throw ConfirmException;

            return Task.FromResult(new DocumentResponse(
                documentId, "doc.pdf", "application/pdf", 1024, null,
                DocumentStatus.Uploaded, 1, 0, null, DateTime.UtcNow));
        }
    }

    private sealed class NullCurrentUser : ICurrentUser
    {
        public string? UserId => null;
        public bool IsAuthenticated => false;
    }

    private static (HttpClient Client, RecordingDocumentService Service) CreateClient(Action<IServiceCollection>? extra = null)
    {
        var service = new RecordingDocumentService();
        var factory = new DocumentsApiFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentService>();
                services.AddSingleton<IDocumentService>(service);
                extra?.Invoke(services);
            });
        });
        return (factory.CreateClient(), service);
    }

    [Fact]
    public async Task Register_Authenticated_Returns201_AndPassesTrustedUserId()
    {
        var (client, service) = CreateClient();

        var response = await client.PostAsJsonAsync("/documents", new
        {
            fileName = "spec.pdf",
            contentType = "application/pdf",
            size = 1024
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, service.RegisterCalls);
        Assert.Equal(TrustedUserId, service.LastOwnerId);

        var body = await response.Content.ReadFromJsonAsync<RegisterDocumentResponse>();

        Assert.NotNull(body);
        Assert.Equal(DocumentStatus.PendingUpload, body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.UploadUrl));
    }

    [Fact]
    public async Task Register_MissingTrustedUserId_Returns401_AndDoesNotCallService()
    {
        var (client, service) = CreateClient(services =>
        {
            services.RemoveAll<ICurrentUser>();
            services.AddSingleton<ICurrentUser, NullCurrentUser>();
        });

        var response = await client.PostAsJsonAsync("/documents", new
        {
            fileName = "spec.pdf",
            contentType = "application/pdf",
            size = 1024
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, service.RegisterCalls);
    }

    [Fact]
    public async Task Confirm_ForeignDocument_Returns404_WithoutOwnerLeak()
    {
        var (client, service) = CreateClient();
        service.ConfirmException = new KeyNotFoundException("Документ с идентификатором '...' не найден.");

        var id = Guid.NewGuid();
        var response = await client.PostAsync($"/documents/{id}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        var detail = problem.TryGetProperty("detail", out var d) ? d.GetString() ?? "" : "";
        Assert.DoesNotContain("Owner", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("владел", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, service.ConfirmCalls);
        Assert.Equal(TrustedUserId, service.LastOwnerId);
    }

    [Fact]
    public async Task Register_InvalidPayload_Returns400_ProblemDetails()
    {
        // Реальный DocumentService: невалидное имя -> ArgumentException -> 400
        using var factory = new DocumentsApiFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentStorage>();
                services.AddSingleton<IDocumentStorage, NoOpDocumentStorage>();
            });
        });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/documents", new
        {
            fileName = "evil.exe",
            contentType = "application/octet-stream",
            size = 1024
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    private sealed class NoOpDocumentStorage : IDocumentStorage
    {
        public string CreatePresignedUploadUrl(string temporaryObjectKey, DateTime expiresAtUtc) => $"https://fake/{temporaryObjectKey}";

        public Task<long?> GetTemporaryObjectSizeAsync(string key, CancellationToken ct) => Task.FromResult<long?>(1024);

        public Task<StoredOriginalResult> StoreOriginalAsync(
            string temporaryObjectKey, Guid documentId, int contentVersion,
            long expectedSize, string expectedContentType, CancellationToken cancellationToken) =>
            Task.FromResult(new StoredOriginalResult($"documents/{documentId:N}/v1/original", expectedSize, new string('a', 64)));
    }

    private sealed class RejectingAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public RejectingAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder): base(options, logger, encoder){}

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }

    [Fact]
    public async Task Register_WhenAuthenticationFails_Returns401_AndDoesNotCallService()
    {
        var service = new RecordingDocumentService();

        using var factory = new DocumentsApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentService>();
                services.AddSingleton<IDocumentService>(service);

                // Схема по умолчанию handler, который не аутентифицирует.
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Rejecting";
                    options.DefaultChallengeScheme = "Rejecting";
                }).AddScheme<AuthenticationSchemeOptions, RejectingAuthenticationHandler>("Rejecting", _ => { });
            });
        });

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/documents", new
        {
            fileName = "spec.pdf",
            contentType = "application/pdf",
            size = 1024
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, service.RegisterCalls);
    }

    [Fact]
    public async Task Confirm_OtherOwnersDocument_Returns404_AndLeavesStatusUnchanged()
    {
        const string ownerB = "user-b-owner";

        using var factory = new DocumentsApiFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentStorage>();
                services.AddSingleton<IDocumentStorage, NoOpDocumentStorage>();
            });
        });

        var client = factory.CreateClient(); // один host / одна БД

        Guid documentId;
        using (var scope = factory.Services.CreateScope())
        {
            var docs = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var reg = await docs.RegisterUploadAsync(new RegisterDocumentRequest("secret.pdf", "application/pdf", 1024), ownerB, CancellationToken.None);
            documentId = reg.DocumentId;
        }

        var response = await client.PostAsync($"/documents/{documentId}/confirm", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.DocumentId == documentId);

            Assert.Equal(ownerB, doc.OwnerId);
            Assert.Equal(DocumentStatus.PendingUpload, doc.Status);
            Assert.Null(doc.ObjectKey);
            Assert.Null(doc.Checksum);
        }
    }
}