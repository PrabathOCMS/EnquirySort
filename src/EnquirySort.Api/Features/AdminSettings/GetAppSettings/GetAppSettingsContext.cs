using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.AdminSettings.GetAppSettings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSetting))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class GetAppSettingsContext : JsonSerializerContext;
