using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.CreateKnowledgeArticle;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateKnowledgeArticleRequest))]
[JsonSerializable(typeof(KnowledgeArticle))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class CreateKnowledgeArticleContext : JsonSerializerContext;
