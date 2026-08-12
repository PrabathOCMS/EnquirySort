using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.Enquiries.GetEnquiry;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GetEnquiryRequest))]
[JsonSerializable(typeof(Enquiry))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class GetEnquiryContext : JsonSerializerContext;
