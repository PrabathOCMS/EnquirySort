namespace EnquirySort.Api.Models;

public sealed class MailingList
{
    public Guid id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime InsertDateUtc { get; set; }
    public DateTime UpdatedDateUtc { get; set; }
    public bool Deleted { get; set; }
    public byte[] ConcurrencyKey { get; set; } = [];
}
