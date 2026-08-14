using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace EnquirySort.Api.Email;

public static partial class EmailBodyComposer
{
    [GeneratedRegex(
        @"src\s*=\s*[""'](?<data>data:(?<type>image\/[a-zA-Z0-9.+-]+);base64,(?<payload>[A-Za-z0-9+/=\s]+))[""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataUriImageRegex();

    public static MimeEntity BuildReplyBody(string replyText, string? signatureHtml)
    {
        string plainBody = replyText.TrimEnd();
        string plainSignature = HtmlToPlainText(signatureHtml);
        string plain = string.IsNullOrWhiteSpace(plainSignature)
            ? plainBody
            : $"{plainBody}\n\n--\n{plainSignature}";

        string htmlBody = $"<div>{WebUtility.HtmlEncode(replyText).Replace("\n", "<br/>", StringComparison.Ordinal)}</div>";
        string html;
        if (string.IsNullOrWhiteSpace(signatureHtml))
        {
            html = $"<html><body>{htmlBody}</body></html>";
            return BuildAlternative(plain, html, []);
        }

        List<(string Cid, string ContentType, byte[] Bytes)> assets = [];
        string rewrittenSignature = RewriteDataUris(signatureHtml, assets);
        html = $"<html><body>{htmlBody}<br/><div class=\"signature\">{rewrittenSignature}</div></body></html>";
        return BuildAlternative(plain, html, assets);
    }

    private static MimeEntity BuildAlternative(
        string plain,
        string html,
        List<(string Cid, string ContentType, byte[] Bytes)> assets)
    {
        TextPart plainPart = new("plain")
        {
            Text = plain,
            ContentTransferEncoding = ContentEncoding.QuotedPrintable
        };

        TextPart htmlPart = new("html")
        {
            Text = html,
            ContentTransferEncoding = ContentEncoding.QuotedPrintable
        };

        if (assets.Count == 0)
        {
            MultipartAlternative alternative = new("alternative")
            {
                plainPart,
                htmlPart
            };
            return alternative;
        }

        MultipartRelated related = new("related")
        {
            htmlPart
        };

        foreach ((string cid, string contentType, byte[] bytes) in assets)
        {
            string[] typeParts = contentType.Split('/', 2, StringSplitOptions.TrimEntries);
            string mediaType = typeParts.Length > 0 ? typeParts[0] : "image";
            string mediaSubtype = typeParts.Length > 1 ? typeParts[1] : "png";
            MimePart imagePart = new(mediaType, mediaSubtype)
            {
                ContentId = cid,
                Content = new MimeContent(new MemoryStream(bytes)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64
            };
            related.Add(imagePart);
        }

        MultipartAlternative alternativeWithImages = new("alternative")
        {
            plainPart,
            related
        };
        return alternativeWithImages;
    }

    private static string RewriteDataUris(
        string html,
        List<(string Cid, string ContentType, byte[] Bytes)> assets)
    {
        return DataUriImageRegex().Replace(html, match =>
        {
            string contentType = match.Groups["type"].Value.Trim().ToLowerInvariant();
            string payload = Regex.Replace(match.Groups["payload"].Value, @"\s+", "");
            try
            {
                byte[] bytes = Convert.FromBase64String(payload);
                if (bytes.Length == 0 || bytes.Length > 1_500_000)
                {
                    return match.Value;
                }

                string cid = $"{Guid.NewGuid():N}@enquirysort";
                assets.Add((cid, contentType, bytes));
                return $"src=\"cid:{cid}\"";
            }
            catch (FormatException)
            {
                return match.Value;
            }
        });
    }

    public static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        string text = html;
        text = Regex.Replace(text, @"<(br|/p|/div|/li|tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<img[^>]*>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "[ \t]+\n", "\n");
        text = Regex.Replace(text, "\n{3,}", "\n\n");
        text = Regex.Replace(text, "[ \t]{2,}", " ");
        return text.Trim();
    }
}
