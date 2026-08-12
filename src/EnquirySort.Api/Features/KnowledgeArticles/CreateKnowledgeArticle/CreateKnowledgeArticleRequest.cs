namespace EnquirySort.Api.Features.KnowledgeArticles.CreateKnowledgeArticle;

public sealed class CreateKnowledgeArticleRequest
{
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Content { get; set; }
}
