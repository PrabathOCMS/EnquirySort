using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.CreateMailingList;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateMailingListRequest))]
[JsonSerializable(typeof(MailingList))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class CreateMailingListContext : JsonSerializerContext;
