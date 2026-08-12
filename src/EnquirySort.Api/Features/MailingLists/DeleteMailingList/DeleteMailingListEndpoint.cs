using EnquirySort.Api.Enums;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.DeleteMailingList;

public sealed class DeleteMailingListEndpoint : Endpoint<DeleteMailingListRequest>
{
    private readonly MailingListsRepository _repo;

    public DeleteMailingListEndpoint(MailingListsRepository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/mailingLists/delete");
        SerializerContext(DeleteMailingListContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeleteMailingListRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        SqlQueryResult queryResult =
            await _repo.DeleteMailingListAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.NoContentAsync();
    }

    private void ValidateInput(DeleteMailingListRequest req)
    {
        if (!req.id.HasValue)
        {
            AddError(m => m.id!, "Id is required.", "error.mailingList.idIsRequired");
        }

        if (req.ConcurrencyKey is null)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.mailingList.concurrencyKeyIsRequired");
        }
        else if (req.ConcurrencyKey.Length > 4)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.mailingList.concurrencyKeyLength|{\"length\":\"4\"}");
        }
    }

    private void ValidateOutput(SqlQueryResult queryResult)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The mailing list was already deleted.", "error.mailingList.didNotExist");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                AddError("The mailing list's data has changed since you last accessed this page.",
                    "error.mailingList.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
