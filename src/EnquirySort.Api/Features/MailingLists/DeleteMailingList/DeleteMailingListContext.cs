using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.DeleteMailingList;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeleteMailingListRequest))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class DeleteMailingListContext : JsonSerializerContext;
