using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDataTable;

public sealed class ListKnowledgeArticlesForDataTableEndpoint
    : Endpoint<ListKnowledgeArticlesForDataTableRequest, DataTableResponse<KnowledgeArticle>>
{
    private readonly KnowledgeArticlesRepository _repo;

    public ListKnowledgeArticlesForDataTableEndpoint(KnowledgeArticlesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/knowledgeArticles/listForDataTable");
        SerializerContext(ListKnowledgeArticlesForDataTableContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListKnowledgeArticlesForDataTableRequest req, CancellationToken ct)
    {
        ValidateInput(req);

        DataTableResponse<KnowledgeArticle> response = await _repo.ListKnowledgeArticlesForDataTableAsync(
            req.PageNumber!.Value, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);

        if (1 + (response.PageNumber - 1) * response.PageSize > response.TotalCount)
        {
            response = await _repo.ListKnowledgeArticlesForDataTableAsync(
                1, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);
        }

        await Send.OkAsync(response);
    }

    private void ValidateInput(ListKnowledgeArticlesForDataTableRequest req)
    {
        req.PageNumber ??= 1;
        req.PageSize ??= 30;
        if (req.PageSize is < 1 or > 200) req.PageSize = 30;
        if (req.Sort is null or SortType.Unsorted) req.Sort = SortType.Name;
    }
}
