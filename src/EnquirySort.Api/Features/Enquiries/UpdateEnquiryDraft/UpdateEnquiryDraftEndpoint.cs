using System.Text.Json;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.UpdateEnquiryDraft;

public sealed class UpdateEnquiryDraftEndpoint : Endpoint<UpdateEnquiryDraftRequest, Enquiry>
{
    private readonly EnquiriesRepository _repo;

    public UpdateEnquiryDraftEndpoint(EnquiriesRepository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/enquiries/updateDraft");
        SerializerContext(UpdateEnquiryDraftContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateEnquiryDraftRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (SqlQueryResult queryResult, Enquiry? entity) =
            await _repo.UpdateEnquiryDraftAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(UpdateEnquiryDraftRequest req)
    {
        if (!req.id.HasValue)
        {
            AddError(m => m.id!, "Id is required.", "error.enquiry.idIsRequired");
        }

        req.ReplyBody = req.ReplyBody?.Trim();
        if (string.IsNullOrWhiteSpace(req.ReplyBody))
        {
            AddError(m => m.ReplyBody!, "Reply body is required.", "error.enquiry.replyBodyIsRequired");
        }
        else if (req.ReplyBody.Length > 100000)
        {
            AddError(m => m.ReplyBody!, "Reply body must be 100000 characters or less.",
                "error.enquiry.replyBodyLength|{\"length\":\"100000\"}");
        }

        if (req.ConcurrencyKey is null)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.enquiry.concurrencyKeyIsRequired");
        }
        else if (req.ConcurrencyKey.Length > 4)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.enquiry.concurrencyKeyLength|{\"length\":\"4\"}");
        }
    }

    private void ValidateOutput(SqlQueryResult queryResult, Enquiry? entity)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                if (entity is null)
                {
                    AddError("An unknown error occurred.", "error.unknown");
                }

                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The enquiry was deleted since you last accessed this page.",
                    "error.enquiry.deletedSinceAccessedPage");
                break;
            case SqlQueryResult.RecordAlreadyExists:
                AddError("This enquiry does not have an editable draft reply.",
                    "error.enquiry.draftNotEditable");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                HttpContext.Items["ConcurrencyKeyInvalid"] = true;
                HttpContext.Items["ErrorAdditionalData"] =
                    JsonSerializer.Serialize(entity, UpdateEnquiryDraftContext.Default.Enquiry);
                AddError(
                    "The enquiry's data has changed since you last accessed this page. Please review the current draft and try again.",
                    "error.enquiry.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
