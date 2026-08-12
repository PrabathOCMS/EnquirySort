using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.UpdateKnowledgeArticle;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateKnowledgeArticleRequest))]
[JsonSerializable(typeof(KnowledgeArticle))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class UpdateKnowledgeArticleContext : JsonSerializerContext;
