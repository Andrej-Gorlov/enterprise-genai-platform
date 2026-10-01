using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseGenAI.Core.Api.Tests.Infrastructure;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Chats.Api;

public sealed class ChatEndpointsTests : IClassFixture<CoreApiFactory>
{
    private readonly HttpClient _client;

    public ChatEndpointsTests(CoreApiFactory factory)
    {
        _client = factory.CreateClient();
        factory.ResetDatabase();
    }

    [Fact]
    public async Task GetChats_WhenDatabaseIsEmpty_ReturnsEmptyArray()
    {
        // Act
        var response = await _client.GetAsync("/chats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task CreateChat_WithValidTitle_ReturnsCreatedChat()
    {
        // Arrange
        var request = new
        {
            title = "Integration test chat"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/chats", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var createdChat = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Integration test chat", createdChat.GetProperty("title").GetString());

        var id = createdChat.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, id);

        // Проверяем, что созданный Chat действительно можно получить через API
        var getResponse = await _client.GetAsync(response.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var loadedChat = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(id, loadedChat.GetProperty("id").GetGuid());
        Assert.Equal("Integration test chat", loadedChat.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CreateChat_WithEmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            title = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/chats", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("Title", out _));
    }

    [Fact]
    public async Task GetChat_WhenChatDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/chats/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Request_WithCorrelationId_ReturnsSameCorrelationId()
    {
        // Arrange
        const string correlationId = "integration-test-123";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/chats");

        request.Headers.Add("X-Correlation-ID", correlationId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(correlationId, Assert.Single(values));
    }
}