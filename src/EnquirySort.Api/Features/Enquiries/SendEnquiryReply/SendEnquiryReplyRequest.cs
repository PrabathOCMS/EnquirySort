namespace EnquirySort.Api.Features.Enquiries.SendEnquiryReply;

public sealed class SendEnquiryReplyRequest
{
    public Guid? id { get; set; }
    public string? ReplyBody { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
