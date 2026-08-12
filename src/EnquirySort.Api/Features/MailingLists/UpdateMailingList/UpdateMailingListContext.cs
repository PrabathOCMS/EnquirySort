using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.UpdateMailingList;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateMailingListRequest))]
[JsonSerializable(typeof(MailingList))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class UpdateMailingListContext : JsonSerializerContext;
