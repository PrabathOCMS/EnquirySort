using FastEndpoints;

namespace EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDropdown;

public sealed class ListKnowledgeArticlesForDropdownRequest
{
    public string? Search { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
