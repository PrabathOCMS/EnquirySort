namespace EnquirySort.Api.Configuration;

public sealed class AppSettings
{
    public ConnectionStringsSettings ConnectionStrings { get; set; } = new();
    public MailSettings Mail { get; set; } = new();
    public OpenRouterSettings OpenRouter { get; set; } = new();
    public EnquiryWorkerSettings EnquiryWorker { get; set; } = new();
}

public sealed class ConnectionStringsSettings
{
    public string EnquirySort { get; set; } = string.Empty;
}

public sealed class MailSettings
{
    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string EmailAddress { get; set; } = string.Empty;
    public string EmailPassword { get; set; } = string.Empty;
    public string Mailbox { get; set; } = "INBOX";
    public string ProcessedFolder { get; set; } = "EnquirySort/Processed";
    public bool DryRun { get; set; }
}

public sealed class OpenRouterSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "openai/gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string SiteUrl { get; set; } = "https://github.com/PrabathOCMS/EnquirySort";
    public string AppName { get; set; } = "EnquirySort";
}

public sealed class EnquiryWorkerSettings
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public double RespondConfidenceThreshold { get; set; } = 0.65;
    public double RouteConfidenceThreshold { get; set; } = 0.55;
    public int MaxBodyChars { get; set; } = 8000;
}
