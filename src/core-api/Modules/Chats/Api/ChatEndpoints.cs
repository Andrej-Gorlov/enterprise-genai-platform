using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using EnterpriseGenAI.Core.Api.Modules.Chats.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Modules.Chats.Api;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/chats", async (CreateChatRequest request, AppDbContext dbContext) =>
        {
            var chat = new Chat(Guid.NewGuid(), request.Title, DateTimeOffset.UtcNow);
            dbContext.Chats.Add(chat);
            await dbContext.SaveChangesAsync();
            return chat;
        });

        endpoints.MapGet("/chats", async (AppDbContext dbContext) =>
        {
            var chats = await dbContext.Chats.AsNoTracking().OrderBy(chat => chat.CreatedAt).ToListAsync();
            return chats;
        });

        return endpoints;
    }
}