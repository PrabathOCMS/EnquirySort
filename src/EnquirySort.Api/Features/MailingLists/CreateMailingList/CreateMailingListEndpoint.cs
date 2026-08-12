using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using EnquirySort.Api.Utilities;
using FastEndpoints;

namespace EnquirySort.Api.Features.MailingLists.CreateMailingList;

public sealed class CreateMailingListEndpoint : Endpoint<CreateMailingListRequest, MailingList>
{
    private readonly MailingListsRepository _repo;

    public CreateMailingListEndpoint(MailingListsRepository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/mailingLists/create");
        SerializerContext(CreateMailingListContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateMailingListRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        (SqlQueryResult queryResult, MailingList? entity) =
            await _repo.CreateMailingListAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(CreateMailingListRequest req)
    {
        req.Name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            AddError(m => m.Name!, "Name is required.", "error.mailingList.nameIsRequired");
        }
        else if (req.Name.Length > 100)
        {
            AddError(m => m.Name!, "Name must be 100 characters or less.",
                "error.mailingList.nameLength|{\"length\":\"100\"}");
        }

        req.Address = req.Address?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(req.Address))
        {
            AddError(m => m.Address!, "Address is required.", "error.mailingList.addressIsRequired");
        }
        else if (req.Address.Length > 320)
        {
            AddError(m => m.Address!, "Address must be 320 characters or less.",
                "error.mailingList.addressLength|{\"length\":\"320\"}");
        }
        else if (!Toolbox.IsValidEmail(req.Address))
        {
            AddError(m => m.Address!, "Address must be a valid email address.", "error.mailingList.addressFormat");
        }

        req.Description = req.Description?.Trim();
        if (string.IsNullOrWhiteSpace(req.Description))
        {
            req.Description = null;
        }
        else if (req.Description.Length > 500)
        {
            AddError(m => m.Description!, "Description must be 500 characters or less.",
                "error.mailingList.descriptionLength|{\"length\":\"500\"}");
        }
    }

    private void ValidateOutput(SqlQueryResult queryResult, MailingList? entity)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                if (entity is null)
                {
                    AddError("An unknown error occurred.", "error.unknown");
                }

                return;
            case SqlQueryResult.RecordAlreadyExists:
                AddError(m => m.Name!, "Another mailing list already exists with the specified name.",
                    "error.mailingList.nameExists");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
