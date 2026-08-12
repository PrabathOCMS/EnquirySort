namespace EnquirySort.Api.Models;

public sealed class DataTableResponse<T>
{
    public long? RequestCounter { get; set; }
    public List<T> Records { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
