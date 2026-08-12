using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDataTable;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListKnowledgeArticlesForDataTableRequest))]
[JsonSerializable(typeof(DataTableResponse<KnowledgeArticle>))]
[JsonSerializable(typeof(KnowledgeArticle))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ListKnowledgeArticlesForDataTableContext : JsonSerializerContext;
