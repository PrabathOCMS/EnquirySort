using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDropdown;

public sealed class ListMailingListsForDropdownEndpoint
    : Endpoint<ListMailingListsForDropdownRequest, DropdownResponse>
{
    private readonly MailingListsRepository _repo;

    public ListMailingListsForDropdownEndpoint(MailingListsRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/mailingLists/listForDropdown");
        SerializerContext(ListMailingListsForDropdownContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListMailingListsForDropdownRequest req, CancellationToken ct)
    {
        DropdownResponse response =
            await _repo.ListMailingListsForDropdownAsync(req.Search, req.RequestCounter, ct);

        await Send.OkAsync(response);
    }
}
