using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.Enquiries.SendEnquiryReply;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SendEnquiryReplyRequest))]
[JsonSerializable(typeof(Enquiry))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class SendEnquiryReplyContext : JsonSerializerContext;
