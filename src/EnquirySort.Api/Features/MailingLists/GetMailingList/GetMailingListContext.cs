using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.GetMailingList;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GetMailingListRequest))]
[JsonSerializable(typeof(MailingList))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class GetMailingListContext : JsonSerializerContext;
