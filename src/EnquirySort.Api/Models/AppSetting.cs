using EnquirySort.Api.Enums;

namespace EnquirySort.Api.Models;

public sealed class AppSetting
{
    public Guid id { get; set; }
    public ResponseMode ResponseMode { get; set; } = ResponseMode.Draft;
    public string? EmailSignatureHtml { get; set; }
    public DateTime InsertDateUtc { get; set; }
    public DateTime UpdatedDateUtc { get; set; }
    public bool Deleted { get; set; }
    public byte[] ConcurrencyKey { get; set; } = [];
}
