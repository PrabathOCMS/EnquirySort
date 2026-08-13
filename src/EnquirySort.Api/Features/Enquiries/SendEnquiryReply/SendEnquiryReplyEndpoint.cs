using System.Text.Json;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Email;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.SendEnquiryReply;

public sealed class SendEnquiryReplyEndpoint : Endpoint<SendEnquiryReplyRequest, Enquiry>
{
    private readonly EnquiriesRepository _repo;
    private readonly ImapEmailClient _mail;
    private readonly AppSettings _settings;

    public SendEnquiryReplyEndpoint(EnquiriesRepository repo, ImapEmailClient mail, AppSettings settings)
    {
        _repo = repo;
        _mail = mail;
        _settings = settings;
    }

    public override void Configure()
    {
        Post("/enquiries/sendReply");
        SerializerContext(SendEnquiryReplyContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(SendEnquiryReplyRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        Enquiry? current = await _repo.GetEnquiryAsync(req.id!.Value, ct);
        if (current is null)
        {
            HttpContext.Items["FatalError"] = true;
            AddError("The selected enquiry did not exist.", "error.enquiry.didNotExist");
            await Send.ErrorsAsync();
            return;
        }

        if (current.ReplySent || current.ReplyStatus == ReplyStatus.Sent)
        {
            AddError("This enquiry reply was already sent.", "error.enquiry.replyAlreadySent");
            await Send.ErrorsAsync();
            return;
        }

        string replyBody = string.IsNullOrWhiteSpace(req.ReplyBody) ? current.ReplyBody ?? "" : req.ReplyBody.Trim();
        if (string.IsNullOrWhiteSpace(replyBody))
        {
            AddError(m => m.ReplyBody!, "Reply body is required before sending.",
                "error.enquiry.replyBodyIsRequired");
            await Send.ErrorsAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Mail.EmailAddress)
            || string.IsNullOrWhiteSpace(_settings.Mail.EmailPassword))
        {
            AddError(
                "Set Mail:EmailAddress and Mail:EmailPassword before sending a reply.",
                "error.mail.credentialsRequired");
            await Send.ErrorsAsync();
            return;
        }

        if (_settings.Mail.DryRun)
        {
            AddError(
                "Mail:DryRun is true. Set Mail:DryRun to false in appsettings to send the approved reply.",
                "error.mail.dryRunEnabled");
            await Send.ErrorsAsync();
            return;
        }

        try
        {
            InboundEmail original = new()
            {
                MessageId = current.MessageId,
                FromAddress = current.FromAddress,
                Subject = current.Subject,
                BodyText = current.BodyText
            };
            await _mail.SendReplyAsync(original, replyBody, ct);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            AddError(
                $"SMTP login failed: {ex.Message}. Check Mail:EmailAddress and Mail:EmailPassword.",
                "error.mail.authenticationFailed");
            await Send.ErrorsAsync();
            return;
        }

        req.ReplyBody = replyBody;
        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (SqlQueryResult queryResult, Enquiry? entity) =
            await _repo.MarkEnquiryReplySentAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(SendEnquiryReplyRequest req)
    {
        if (!req.id.HasValue)
        {
            AddError(m => m.id!, "Id is required.", "error.enquiry.idIsRequired");
        }

        if (!string.IsNullOrWhiteSpace(req.ReplyBody) && req.ReplyBody.Length > 100000)
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
                AddError("This enquiry reply was already sent.", "error.enquiry.replyAlreadySent");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                HttpContext.Items["ConcurrencyKeyInvalid"] = true;
                HttpContext.Items["ErrorAdditionalData"] =
                    JsonSerializer.Serialize(entity, SendEnquiryReplyContext.Default.Enquiry);
                AddError(
                    "The enquiry's data has changed since you last accessed this page. Please review and try again.",
                    "error.enquiry.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
