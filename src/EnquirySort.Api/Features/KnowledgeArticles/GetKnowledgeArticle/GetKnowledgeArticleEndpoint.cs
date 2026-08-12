using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.GetKnowledgeArticle;

public sealed class GetKnowledgeArticleEndpoint : Endpoint<GetKnowledgeArticleRequest, KnowledgeArticle>
{
    private readonly KnowledgeArticlesRepository _repo;

    public GetKnowledgeArticleEndpoint(KnowledgeArticlesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/knowledgeArticles/get/{id}");
        SerializerContext(GetKnowledgeArticleContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetKnowledgeArticleRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        KnowledgeArticle? entity = await _repo.GetKnowledgeArticleAsync(req.id!.Value, ct);

        ValidateOutput(entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(GetKnowledgeArticleRequest req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.knowledgeArticle.idIsRequired");
    }

    private void ValidateOutput(KnowledgeArticle? entity)
    {
        if (entity is null)
        {
            HttpContext.Items["FatalError"] = true;
            AddError("The selected knowledge article did not exist.", "error.knowledgeArticle.didNotExist");
        }
    }
}
