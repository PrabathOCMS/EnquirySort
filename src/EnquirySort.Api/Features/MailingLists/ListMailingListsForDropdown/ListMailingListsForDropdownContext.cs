using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.MailingLists.ListMailingListsForDropdown;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListMailingListsForDropdownRequest))]
[JsonSerializable(typeof(DropdownResponse))]
[JsonSerializable(typeof(SelectListItem))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ListMailingListsForDropdownContext : JsonSerializerContext;
