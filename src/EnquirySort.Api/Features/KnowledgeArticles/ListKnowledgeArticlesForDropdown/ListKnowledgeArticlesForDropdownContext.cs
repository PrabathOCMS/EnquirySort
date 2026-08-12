using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDropdown;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListKnowledgeArticlesForDropdownRequest))]
[JsonSerializable(typeof(DropdownResponse))]
[JsonSerializable(typeof(SelectListItem))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ListKnowledgeArticlesForDropdownContext : JsonSerializerContext;
