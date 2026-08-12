using EnquirySort.Api.Enums;

namespace EnquirySort.Api.Models;

public sealed class Enquiry
{
    public Guid id { get; set; }
    public string? MessageId { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public EnquiryAction Action { get; set; }
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public string? CustomerQuestion { get; set; }
    public Guid? RoutedToMailingListId { get; set; }
    public string? RoutedToMailingListName { get; set; }
    public string? ReplyBody { get; set; }
    public bool ReplySent { get; set; }
    public DateTime ProcessedUtc { get; set; }
    public DateTime InsertDateUtc { get; set; }
    public DateTime UpdatedDateUtc { get; set; }
    public bool Deleted { get; set; }
    public byte[] ConcurrencyKey { get; set; } = [];
}
