namespace EnquirySort.Api.Features.MailingLists.CreateMailingList;

public sealed class CreateMailingListRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
}
