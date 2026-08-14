using System.Text.Json;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;
using FastEndpoints;

namespace EnquirySort.Api.Features.AdminSettings.UpdateAppSettings;

public sealed class UpdateAppSettingsEndpoint : Endpoint<UpdateAppSettingsRequest, AppSetting>
{
    private const int MaxSignatureChars = 4_000_000;

    private readonly AppSettingsRepository _repo;
    private readonly Services.RuntimeAppSettings _runtime;

    public UpdateAppSettingsEndpoint(AppSettingsRepository repo, Services.RuntimeAppSettings runtime)
    {
        _repo = repo;
        _runtime = runtime;
    }

    public override void Configure()
    {
        Post("/appSettings/update");
        SerializerContext(UpdateAppSettingsContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateAppSettingsRequest req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (SqlQueryResult queryResult, AppSetting? entity) =
            await _repo.UpdateAppSettingsAsync(req, null, null, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        _runtime.ReplaceCache(entity!);
        await Send.OkAsync(entity!);
    }

    private void ValidateInput(UpdateAppSettingsRequest req)
    {
        req.id ??= AppSettingsRepository.SingletonId;

        if (!req.ResponseMode.HasValue
            || (req.ResponseMode is not ResponseMode.Automatic and not ResponseMode.Draft))
        {
            AddError(m => m.ResponseMode!, "Response mode must be Automatic or Draft.",
                "error.appSettings.responseModeInvalid");
        }

        req.EmailSignatureHtml = NormalizeSignature(req.EmailSignatureHtml);
        if (req.EmailSignatureHtml is not null && req.EmailSignatureHtml.Length > MaxSignatureChars)
        {
            AddError(m => m.EmailSignatureHtml!,
                $"Email signature must be {MaxSignatureChars} characters or less (including embedded images).",
                "error.appSettings.emailSignatureHtmlLength");
        }

        if (req.ConcurrencyKey is null)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.appSettings.concurrencyKeyIsRequired");
        }
        else if (req.ConcurrencyKey.Length > 4)
        {
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.appSettings.concurrencyKeyLength|{\"length\":\"4\"}");
        }
    }

    private void ValidateOutput(SqlQueryResult queryResult, AppSetting? entity)
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
                AddError("App settings were deleted since you last accessed this page.",
                    "error.appSettings.deletedSinceAccessedPage");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                HttpContext.Items["ConcurrencyKeyInvalid"] = true;
                HttpContext.Items["ErrorAdditionalData"] =
                    JsonSerializer.Serialize(entity, UpdateAppSettingsContext.Default.AppSetting);
                AddError(
                    "App settings have changed since you last accessed this page. Please review and try again.",
                    "error.appSettings.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }

    private static string? NormalizeSignature(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        string trimmed = html.Trim();
        return trimmed is "" or "<br>" or "<div><br></div>" or "<p><br></p>" ? null : trimmed;
    }
}
