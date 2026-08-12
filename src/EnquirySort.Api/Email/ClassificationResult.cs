using EnquirySort.Api.Enums;

namespace EnquirySort.Api.Email;

public sealed class ClassificationResult
{
    public EnquiryAction Action { get; set; } = EnquiryAction.Ignore;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MailingList { get; set; }
    public string? CustomerQuestion { get; set; }
}
