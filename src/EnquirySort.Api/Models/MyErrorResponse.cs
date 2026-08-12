namespace EnquirySort.Api.Models;

public sealed class MyErrorResponse
{
    public Dictionary<string, List<ErrorMessageItem>> ErrorMessages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool FatalError { get; set; }
    public bool ConcurrencyKeyInvalid { get; set; }
    public string? AdditionalData { get; set; }
    public string? TraceId { get; set; }
}

public sealed class ErrorMessageItem
{
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
