using EnquirySort.Api.Enums;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDataTable;

public sealed class ListMailingListsForDataTableRequest
{
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public SortType? Sort { get; set; }
    public string? Search { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
