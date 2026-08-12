namespace EnquirySort.Api.Models;

public sealed class DropdownResponse
{
    public long? RequestCounter { get; set; }
    public List<SelectListItem> Records { get; set; } = [];
}
