using EnquirySort.Api.Enums;

namespace EnquirySort.Api.Features.AdminSettings.UpdateAppSettings;

public sealed class UpdateAppSettingsRequest
{
    public Guid? id { get; set; }
    public ResponseMode? ResponseMode { get; set; }
    public string? EmailSignatureHtml { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
