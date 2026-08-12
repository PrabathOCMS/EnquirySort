using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDropdown;

public sealed class ListMailingListsForDropdownRequest
{
    public string? Search { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
