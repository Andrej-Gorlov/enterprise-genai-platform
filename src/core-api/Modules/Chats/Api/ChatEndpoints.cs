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

            var response = new ChatResponse(chat.Id, chat.Title, chat.CreatedAt);

            return Results.CreatedAtRoute("GetChatById", new { id = chat.Id }, response);
        });

        endpoints.MapGet("/chats", async (AppDbContext dbContext) =>
        {
            var chats = await dbContext.Chats.AsNoTracking()
                .OrderBy(chat => chat.CreatedAt)
                .Select(chat => new ChatResponse(
                    chat.Id,
                    chat.Title,
                    chat.CreatedAt))
                .ToListAsync();

            return Results.Ok(chats);
        });

        endpoints.MapGet("/chats/{id:guid}", async (Guid id,AppDbContext dbContext) =>
        {
            var chat = await dbContext.Chats.AsNoTracking().FirstOrDefaultAsync(chat => chat.Id == id);

            if (chat is null)
            {
                return Results.NotFound();
            }

            var response = new ChatResponse(chat.Id, chat.Title, chat.CreatedAt);
            return Results.Ok(response);

        }).WithName("GetChatById");

        return endpoints;
    }
}