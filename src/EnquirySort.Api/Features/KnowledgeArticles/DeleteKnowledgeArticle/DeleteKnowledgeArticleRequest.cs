namespace EnquirySort.Api.Features.KnowledgeArticles.DeleteKnowledgeArticle;

public sealed class DeleteKnowledgeArticleRequest
{
    public Guid? id { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
