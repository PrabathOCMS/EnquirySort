using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.ListEnquiriesForDataTable;

public sealed class ListEnquiriesForDataTableEndpoint
    : Endpoint<ListEnquiriesForDataTableRequest, DataTableResponse<Enquiry>>
{
    private readonly EnquiriesRepository _repo;

    public ListEnquiriesForDataTableEndpoint(EnquiriesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/enquiries/listForDataTable");
        SerializerContext(ListEnquiriesForDataTableContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListEnquiriesForDataTableRequest req, CancellationToken ct)
    {
        ValidateInput(req);

        DataTableResponse<Enquiry> response = await _repo.ListEnquiriesForDataTableAsync(
            req.PageNumber!.Value,
            req.PageSize!.Value,
            req.Sort!.Value,
            req.RequestCounter,
            req.Search,
            ct);

        if (1 + (response.PageNumber - 1) * response.PageSize > response.TotalCount && response.TotalCount > 0)
        {
            response = await _repo.ListEnquiriesForDataTableAsync(
                1,
                req.PageSize!.Value,
                req.Sort!.Value,
                req.RequestCounter,
                req.Search,
                ct);
        }

        await Send.OkAsync(response);
    }

    private void ValidateInput(ListEnquiriesForDataTableRequest req)
    {
        req.PageNumber ??= 1;
        req.PageSize ??= 30;
        if (req.PageSize is < 1 or > 200)
        {
            req.PageSize = 30;
        }

        if (req.Sort is null or SortType.Unsorted)
        {
            req.Sort = SortType.Updated;
        }
    }
}
