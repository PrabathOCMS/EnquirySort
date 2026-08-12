namespace EnquirySort.Api.Email;

public sealed class InboundEmail
{
    public string Uid { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public string Subject { get; set; } = "(no subject)";
    public string FromAddress { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
}
