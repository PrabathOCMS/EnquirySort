namespace EnquirySort.Api.Features.MailingLists.DeleteMailingList;

public sealed class DeleteMailingListRequest
{
    public Guid? id { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
