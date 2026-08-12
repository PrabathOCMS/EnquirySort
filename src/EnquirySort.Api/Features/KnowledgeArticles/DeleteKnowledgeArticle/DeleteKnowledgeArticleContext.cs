using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.DeleteKnowledgeArticle;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeleteKnowledgeArticleRequest))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class DeleteKnowledgeArticleContext : JsonSerializerContext;
