using EnquirySort.Api.Enums;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.DeleteKnowledgeArticle;

public sealed class DeleteKnowledgeArticleEndpoint : Endpoint<DeleteKnowledgeArticleRequest>
{
    private readonly KnowledgeArticlesRepository _repo;

    public DeleteKnowledgeArticleEndpoint(KnowledgeArticlesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/knowledgeArticles/delete");
        SerializerContext(DeleteKnowledgeArticleContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeleteKnowledgeArticleRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        SqlQueryResult queryResult =
            await _repo.DeleteKnowledgeArticleAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.NoContentAsync();
    }

    private void ValidateInput(DeleteKnowledgeArticleRequest req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.knowledgeArticle.idIsRequired");

        if (req.ConcurrencyKey is null)
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.knowledgeArticle.concurrencyKeyIsRequired");
        else if (req.ConcurrencyKey.Length > 4)
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.knowledgeArticle.concurrencyKeyLength|{\"length\":\"4\"}");
    }

    private void ValidateOutput(SqlQueryResult queryResult)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The knowledge article was already deleted.", "error.knowledgeArticle.didNotExist");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                AddError("The knowledge article's data has changed since you last accessed this page.",
                    "error.knowledgeArticle.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
