using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EnquirySort.Api.Utilities;

public static partial class Toolbox
{
    public static bool ByteArrayEqual(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null || a.Length != b.Length)
        {
            return false;
        }

        return a.AsSpan().SequenceEqual(b);
    }

    public static string Sha1Upper(string value)
    {
        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return EmailRegex().IsMatch(email.Trim());
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
