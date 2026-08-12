namespace EnquirySort.Api.Features.MailingLists.UpdateMailingList;

public sealed class UpdateMailingListRequest
{
    public Guid? id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
