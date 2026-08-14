namespace EnquirySort.Api.Features.Enquiries.UpdateEnquiryDraft;

public sealed class UpdateEnquiryDraftRequest
{
    public Guid? id { get; set; }
    public string? ReplyBody { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
