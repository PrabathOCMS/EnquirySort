using EnquirySort.Api.Configuration;
using EnquirySort.Api.Email;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;
using EnquirySort.Api.Repositories;

namespace EnquirySort.Api.Services;

public sealed class EnquiryPipeline
{
    private readonly AppSettings _settings;
    private readonly RuntimeAppSettings _runtimeSettings;
    private readonly OpenRouterClient _openRouter;
    private readonly ImapEmailClient _mail;
    private readonly MailingListsRepository _mailingLists;
    private readonly KnowledgeArticlesRepository _knowledgeArticles;
    private readonly EnquiriesRepository _enquiries;
    private readonly ILogger<EnquiryPipeline> _logger;

    public EnquiryPipeline(
        AppSettings settings,
        RuntimeAppSettings runtimeSettings,
        OpenRouterClient openRouter,
        ImapEmailClient mail,
        MailingListsRepository mailingLists,
        KnowledgeArticlesRepository knowledgeArticles,
        EnquiriesRepository enquiries,
        ILogger<EnquiryPipeline> logger)
    {
        _settings = settings;
        _runtimeSettings = runtimeSettings;
        _openRouter = openRouter;
        _mail = mail;
        _mailingLists = mailingLists;
        _knowledgeArticles = knowledgeArticles;
        _enquiries = enquiries;
        _logger = logger;
    }

    public async Task<List<Enquiry>> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        List<InboundEmail> unread = await _mail.FetchUnreadAsync(cancellationToken);
        _logger.LogInformation("Fetched {Count} unread message(s)", unread.Count);
        List<Enquiry> results = [];
        foreach (InboundEmail message in unread)
        {
            try
            {
                results.Add(await ProcessMessageAsync(message, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing uid={Uid}", message.Uid);
            }
        }

        return results;
    }

    public async Task<Enquiry> ProcessMessageAsync(InboundEmail message, CancellationToken cancellationToken = default)
    {
        List<MailingList> lists = await _mailingLists.ListAllActiveAsync(cancellationToken);
        ClassificationResult classification = await _openRouter.ClassifyAsync(message, lists, cancellationToken);
        classification = ApplyThresholds(classification, lists);

        Enquiry enquiry = new()
        {
            MessageId = message.MessageId,
            FromAddress = message.FromAddress,
            Subject = message.Subject,
            BodyText = message.BodyText,
            Action = classification.Action,
            Confidence = classification.Confidence,
            Reason = classification.Reason,
            CustomerQuestion = classification.CustomerQuestion,
            ProcessedUtc = DateTime.UtcNow
        };

        if (classification.Action == EnquiryAction.Respond)
        {
            string query = classification.CustomerQuestion ?? $"{message.Subject}\n{message.BodyText}";
            List<KnowledgeArticle> snippets = await _knowledgeArticles.SearchAsync(query, 3, cancellationToken);
            string reply = await _openRouter.DraftReplyAsync(message, snippets, classification.CustomerQuestion, cancellationToken);
            enquiry.ReplyBody = reply;

            Models.AppSetting runtime = await _runtimeSettings.GetAsync(cancellationToken);
            ResponseMode responseMode = runtime.ResponseMode;

            if (responseMode == ResponseMode.Automatic)
            {
                await _mail.SendReplyAsync(message, reply, runtime.EmailSignatureHtml, cancellationToken);
                enquiry.ReplySent = !_settings.Mail.DryRun;
                enquiry.ReplyStatus = enquiry.ReplySent ? ReplyStatus.Sent : ReplyStatus.Draft;
                if (_settings.Mail.DryRun)
                {
                    enquiry.Reason = AppendReason(enquiry.Reason, "auto-reply drafted but DryRun=true (not sent)");
                }
            }
            else
            {
                enquiry.ReplySent = false;
                enquiry.ReplyStatus = ReplyStatus.Draft;
                enquiry.Reason = AppendReason(enquiry.Reason, "draft reply awaiting approval");
            }
        }
        else if (classification.Action == EnquiryAction.Route)
        {
            MailingList? target = ResolveList(lists, classification.MailingList) ?? lists.FirstOrDefault();
            if (target is not null)
            {
                await _mail.ForwardToListAsync(message, target.Address, classification.Reason, cancellationToken);
                enquiry.RoutedToMailingListId = target.id;
                enquiry.RoutedToMailingListName = target.Name;
                enquiry.ReplyStatus = ReplyStatus.None;
            }
            else
            {
                enquiry.Action = EnquiryAction.Ignore;
                enquiry.Reason = "Route requested but no mailing lists configured";
                enquiry.ReplyStatus = ReplyStatus.None;
            }
        }
        else
        {
            enquiry.ReplyStatus = ReplyStatus.None;
        }

        await _mail.MarkProcessedAsync(message.Uid, cancellationToken);
        Enquiry saved = await _enquiries.CreateEnquiryAsync(enquiry);
        return saved;
    }

    private static string AppendReason(string? reason, string suffix)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return suffix;
        }

        return $"{reason} ({suffix})";
    }

    private ClassificationResult ApplyThresholds(ClassificationResult classification, List<MailingList> lists)
    {
        if (classification.Action == EnquiryAction.Respond
            && classification.Confidence < _settings.EnquiryWorker.RespondConfidenceThreshold)
        {
            if (!string.IsNullOrWhiteSpace(classification.MailingList) || lists.Count > 0)
            {
                classification.Action = EnquiryAction.Route;
                classification.Reason += " (low respond confidence; routed)";
                classification.MailingList ??= lists.FirstOrDefault()?.Name;
            }
            else
            {
                classification.Action = EnquiryAction.Ignore;
                classification.Reason += " (below confidence thresholds)";
            }
        }
        else if (classification.Action == EnquiryAction.Route
                 && classification.Confidence < _settings.EnquiryWorker.RouteConfidenceThreshold)
        {
            classification.Action = EnquiryAction.Ignore;
            classification.Reason += " (route confidence below threshold)";
        }

        return classification;
    }

    private static MailingList? ResolveList(List<MailingList> lists, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string needle = name.Trim().ToLowerInvariant();
        return lists.FirstOrDefault(l =>
                   string.Equals(l.Name, needle, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(l.Address, needle, StringComparison.OrdinalIgnoreCase))
               ?? lists.FirstOrDefault(l => l.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
