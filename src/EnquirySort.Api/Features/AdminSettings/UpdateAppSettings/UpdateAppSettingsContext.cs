using System.Text.Json.Serialization;
using EnquirySort.Api.Models;

namespace EnquirySort.Api.Features.AdminSettings.UpdateAppSettings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateAppSettingsRequest))]
[JsonSerializable(typeof(AppSetting))]
[JsonSerializable(typeof(MyErrorResponse))]
internal sealed partial class UpdateAppSettingsContext : JsonSerializerContext;
