using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace OmniTactica.AppCode.Utilities
{
    internal class HtmlUtility
    {
        public static string ConvertHtmlToPlainText(string html)
        {
            // 1. Create a new HtmlDocument instance
            HtmlDocument doc = new HtmlDocument();

            // 2. Load the HTML content
            doc.LoadHtml(html);

            // 3. Access the InnerText property of the main document node
            string plainText = doc.DocumentNode.InnerText;

            // 4. Decode HTML entities for a cleaner result
            // Note: In modern .NET, use WebUtility.HtmlDecode. 
            // If using older .NET frameworks, you might need System.Web.HttpUtility.HtmlDecode
            return WebUtility.HtmlDecode(plainText);
        }
    }
}
