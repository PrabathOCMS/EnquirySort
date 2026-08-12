using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.GetMailingList;

public sealed class GetMailingListEndpoint : Endpoint<GetMailingListRequest, MailingList>
{
    private readonly MailingListsRepository _repo;

    public GetMailingListEndpoint(MailingListsRepository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/mailingLists/get/{id}");
        SerializerContext(GetMailingListContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetMailingListRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        MailingList? entity = await _repo.GetMailingListAsync(req.id!.Value, ct);

        ValidateOutput(entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(GetMailingListRequest req)
    {
        if (!req.id.HasValue)
        {
            AddError(m => m.id!, "Id is required.", "error.mailingList.idIsRequired");
        }
    }

    private void ValidateOutput(MailingList? entity)
    {
        if (entity is null)
        {
            HttpContext.Items["FatalError"] = true;
            AddError("The selected mailing list did not exist.", "error.mailingList.didNotExist");
        }
    }
}
