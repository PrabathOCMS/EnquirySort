namespace EnquirySort.Api.Models;

public sealed class KnowledgeArticle
{
    public Guid id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime InsertDateUtc { get; set; }
    public DateTime UpdatedDateUtc { get; set; }
    public bool Deleted { get; set; }
    public byte[] ConcurrencyKey { get; set; } = [];
}
