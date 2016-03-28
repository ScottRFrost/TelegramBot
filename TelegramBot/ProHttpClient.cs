using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TelegramBot
{
    public class ProHttpClient : HttpClient
    {
        public ProHttpClient()
        {
            Timeout = new TimeSpan(0, 0, 30);
            ReferrerUri = "https://duckduckgo.com";
            AuthorizationHeader = string.Empty;
            DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
            DefaultRequestHeaders.TryAddWithoutValidation("DNT", "1");
            DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:45.0) Gecko/20100101 Firefox/45.0");
        }

        public string AuthorizationHeader { get; set; }

        public string ReferrerUri { get; set; }

        public async Task<string> DownloadString(string uri)
        {
            BuildHeaders();
            var response = await GetStringAsync(uri);
            CleanHeaders();
            return response;
        }

        public async Task<Stream> DownloadData(string uri)
        {
            BuildHeaders();
            var response = await GetStreamAsync(uri);
            CleanHeaders();
            return response;
        }

        void BuildHeaders()
        {
            DefaultRequestHeaders.Referrer = new Uri(ReferrerUri);
            if (AuthorizationHeader != string.Empty)
            {
                DefaultRequestHeaders.TryAddWithoutValidation("Authorization", AuthorizationHeader);
            }
        }

        void CleanHeaders()
        {
            ReferrerUri = "https://duckduckgo.com";
            AuthorizationHeader = string.Empty;
            DefaultRequestHeaders.Remove("Authorization");
        }
    }
}
