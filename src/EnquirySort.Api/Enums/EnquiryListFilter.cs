namespace EnquirySort.Api.Enums;

/// <summary>
/// Filter for the enquiries list (open queue vs completed outcomes).
/// </summary>
public enum EnquiryListFilter
{
    /// <summary>Draft replies awaiting human edit/approve/send.</summary>
    Open = 0,

    /// <summary>Customer reply was sent.</summary>
    Responded = 1,

    /// <summary>Classified as ignore.</summary>
    Ignored = 2,

    /// <summary>Forwarded to a mailing list.</summary>
    Routed = 3,

    /// <summary>No status filter.</summary>
    All = 4
}
