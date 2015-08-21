using System;
using System.Net;

namespace TelegramBot
{
    public class ProWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            Encoding = System.Text.Encoding.UTF8;
            var request = base.GetWebRequest(address) as HttpWebRequest;
            if (request == null) return null;
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers.Add("accept-encoding", "gzip, deflate");
            request.Headers.Add("accept-language", "en-US,en;q=0.5");
            request.Headers.Add("dnt", "1");
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:41.0) Gecko/20100101 Firefox/41.0";
            request.KeepAlive = true;
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 8;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.Timeout = 30000;
            request.Referer = "https://duckduckgo.com";
            return request;
        }
    }
}
