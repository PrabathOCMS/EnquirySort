using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.GetKnowledgeArticle;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GetKnowledgeArticleRequest))]
[JsonSerializable(typeof(KnowledgeArticle))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class GetKnowledgeArticleContext : JsonSerializerContext;
