using EnquirySort.Api.Enums;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.ListEnquiriesForDataTable;

public sealed class ListEnquiriesForDataTableRequest
{
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public SortType? Sort { get; set; }
    public string? Search { get; set; }

    /// <summary>Open (default), Responded, Ignored, Routed, or All.</summary>
    public EnquiryListFilter? Filter { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
