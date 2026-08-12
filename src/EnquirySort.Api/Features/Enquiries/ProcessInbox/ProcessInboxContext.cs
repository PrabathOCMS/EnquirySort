using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.Enquiries.ProcessInbox;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProcessInboxRequest))]
[JsonSerializable(typeof(List<Enquiry>))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class ProcessInboxContext : JsonSerializerContext;
