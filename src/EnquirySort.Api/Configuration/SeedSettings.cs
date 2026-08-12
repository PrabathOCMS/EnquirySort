namespace EnquirySort.Api.Configuration;

public sealed class SeedSettings
{
    /// <summary>
    /// When true, ensure schema exists and insert demo data if tables are empty.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Also insert a sample processed enquiry when the enquiries table is empty.
    /// </summary>
    public bool SampleEnquiries { get; set; } = true;
}
