using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDataTable;

public sealed class ListMailingListsForDataTableEndpoint
    : Endpoint<ListMailingListsForDataTableRequest, DataTableResponse<MailingList>>
{
    private readonly MailingListsRepository _repo;

    public ListMailingListsForDataTableEndpoint(MailingListsRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/mailingLists/listForDataTable");
        SerializerContext(ListMailingListsForDataTableContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListMailingListsForDataTableRequest req, CancellationToken ct)
    {
        ValidateInput(req);

        DataTableResponse<MailingList> response = await _repo.ListMailingListsForDataTableAsync(
            req.PageNumber!.Value, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);

        if (1 + (response.PageNumber - 1) * response.PageSize > response.TotalCount)
        {
            response = await _repo.ListMailingListsForDataTableAsync(
                1, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);
        }

        await Send.OkAsync(response);
    }

    private void ValidateInput(ListMailingListsForDataTableRequest req)
    {
        req.PageNumber ??= 1;
        req.PageSize ??= 30;
        if (req.PageSize is < 1 or > 200)
        {
            req.PageSize = 30;
        }

        if (req.Sort is null or SortType.Unsorted)
        {
            req.Sort = SortType.Name;
        }
    }
}
