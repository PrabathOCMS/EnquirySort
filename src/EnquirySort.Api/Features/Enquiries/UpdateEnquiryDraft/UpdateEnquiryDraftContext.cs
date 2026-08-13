using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.Enquiries.UpdateEnquiryDraft;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateEnquiryDraftRequest))]
[JsonSerializable(typeof(Enquiry))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class UpdateEnquiryDraftContext : JsonSerializerContext;
