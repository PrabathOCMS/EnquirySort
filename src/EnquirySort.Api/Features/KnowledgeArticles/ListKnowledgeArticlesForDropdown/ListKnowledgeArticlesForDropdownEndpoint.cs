using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDropdown;

public sealed class ListKnowledgeArticlesForDropdownEndpoint
    : Endpoint<ListKnowledgeArticlesForDropdownRequest, DropdownResponse>
{
    private readonly KnowledgeArticlesRepository _repo;

    public ListKnowledgeArticlesForDropdownEndpoint(KnowledgeArticlesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/knowledgeArticles/listForDropdown");
        SerializerContext(ListKnowledgeArticlesForDropdownContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListKnowledgeArticlesForDropdownRequest req, CancellationToken ct)
    {
        DropdownResponse response =
            await _repo.ListKnowledgeArticlesForDropdownAsync(req.Search, req.RequestCounter, ct);

        await Send.OkAsync(response);
    }
}
