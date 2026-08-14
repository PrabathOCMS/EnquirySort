using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Email;

public sealed class OpenRouterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(HttpClient httpClient, AppSettings settings, ILogger<OpenRouterClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        InboundEmail message,
        IReadOnlyList<MailingList> mailingLists,
        CancellationToken cancellationToken = default)
    {
        string listsBlock = mailingLists.Count == 0
            ? "(no mailing lists configured)"
            : string.Join('\n', mailingLists.Select(ml =>
                $"- name: {ml.Name}\n  address: {ml.Address}\n  description: {ml.Description}"));

        string system = """
            You are EnquirySort, an email triage assistant. Decide whether an inbound email should be
            answered automatically from a knowledge base, forwarded to an internal mailing list, or ignored.
            Return ONLY valid JSON:
            {
              "action": "respond" | "route" | "ignore",
              "confidence": number between 0 and 1,
              "reason": string,
              "mailing_list": string | null,
              "customer_question": string | null
            }
            Use respond for FAQs/how-tos. Use route for sales, bugs, billing, legal, or anything needing a human.
            """;

        string user =
            $"Configured mailing lists:\n{listsBlock}\n\n" +
            $"From: {message.FromAddress}\nSubject: {message.Subject}\n" +
            $"Body:\n{Truncate(message.BodyText, _settings.EnquiryWorker.MaxBodyChars)}";

        string content = await ChatAsync(system, user, 0.1, cancellationToken);
        ClassificationDto? dto = ParseJson<ClassificationDto>(content);
        ClassificationResult result = new()
        {
            Action = ParseAction(dto?.Action),
            Confidence = dto?.Confidence ?? 0,
            Reason = dto?.Reason ?? "Unparseable model output",
            MailingList = dto?.MailingList,
            CustomerQuestion = dto?.CustomerQuestion
        };

        _logger.LogInformation(
            "Classified action={Action} confidence={Confidence:0.00} list={List}",
            result.Action,
            result.Confidence,
            result.MailingList);

        return result;
    }

    public async Task<List<KnowledgeArticle>> SelectRelevantKnowledgeAsync(
        InboundEmail message,
        string? question,
        IReadOnlyList<KnowledgeArticle> catalog,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (catalog.Count == 0)
        {
            return [];
        }

        if (catalog.Count <= topK)
        {
            // Small knowledge bases: let drafting see everything.
            return catalog.ToList();
        }

        string catalogBlock = string.Join("\n\n", catalog.Select(a =>
            $"- id: {a.id}\n  title: {a.Title}\n  slug: {a.Slug}\n  summary: {Truncate(a.Content, 280)}"));

        string system = """
            You select knowledge-base articles that can answer a customer email.
            Return ONLY valid JSON:
            {
              "article_ids": ["guid", "..."],
              "reason": string
            }
            Pick at most the requested number of articles. Prefer precise how-to matches.
            If nothing is relevant, return an empty article_ids array.
            Only use ids from the catalog.
            """;

        string user =
            $"Select up to {topK} articles.\n\n" +
            $"Customer question summary: {question ?? message.Subject}\n\n" +
            $"Original email:\nFrom: {message.FromAddress}\nSubject: {message.Subject}\n" +
            $"Body:\n{Truncate(message.BodyText, 4000)}\n\n" +
            $"Knowledge catalog:\n{catalogBlock}";

        string content = await ChatAsync(system, user, 0.0, cancellationToken);
        KnowledgeSelectDto? dto = ParseJson<KnowledgeSelectDto>(content);
        List<Guid> selectedIds = (dto?.ArticleIds ?? [])
            .Select(ParseGuid)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Take(topK)
            .ToList();

        List<KnowledgeArticle> selected = selectedIds
            .Select(id => catalog.FirstOrDefault(a => a.id == id))
            .Where(a => a is not null)
            .Cast<KnowledgeArticle>()
            .ToList();

        _logger.LogInformation(
            "AI knowledge select reason={Reason} selected={Count}/{Catalog}",
            dto?.Reason ?? "(none)",
            selected.Count,
            catalog.Count);

        return selected;
    }

    public async Task<string> DraftReplyAsync(
        InboundEmail message,
        IReadOnlyList<KnowledgeArticle> snippets,
        string? question,
        CancellationToken cancellationToken = default)
    {
        string kbBlock = snippets.Count == 0
            ? "(no knowledge base snippets matched)"
            : string.Join("\n\n---\n\n", snippets.Select(s => $"# {s.Title} ({s.Slug})\n{s.Content}"));

        string system = """
            You write helpful, concise customer-support email replies.
            Use ONLY the provided knowledge base excerpts.
            When the knowledge base includes a URL or concrete steps, include them exactly.
            Do not invent product URLs, policies, or steps that are not in the excerpts.
            If no useful excerpts are provided, say you could not find matching documentation and
            that a human will follow up — do not guess a generic how-to.
            Plain text only. Sign off as EnquirySort Support.
            """;

        string user =
            $"Customer question summary: {question ?? message.Subject}\n\n" +
            $"Original email:\nFrom: {message.FromAddress}\nSubject: {message.Subject}\nBody:\n{Truncate(message.BodyText, 6000)}\n\n" +
            $"Knowledge base excerpts:\n{kbBlock}";

        return (await ChatAsync(system, user, 0.3, cancellationToken)).Trim();
    }

    private async Task<string> ChatAsync(string system, string user, double temperature, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"{_settings.OpenRouter.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouter.ApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", _settings.OpenRouter.SiteUrl);
        request.Headers.TryAddWithoutValidation("X-Title", _settings.OpenRouter.AppName);

        object payload = new
        {
            model = _settings.OpenRouter.Model,
            temperature,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static T? ParseJson<T>(string text)
    {
        text = text.Trim();
        try
        {
            return JsonSerializer.Deserialize<T>(text, JsonOptions);
        }
        catch (JsonException)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(text[start..(end + 1)], JsonOptions);
                }
                catch (JsonException)
                {
                    return default;
                }
            }

            return default;
        }
    }

    private static EnquiryAction ParseAction(string? action)
    {
        return (action ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "respond" => EnquiryAction.Respond,
            "route" => EnquiryAction.Route,
            _ => EnquiryAction.Ignore
        };
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid id) ? id : null;
    }

    private sealed class ClassificationDto
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("mailing_list")]
        public string? MailingList { get; set; }

        [JsonPropertyName("customer_question")]
        public string? CustomerQuestion { get; set; }
    }

    private sealed class KnowledgeSelectDto
    {
        [JsonPropertyName("article_ids")]
        public List<string>? ArticleIds { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
