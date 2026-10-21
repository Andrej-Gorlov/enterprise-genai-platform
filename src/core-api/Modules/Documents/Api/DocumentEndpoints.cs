using EnterpriseGenAI.Core.Api.Infrastructure.Identity;
using EnterpriseGenAI.Core.Api.Modules.Documents.Application;
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Api;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/documents").RequireAuthorization();

        group.MapPost("/", RegisterDocumentAsync);
        group.MapPost("/{documentId:guid}/confirm", ConfirmUploadAsync);

        return endpoints;
    }

    private static async Task<IResult> RegisterDocumentAsync(
        RegisterDocumentRequest request,
        IDocumentService documentService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        if (string.IsNullOrEmpty(ownerId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Authenticated user identity is required.");
        }

        try
        {
            var response = await documentService.RegisterUploadAsync(request, ownerId, cancellationToken);
            return Results.Created($"/documents/{response.DocumentId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> ConfirmUploadAsync(
        Guid documentId,
        IDocumentService documentService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        if (string.IsNullOrEmpty(ownerId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Authenticated user identity is required.");
        }

        try
        {
            var response = await documentService.ConfirmUploadAsync(documentId, ownerId, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: ex.Message);
        }
        catch (KeyNotFoundException)
        {
            // Документ чужого владельца и отсутствующий выглядят одинаково — без утечки метаданных.
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found",
                detail: "Document was not found.");
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: ex.Message);
        }
    }
}