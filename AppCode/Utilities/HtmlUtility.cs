using HtmlAgilityPack;
using System.Net;

namespace OmniTactica.AppCode.Utilities
{
    internal static class HtmlUtility
    {
        public static string ConvertHtmlToPlainText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
        }

        public static string StripHtml(string html)
        {
            return string.IsNullOrEmpty(html) ? string.Empty : ConvertHtmlToPlainText(html);
        }
    }
}
