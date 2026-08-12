using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.Enquiries.ListEnquiriesForDataTable;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListEnquiriesForDataTableRequest))]
[JsonSerializable(typeof(DataTableResponse<Enquiry>))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ListEnquiriesForDataTableContext : JsonSerializerContext;
