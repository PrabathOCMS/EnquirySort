namespace EnquirySort.Api.Features.KnowledgeArticles.UpdateKnowledgeArticle;

public sealed class UpdateKnowledgeArticleRequest
{
    public Guid? id { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Content { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
