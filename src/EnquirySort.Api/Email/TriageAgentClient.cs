using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Email;

public sealed class TriageAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly ILogger<TriageAgentClient> _logger;

    public TriageAgentClient(HttpClient httpClient, AppSettings settings, ILogger<TriageAgentClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<TriageAgentResult> TriageAsync(
        InboundEmail message,
        IReadOnlyList<MailingList> mailingLists,
        IReadOnlyList<KnowledgeArticle> knowledgeArticles,
        string? responseRules,
        CancellationToken cancellationToken = default)
    {
        TriageAgentRequest payload = new()
        {
            Subject = message.Subject,
            Body = Truncate(message.BodyText, _settings.EnquiryWorker.MaxBodyChars),
            FromAddress = message.FromAddress,
            ResponseRules = responseRules ?? _settings.Ai.ResponseRules,
            MailingLists = mailingLists.Select(ml => new TriageMailingListDto
            {
                Name = ml.Name,
                Address = ml.Address,
                Description = ml.Description
            }).ToList(),
            KnowledgeArticles = knowledgeArticles.Select(a => new TriageKnowledgeArticleDto
            {
                Id = a.id.ToString(),
                Title = a.Title,
                Slug = a.Slug,
                Content = Truncate(a.Content, 12000)
            }).ToList()
        };

        using HttpRequestMessage request = new(HttpMethod.Post, CombineUrl(_settings.Ai.AgentBaseUrl, "/triage"));
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Triage agent failed status={Status} body={Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException(
                $"Triage agent returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        TriageAgentResponse? parsed = JsonSerializer.Deserialize<TriageAgentResponse>(body, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("Triage agent returned an empty response.");
        }

        return new TriageAgentResult
        {
            Classification = new ClassificationResult
            {
                Action = ParseAction(parsed.Action),
                Confidence = parsed.Confidence,
                Reason = parsed.Reason ?? string.Empty,
                MailingList = parsed.MailingList,
                CustomerQuestion = parsed.CustomerQuestion
            },
            DraftReply = parsed.DraftReply,
            RetrievedArticleIds = parsed.RetrievedArticleIds ?? []
        };
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

    private static string CombineUrl(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value[..max];
    }

    private sealed class TriageAgentRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string? ResponseRules { get; set; }
        public List<TriageMailingListDto> MailingLists { get; set; } = [];
        public List<TriageKnowledgeArticleDto> KnowledgeArticles { get; set; } = [];
    }

    private sealed class TriageMailingListDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class TriageKnowledgeArticleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class TriageAgentResponse
    {
        public string? Action { get; set; }
        public double Confidence { get; set; }
        public string? Reason { get; set; }
        public string? MailingList { get; set; }
        public string? CustomerQuestion { get; set; }
        public string? DraftReply { get; set; }
        public List<string>? RetrievedArticleIds { get; set; }
        public string? Error { get; set; }
    }
}

public sealed class TriageAgentResult
{
    public ClassificationResult Classification { get; set; } = new();
    public string? DraftReply { get; set; }
    public List<string> RetrievedArticleIds { get; set; } = [];
}
