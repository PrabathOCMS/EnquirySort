using EnquirySort.Api.Configuration;
using EnquirySort.Api.Models;
using EnquirySort.Api.Services;
using FastEndpoints;

namespace EnquirySort.Api.Features.Enquiries.ProcessInbox;

public sealed class ProcessInboxEndpoint : EndpointWithoutRequest<List<Enquiry>>
{
    private readonly EnquiryPipeline _pipeline;
    private readonly AppSettings _settings;

    public ProcessInboxEndpoint(EnquiryPipeline pipeline, AppSettings settings)
    {
        _pipeline = pipeline;
        _settings = settings;
    }

    public override void Configure()
    {
        Post("/enquiries/processInbox");
        SerializerContext(ProcessInboxContext.Default);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ValidateConfiguration();
        if (ValidationFailed)
        {
            await Send.ErrorsAsync();
            return;
        }

        try
        {
            List<Enquiry> results = await _pipeline.ProcessInboxAsync(ct);
            await Send.OkAsync(results);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            AddError(
                $"IMAP login failed: {ex.Message}. Check Mail:EmailAddress and Mail:EmailPassword (use a Gmail App Password if using Google).",
                "error.mail.authenticationFailed");
            await Send.ErrorsAsync();
        }
        catch (HttpRequestException ex)
        {
            AddError(
                $"OpenRouter request failed: {ex.Message}. Check OpenRouter:ApiKey and network access.",
                "error.openRouter.requestFailed");
            await Send.ErrorsAsync();
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.Mail.EmailAddress)
            || string.IsNullOrWhiteSpace(_settings.Mail.EmailPassword))
        {
            AddError(
                "Set Mail:EmailAddress and Mail:EmailPassword in appsettings.Development.json (or environment variables) before processing the inbox.",
                "error.mail.credentialsRequired");
        }

        if (string.IsNullOrWhiteSpace(_settings.OpenRouter.ApiKey))
        {
            AddError(
                "Set OpenRouter:ApiKey in appsettings.Development.json (or environment variables) before processing the inbox.",
                "error.openRouter.apiKeyRequired");
        }
    }
}
