using EnquirySort.Api.Configuration;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace EnquirySort.Api.Email;

public sealed class ImapEmailClient
{
    private readonly AppSettings _settings;
    private readonly ILogger<ImapEmailClient> _logger;

    public ImapEmailClient(AppSettings settings, ILogger<ImapEmailClient> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<InboundEmail>> FetchUnreadAsync(CancellationToken cancellationToken = default)
    {
        using ImapClient client = new();
        await client.ConnectAsync(_settings.Mail.ImapHost, _settings.Mail.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(_settings.Mail.EmailAddress, _settings.Mail.EmailPassword, cancellationToken);
        IMailFolder inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        IList<UniqueId> uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
        List<InboundEmail> messages = [];

        foreach (UniqueId uid in uids.TakeLast(20))
        {
            MimeMessage mime = await inbox.GetMessageAsync(uid, cancellationToken);
            messages.Add(new InboundEmail
            {
                Uid = uid.Id.ToString(),
                MessageId = mime.MessageId,
                Subject = string.IsNullOrWhiteSpace(mime.Subject) ? "(no subject)" : mime.Subject,
                FromAddress = mime.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
                BodyText = mime.TextBody ?? StripHtml(mime.HtmlBody) ?? string.Empty,
                InReplyTo = mime.InReplyTo,
                References = mime.References.Count > 0 ? string.Join(" ", mime.References) : null
            });
        }

        await client.DisconnectAsync(true, cancellationToken);
        return messages;
    }

    public async Task MarkProcessedAsync(string uid, CancellationToken cancellationToken = default)
    {
        if (_settings.Mail.DryRun)
        {
            _logger.LogInformation("[dry-run] Would mark seen / move uid={Uid}", uid);
            return;
        }

        if (!uint.TryParse(uid, out uint id))
        {
            return;
        }

        using ImapClient client = new();
        await client.ConnectAsync(_settings.Mail.ImapHost, _settings.Mail.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(_settings.Mail.EmailAddress, _settings.Mail.EmailPassword, cancellationToken);
        IMailFolder inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        UniqueId uniqueId = new(id);
        await inbox.AddFlagsAsync(uniqueId, MessageFlags.Seen, true, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.Mail.ProcessedFolder))
        {
            IMailFolder? destination = await EnsureFolderAsync(client, _settings.Mail.ProcessedFolder, cancellationToken);
            if (destination is not null)
            {
                await inbox.MoveToAsync(uniqueId, destination, cancellationToken);
            }
        }

        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendReplyAsync(InboundEmail original, string body, CancellationToken cancellationToken = default)
    {
        await SendReplyAsync(original, body, signatureHtml: null, cancellationToken);
    }

    public async Task SendReplyAsync(
        InboundEmail original,
        string body,
        string? signatureHtml,
        CancellationToken cancellationToken = default)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(_settings.Mail.EmailAddress));
        message.To.Add(MailboxAddress.Parse(original.FromAddress));
        message.Subject = original.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? original.Subject
            : $"Re: {original.Subject}";

        if (!string.IsNullOrWhiteSpace(original.MessageId))
        {
            message.InReplyTo = original.MessageId;
            message.References.Add(original.MessageId);
        }

        message.Body = EmailBodyComposer.BuildReplyBody(body, signatureHtml);
        await SendAsync(message, cancellationToken);
    }

    public async Task ForwardToListAsync(InboundEmail original, string listAddress, string note, CancellationToken cancellationToken = default)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(_settings.Mail.EmailAddress));
        message.To.Add(MailboxAddress.Parse(listAddress));
        message.ReplyTo.Add(MailboxAddress.Parse(original.FromAddress));
        message.Subject = $"[Routed] {original.Subject}";
        message.Body = new TextPart("plain")
        {
            Text = string.Join('\n',
                "This enquiry was automatically routed by EnquirySort.",
                $"Original From: {original.FromAddress}",
                $"Original Subject: {original.Subject}",
                string.IsNullOrWhiteSpace(note) ? null : $"Classifier note: {note}",
                string.Empty,
                "----- Original Message -----",
                original.BodyText)
        };
        await SendAsync(message, cancellationToken);
    }

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        if (_settings.Mail.DryRun)
        {
            _logger.LogInformation("[dry-run] Would send To={To} Subject={Subject}", message.To, message.Subject);
            return;
        }

        _logger.LogInformation("Sending email To={To} Subject={Subject}", message.To, message.Subject);
        using SmtpClient smtp = new();
        await smtp.ConnectAsync(_settings.Mail.SmtpHost, _settings.Mail.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
        await smtp.AuthenticateAsync(_settings.Mail.EmailAddress, _settings.Mail.EmailPassword, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    private static async Task<IMailFolder?> EnsureFolderAsync(ImapClient client, string path, CancellationToken cancellationToken)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        FolderNamespace personal = client.PersonalNamespaces[0];
        IMailFolder current = await client.GetFolderAsync(personal.Path, cancellationToken);
        foreach (string part in parts)
        {
            IList<IMailFolder> children = await current.GetSubfoldersAsync(false, cancellationToken);
            IMailFolder? next = children.FirstOrDefault(f =>
                string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                next = await current.CreateAsync(part, true, cancellationToken);
            }

            current = next!;
        }

        return current;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
    }
}
