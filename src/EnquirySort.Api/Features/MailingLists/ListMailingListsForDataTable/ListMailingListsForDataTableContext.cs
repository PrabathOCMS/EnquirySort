using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDataTable;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListMailingListsForDataTableRequest))]
[JsonSerializable(typeof(DataTableResponse<MailingList>))]
[JsonSerializable(typeof(MailingList))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ListMailingListsForDataTableContext : JsonSerializerContext;
