namespace EnquirySort.Api.Enums;

/// <summary>
/// How EnquirySort handles AI-generated customer replies.
/// </summary>
public enum ResponseMode
{
    /// <summary>Send the reply immediately after drafting (subject to Mail:DryRun).</summary>
    Automatic = 0,

    /// <summary>Save a draft for a human to edit/approve before sending.</summary>
    Draft = 1
}
