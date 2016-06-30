using System;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Newtonsoft.Json.Linq;
using OverwatchAPI;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBot
{
    #pragma warning disable 4014 // Allow for bot.SendChatAction to not be awaited
    // ReSharper disable FunctionNeverReturns
    #pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
    // ReSharper disable CatchAllClause
class Bot
    {
        static void Main()
        {
            while (true)
            {
                try
                {
                    MainLoop().Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MAIN LOOP EXIT ERROR - " + ex);
                    Thread.Sleep(30000);
                }
            }
        }

        static async Task MainLoop()
        {
            // Read Configuration
            var telegramKey = ConfigurationManager.AppSettings["TelegramKey"];

            // Start Bot
            var bot = new TelegramBotClient(telegramKey);
            var me = await bot.GetMeAsync();
            Console.WriteLine(me.Username + " started at " + DateTime.Now);

            var offset = 0;
            while (true)
            {
                var updates = new Update[0];
                try
                {
                    updates = await bot.GetUpdatesAsync(offset);
                }
                catch (TaskCanceledException)
                {
                    // Don't care
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR WHILE GETTIGN UPDATES - " + ex);
                }
                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    ProcessUpdate(bot, update, me);
                }

                await Task.Delay(1000);
            }
        }

        static async void ProcessUpdate(TelegramBotClient bot, Update update, User me)
        {
            // Read Configuration
            var wundergroundKey = ConfigurationManager.AppSettings["WundergroundKey"];
            var bingKey = ConfigurationManager.AppSettings["BingKey"];
            var wolframAppId = ConfigurationManager.AppSettings["WolframAppID"];

            // Process Request
            try
            {
                var httpClient = new ProHttpClient();
                var text = update.Message.Text;
                var replyText = string.Empty;
                var replyTextMarkdown = string.Empty;
                var replyImage = string.Empty;
                var replyImageCaption = string.Empty;
                var replyDocument = string.Empty;

                if (text != null && (text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("!", StringComparison.Ordinal)))
                {
                    // Log to console
                    Console.WriteLine(update.Message.Chat.Id + " < " + update.Message.From.Username + " - " + text);

                    // Allow ! or /
                    if (text.StartsWith("!", StringComparison.Ordinal))
                    {
                        text = "/" + text.Substring(1);
                    }

                    // Strip @BotName
                    text = text.Replace("@" + me.Username, "");

                    // Parse
                    string command;
                    string body;
                    if (text.StartsWith("/s/", StringComparison.Ordinal))
                    {
                        command = "/s"; // special case for sed
                        body = text.Substring(2);
                    }
                    else
                    {
                        command = text.Split(' ')[0];
                        body = text.Replace(command, "").Trim();
                    }
                    var stringBuilder = new StringBuilder();

                    switch (command.ToLowerInvariant())
                    {
                        case "/beer":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /beer <Name of beer>";
                                break;
                            }

                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            var beerSearch = httpClient.DownloadString("http://www.beeradvocate.com/search/?q=" + HttpUtility.UrlEncode(body) + "&qt=beer").Result.Replace("\r", "").Replace("\n", "");

                            // Load First Result
                            var firstBeer = Regex.Match(beerSearch, @"<div id=""ba-content"">.*?<ul>.*?<li>.*?<a href=""(.*?)"">").Groups[1].Value.Trim();
                            if (firstBeer == string.Empty)
                            {
                                replyText = "The Great & Powerful Trixie was unable to find a beer name matching: " + body;
                                break;
                            }
                            var beer = httpClient.DownloadString("http://www.beeradvocate.com" + firstBeer).Result.Replace("\r", "").Replace("\n", "");
                            var beerName = Regex.Match(beer, @"<title>(.*?)</title>").Groups[1].Value.Replace(" | BeerAdvocate", string.Empty).Trim();
                            beer = Regex.Match(beer, @"<div id=""ba-content"">.*?<div>(.*?)<div style=""clear:both;"">").Groups[1].Value.Trim();
                            replyImage = Regex.Match(beer, @"img src=""(.*?)""").Groups[1].Value.Trim();
                            replyImageCaption = "http://www.beeradvocate.com" + firstBeer;
                            var beerScore = Regex.Match(beer, @"<span class=""BAscore_big ba-score"">(.*?)</span>").Groups[1].Value.Trim();
                            var beerScoreText = Regex.Match(beer, @"<span class=""ba-score_text"">(.*?)</span>").Groups[1].Value.Trim();
                            var beerbroScore = Regex.Match(beer, @"<span class=""BAscore_big ba-bro_score"">(.*?)</span>").Groups[1].Value.Trim();
                            var beerbroScoreText = Regex.Match(beer, @"<b class=""ba-bro_text"">(.*?)</b>").Groups[1].Value.Trim();
                            var beerHads = Regex.Match(beer, @"<span class=""ba-ratings"">(.*?)</span>").Groups[1].Value.Trim();
                            var beerAvg = Regex.Match(beer, @"<span class=""ba-ravg"">(.*?)</span>").Groups[1].Value.Trim();
                            var beerStyle = Regex.Match(beer, @"<b>Style:</b>.*?<b>(.*?)</b>").Groups[1].Value.Trim();
                            var beerAbv = beer.Substring(beer.IndexOf("(ABV):", StringComparison.Ordinal) + 10, 7).Trim();
                            var beerDescription = Regex.Match(beer, @"<b>Notes / Commercial Description:</b>(.*?)</div>").Groups[1].Value.Replace("|", "").Trim();
                            stringBuilder.Append(beerName.Replace("|", "- " + beerStyle + " by") + "\r\nScore: " + beerScore + " (" + beerScoreText + ") | Bros: " + beerbroScore + " (" + beerbroScoreText + ") | Avg: " + beerAvg + " (" + beerHads + " hads)\r\nABV: " + beerAbv + " | ");
                            stringBuilder.Append(HttpUtility.HtmlDecode(beerDescription).Replace("<br>"," ").Trim());
                            break;

                        case "/cat":
                            replyImage = "http://thecatapi.com/api/images/get?format=src&type=jpg,png";
                            break;

                        case "/doge":
                            replyImage = "http://dogr.io/wow/" + body.Replace(",", "/").Replace(" ", "") + ".png";
                            replyImageCaption = "wow";
                            break;

                        case "/echo":
                            replyText = body;
                            break;

                        case "/fat":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /fat <Name of food>";
                                break;
                            }

                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            var search = httpClient.DownloadString("http://www.calorieking.com/foods/search.php?keywords=" + body).Result.Replace("\r", "").Replace("\n", "");

                            // Load First Result
                            var firstUrl = Regex.Match(search, @"<a class=""food-search-result-name"" href=""([\w:\/\-\._]*)""").Groups[1].Value.Trim();
                            if (firstUrl == string.Empty)
                            {
                                replyText = "The Great & Powerful Trixie was unable to find a food name matching: " + body;
                                break;
                            }
                            var food = httpClient.DownloadString(firstUrl).Result.Replace("\r", "").Replace("\n", "");

                            // Scrape it
                            var label = string.Empty;
                            var protein = 0.0;
                            var carbs = 0.0;
                            var fat = 0.0;
                            var fiber = 0.0;
                            stringBuilder.Append(Regex.Match(food, @"<title>(.*)\ \|.*<\/title>").Groups[1].Value.Replace("Calories in ", "").Trim() + " per "); // Name of item
                            stringBuilder.Append(Regex.Match(food, @"<select name=""units"".*?<option.*?>(.*?)<\/option>", RegexOptions.IgnoreCase).Groups[1].Value.Trim() + "\r\n"); // Unit
                            foreach (Match fact in Regex.Matches(food, @"<td class=""(calories|label|amount)"">([a-zA-Z0-9\ &;<>=\/\""\.]*)<\/td>"))
                            {
                                switch (fact.Groups[1].Value.Trim().ToLowerInvariant())
                                {
                                    case "calories":
                                        stringBuilder.Append("Calories: " + fact.Groups[2].Value.Replace("Calories&nbsp;<span class=\"amount\">", "").Replace("</span>", "") + ", ");
                                        break;

                                    case "label":
                                        label = fact.Groups[2].Value.Trim();
                                        break;

                                    case "amount":
                                        stringBuilder.Append(label + ": " + fact.Groups[2].Value + ", ");
                                        switch (label.ToLowerInvariant())
                                        {
                                            case "protein":
                                                protein = Convert.ToDouble(fact.Groups[2].Value.Replace("mg", "").Replace("g", "").Replace("&lt;", "").Replace("&gt;", ""));
                                                break;

                                            case "total carbs.":
                                                carbs = Convert.ToDouble(fact.Groups[2].Value.Replace("mg", "").Replace("g", "").Replace("&lt;", "").Replace("&gt;", ""));
                                                break;

                                            case "total fat":
                                                fat = Convert.ToDouble(fact.Groups[2].Value.Replace("mg", "").Replace("g", "").Replace("&lt;", "").Replace("&gt;", ""));
                                                break;

                                            case "dietary fiber":
                                                fiber = Convert.ToDouble(fact.Groups[2].Value.Replace("mg", "").Replace("g", "").Replace("&lt;", "").Replace("&gt;", ""));
                                                break;
                                        }
                                        break;
                                }
                            }

                            // WW Points = (Protein/10.9375) + (Carbs/9.2105) + (Fat/3.8889) - (Fiber/12.5)
                            stringBuilder.Append("WW PointsPlus: " + Math.Round((protein / 10.9375) + (carbs / 9.2105) + (fat / 3.8889) - (fiber / 12.5), 1));
                            break;

                        case "/forecast":
                            if (body.Length < 2)
                            {
                                body = "Cincinnati, OH";
                            }

                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic dfor = JObject.Parse(httpClient.DownloadString("http://api.wunderground.com/api/" + wundergroundKey + "/forecast/q/" + body + ".json").Result);
                            if (dfor.forecast == null || dfor.forecast.txt_forecast == null)
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try \"City, ST\" or \"City, Country\" next time.";
                                break;
                            }
                            for (var ifor = 0; ifor < Enumerable.Count(dfor.forecast.txt_forecast.forecastday) - 1; ifor++)
                            {
                                stringBuilder.AppendLine(dfor.forecast.txt_forecast.forecastday[ifor].title.ToString() + ": " + dfor.forecast.txt_forecast.forecastday[ifor].fcttext.ToString());
                            }
                            break;

                        case "/help":
                            replyText = "The Great & powerful Trixie understands the following commands:\r\n" +
                                "/cat /doge /fat /forecast /help /image /imdb /google /joke /map /outside /overwatch /pony /radar /satellite /stock /stock7 /stockyear /translate /translateto /trixie /version /weather /wiki /ww";
                            /* Send this string of text to BotFather to register the bot's commands:
cat - Get a picture of a cat
doge - Dogeify a comma sep list of terms
fat - Nutrition information
forecast - Weather forecast
help - Displays help text
image - Search for an image
imdb - Search IMDB for a movie name
google - Search Google
map - Returns a location for the given search
joke - Returns a random joke from /r/jokes on Reddit
outside - Webcam image
overwatch - Overwatch Stats
pony - Ponies matching comma separated tags
radar - Weather radar
remind - Sets a reminder message after X minutes
satellite - Weather Satellite
stock - US Stock Chart (1 day)
stock7 - US Stock Chart (7 day)
stockyear - US Stock Chart (12 month)
translate - Translate to english
translateto - Translate to a given language
trixie - Wolfram Alpha logic search
version - Display version info
weather - Current weather conditions
wiki - Search Wikipedia
ww - WeightWatcher PointsPlus calc
                            */
                            break;

                        case "/image":
                        case "/img":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /image <Description of image to find>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            httpClient.AuthorizationHeader = "Basic " + bingKey;
                            dynamic dimg = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Data.ashx/Bing/Search/Image?Market=%27en-US%27&Adult=%27Moderate%27&Query=%27" + HttpUtility.UrlEncode(body) + "%27&$format=json&$top=3").Result);
                            httpClient.AuthorizationHeader = string.Empty;
                            if (dimg.d == null || dimg.d.results == null || Enumerable.Count(dimg.d.results) < 1)
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                                break;
                            }
                            var rimg = new Random();
                            var iimgmax = Enumerable.Count(dimg.d.results);
                            if (iimgmax > 3)
                            {
                                iimgmax = 3;
                            }
                            var iimg = rimg.Next(0, iimgmax);
                            string imageUrl = dimg.d.results[iimg].MediaUrl.ToString();
                            if (imageUrl.Trim().EndsWith(".gif", StringComparison.Ordinal))
                            {
                                replyDocument = dimg.d.results[iimg].MediaUrl;
                            }
                            else
                            {
                                replyImage = dimg.d.results[iimg].MediaUrl;
                            }
                            break;

                        case "/imdb":
                        case "/rt":
                        case "/movie":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /imdb <Movie Title>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);

                            // Search Bing
                            httpClient.AuthorizationHeader = "Basic " + bingKey;
                            dynamic dimdb = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Data.ashx/Bing/Search/Web?Market=%27en-US%27&Adult=%27Moderate%27&Query=%27site%3Aimdb.com%20" + HttpUtility.UrlEncode(body) + "%27&$format=json&$top=1").Result);
                            httpClient.AuthorizationHeader = string.Empty;
                            if (dimdb.d == null || dimdb.d.results == null ||
                                Enumerable.Count(dimdb.d.results) < 1)
                            {
                                replyText = "Trixie was unable to find a movie name matching:" + body;
                                break;
                            }

                            // Find correct /combined URL
                            string imdbUrl = dimdb.d.results[0].Url;
                            imdbUrl = (imdbUrl.Replace("/business", "").Replace("/combined", "").Replace("/faq", "").Replace("/goofs", "").Replace("/news", "").Replace("/parentalguide", "").Replace("/quotes", "").Replace("/ratings", "").Replace("/synopsis", "").Replace("/trivia", "") + "/combined").Replace("//combined","/combined");

                            // Scrape it
                            var imdb = httpClient.DownloadString(imdbUrl).Result.Replace("\r", "").Replace("\n", "");
                            var title = Regex.Match(imdb, @"<title>(IMDb \- )*(.*?) \(.*?</title>", RegexOptions.IgnoreCase).Groups[2].Value.Trim();
                            var year = Regex.Match(imdb, @"<title>.*?\(.*?(\d{4}).*?\).*?</title>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var rating = Regex.Match(imdb, @"<b>(\d.\d)/10</b>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var votes = Regex.Match(imdb, @">(\d+,?\d*) votes<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var plot = Regex.Match(imdb, @"Plot:</h5>.*?<div class=""info-content"">(.*?)(<a|</div)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var tagline = Regex.Match(imdb, @"Tagline:</h5>.*?<div class=""info-content"">(.*?)(<a|</div)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var poster = Regex.Match(imdb, @"<div class=""photo"">.*?<a name=""poster"".*?><img.*?src=""(.*?)"".*?</div>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            var posterFull = string.Empty;
                            if (!string.IsNullOrEmpty(poster) && poster.IndexOf("media-imdb.com", StringComparison.Ordinal) > 0)
                            {
                                poster = Regex.Replace(poster, @"_V1.*?.jpg", "_V1._SY200.jpg");
                                posterFull = Regex.Replace(poster, @"_V1.*?.jpg", "_V1._SX1280_SY1280.jpg");
                            }
                            if (title.Length < 2)
                            {
                                replyText = "Trixie was unable to find a movie name matching: " + body;
                            }
                            else
                            {
                                // Try for RT score scrape
                                httpClient.AuthorizationHeader = "Basic " + bingKey;
                                dynamic drt = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Data.ashx/Bing/Search/Web?Market=%27en-US%27&Adult=%27Moderate%27&Query=%27site%3Arottentomatoes.com%20" + HttpUtility.UrlEncode(body) + "%27&$format=json&$top=1").Result);
                                httpClient.AuthorizationHeader = string.Empty;
                                if (drt.d != null && drt.d.results != null && Enumerable.Count(drt.d.results) > 0)
                                {
                                    string rtUrl = drt.d.results[0].Url;
                                    var rt = httpClient.DownloadString(rtUrl).Result;
                                    //var rtCritic = Regex.Match(rt, @"<span class=""meter-value .*?<span>(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    var rtCritic = Regex.Match(rt, @"<span class=""meter-value superPageFontColor""><span>(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    var rtAudience = Regex.Match(rt, @"<span class=""superPageFontColor"" style=""vertical-align:top"">(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    replyText = HttpUtility.HtmlDecode(title) + " (" + year + ") - " + HttpUtility.HtmlDecode(tagline) + "\r\nIMDb: " + rating + " (" + votes + " votes) | RT critic: " + rtCritic + "% | RT audience: " + rtAudience + "\r\n" + HttpUtility.HtmlDecode(plot);
                                }
                                else
                                {
                                    var rt = httpClient.DownloadString("http://www.rottentomatoes.com/search/?search=" + HttpUtility.UrlEncode(body)).Result;
                                    //var rtCritic = Regex.Match(rt, @"<span class=""meter-value .*?<span>(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    var rtCritic = Regex.Match(rt, @"<span class=""meter-value superPageFontColor""><span>(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    var rtAudience = Regex.Match(rt, @"<span class=""superPageFontColor"" style=""vertical-align:top"">(.*?)</span>", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                    replyText = HttpUtility.HtmlDecode(title) + " (" + year + ") - " + HttpUtility.HtmlDecode(tagline) + "\r\nIMDb: " + rating + " (" + votes + " votes) | RT critic: " + rtCritic + "% | RT audience: " + rtAudience + "\r\n" + HttpUtility.HtmlDecode(plot);
                                }

                                // Remove trailing pipe that sometimes occurs
                                if (replyText.EndsWith("|"))
                                {
                                    replyText = replyText.Substring(0, replyText.Length - 2).Trim();
                                }

                                // Set referrer URI to grab IMDB poster
                                httpClient.ReferrerUri = imdbUrl;
                                replyImage = posterFull;
                                replyImageCaption = imdbUrl;
                            }
                            break;

                        case "/joke":
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic djoke = JObject.Parse(httpClient.DownloadString("https://api.reddit.com/r/jokes/top?t=day&limit=5").Result);
                            var rjoke = new Random();
                            var ijokemax = Enumerable.Count(djoke.data.children);
                            if (ijokemax > 4)
                            {
                                ijokemax = 4;
                            }
                            var ijoke = rjoke.Next(0, ijokemax);
                            replyText = djoke.data.children[ijoke].data.title.ToString() + " " + djoke.data.children[ijoke].data.selftext.ToString();
                            break;

                        case "/map":
                        case "/location":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /map <Search Text>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic dmap = JObject.Parse(httpClient.DownloadString("http://maps.googleapis.com/maps/api/geocode/json?address=" + HttpUtility.UrlEncode(body)).Result);
                            if (dmap == null || dmap.results == null || Enumerable.Count(dmap.results) < 1)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                            }
                            else
                            {
                                await bot.SendLocationAsync(update.Message.Chat.Id, (float)dmap.results[0].geometry.location.lat, (float)dmap.results[0].geometry.location.lng);
                            }
                            break;

                        case "/google":
                        case "/bing":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /google <Search Text>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            httpClient.AuthorizationHeader = "Basic " + bingKey;
                            dynamic dgoog = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Data.ashx/Bing/Search/Web?Market=%27en-US%27&Adult=%27Moderate%27&Query=%27" + HttpUtility.UrlEncode(body) + "%27&$format=json&$top=1").Result);
                            httpClient.AuthorizationHeader = string.Empty;
                            if (dgoog.d == null || dgoog.d.results == null || Enumerable.Count(dgoog.d.results) < 1)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                            }
                            else
                            {
                                var rgoog = new Random();
                                var igoog = rgoog.Next(0, Enumerable.Count(dgoog.d.results));
                                replyText = HttpUtility.HtmlDecode(dgoog.d.results[igoog].Title.ToString()) + " | " + HttpUtility.HtmlDecode(dgoog.d.results[igoog].Description.ToString()) + "\r\n" + dgoog.d.results[igoog].Url;
                            }
                            break;

                        case "/outside":
                            if (body.Length < 2)
                            {
                                body = "Cincinnati, OH";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic dout = JObject.Parse(httpClient.DownloadString("http://api.wunderground.com/api/" + wundergroundKey + "/webcams/q/" + body + ".json").Result);
                            if (dout.webcams == null)
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try \"City, ST\" or \"City, Country\" next time.";
                                break;
                            }
                            var rout = new Random();
                            var iout = rout.Next(0, Enumerable.Count(dout.webcams));
                            replyImage = dout.webcams[iout].CURRENTIMAGEURL;
                            replyImageCaption = dout.webcams[iout].organization + " " + dout.webcams[iout].neighborhood + " " + dout.webcams[iout].city + ", " + dout.webcams[iout].state + "\r\n" + dout.webcams[iout].CAMURL;
                            break;

                        case "/overwatch":
                            if (body.Length < 2)
                            {
                                replyText = "Usage: /overwatch <Battletag with no spaces>  eg: /overwatch SniperFox#1513";
                                break;
                            }
                            var ow = new OverwatchPlayer(body, Platform.pc, Region.us);
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            ow.UpdateStats().GetAwaiter().GetResult();
                            if (ow.CompetitiveStats.AllHeroes != null)
                            { 
                                replyTextMarkdown = "*Competitive Play - Rank " + ow.CompetitiveRank + "*\r\n" + ow.CompetitiveStats.AllHeroes.Game.GamesWon + " wins, " + (ow.CompetitiveStats.AllHeroes.Game.GamesPlayed - ow.CompetitiveStats.AllHeroes.Game.GamesWon) + " losses " +
                                    "(" + Math.Round((ow.CompetitiveStats.AllHeroes.Game.GamesWon / ow.CompetitiveStats.AllHeroes.Game.GamesPlayed) * 100, 2) + "%) " +
                                    "over " + Math.Round(ow.CompetitiveStats.AllHeroes.Game.TimePlayed.TotalHours / 24, 2) + " days played.\r\n" +
                                    ow.CompetitiveStats.AllHeroes.Combat.Eliminations + " eliminations, " + ow.CompetitiveStats.AllHeroes.Deaths.Deaths + " deaths, " + Math.Round(ow.CompetitiveStats.AllHeroes.Combat.Eliminations / ow.CompetitiveStats.AllHeroes.Deaths.Deaths, 2) + " eliminations per death.\r\n" +
                                    ow.CompetitiveStats.AllHeroes.MatchAwards.Cards + " cards, " + ow.CompetitiveStats.AllHeroes.MatchAwards.MedalsGold + " gold, " + ow.CompetitiveStats.AllHeroes.MatchAwards.MedalsSilver + " silver, and " + ow.CompetitiveStats.AllHeroes.MatchAwards.MedalsGold + " bronze medals.\r\n";
                            }
                            replyTextMarkdown += "*Quick Filthy Casual Play - Level " + ow.PlayerLevel + "*\r\n" + ow.CasualStats.AllHeroes.Game.GamesWon + " wins, " + (ow.CasualStats.AllHeroes.Game.GamesPlayed - ow.CasualStats.AllHeroes.Game.GamesWon) + " losses " +
                                "(" + Math.Round((ow.CasualStats.AllHeroes.Game.GamesWon / ow.CasualStats.AllHeroes.Game.GamesPlayed) * 100, 2) + "%) " +
                                "over " + Math.Round(ow.CasualStats.AllHeroes.Game.TimePlayed.TotalHours / 24, 2) + " days played.\r\n" +
                                ow.CasualStats.AllHeroes.Combat.Eliminations + " eliminations, " + ow.CasualStats.AllHeroes.Deaths.Deaths + " deaths, " + Math.Round(ow.CasualStats.AllHeroes.Combat.Eliminations / ow.CasualStats.AllHeroes.Deaths.Deaths, 2) + " eliminations per death.\r\n" +
                                ow.CasualStats.AllHeroes.MatchAwards.Cards + " cards, " + ow.CasualStats.AllHeroes.MatchAwards.MedalsGold + " gold, " + ow.CasualStats.AllHeroes.MatchAwards.MedalsSilver + " silver, and " + ow.CasualStats.AllHeroes.MatchAwards.MedalsGold + " bronze medals.\r\n" +
                                ow.ProfileURL.Replace("/en-gb/", "/en-us/");
                            break;

                        case "/pony":
                        case "/pone":
                            if (body.Length < 2)
                            {
                                replyText = "I like ponies too.  What kind of pony would you like me to search for?";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic dpony = JObject.Parse(httpClient.DownloadString("https://derpibooru.org/search.json?q=safe," + HttpUtility.UrlEncode(body)).Result);
                            if (dpony.search == null)
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.";
                                break;
                            }
                            var rpony = new Random();
                            var iponymax = Enumerable.Count(dpony.search);
                            if (iponymax < 1)
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.";
                                break;
                            }
                            if (iponymax > 5)
                            {
                                iponymax = 5;
                            }
                            var ipony = rpony.Next(0, iponymax);
                            replyImage = "https:" + dpony.search[ipony].representations.large;
                            replyImageCaption = "https:" + dpony.search[ipony].image;
                            break;

                        case "/radar":
                            if (body.Length < 2)
                            { 
                                body = "Cincinnati, OH";
                            }
                            replyDocument = "http://api.wunderground.com/api/" + wundergroundKey + "/animatedradar/q/" + body + ".gif?newmaps=1&num=15&width=1024&height=1024";
                            break;

                        case "/remind":
                        case "/remindme":
                        case "/reminder":
                            if (body.Length < 2 || !body.Contains(" "))
                            { 
                                replyText = "Usage: /remind <minutes> <Reminder Text>";
                            }
                            else
                            {
                                var delayMinutesString = body.Substring(0, body.IndexOf(" ", StringComparison.Ordinal));
                                int delayMinutes;
                                if (int.TryParse(delayMinutesString, out delayMinutes))
                                {
                                    if (delayMinutes > 1440 || delayMinutes < 1)
                                    {
                                        replyText = "Reminders can not be set for longer than 1440 minutes (24 hours).";
                                    }
                                    else
                                    {
                                        DelayedMessage(bot, update.Message.Chat.Id, "@" + update.Message.From.Username + " Reminder: " + body.Substring(delayMinutesString.Length).Trim(), delayMinutes);
                                        replyText = "OK, I'll remind you at " + DateTime.Now.AddMinutes(delayMinutes).ToString("MM/dd/yyyy HH:mm") + " (US Eastern)";
                                    }
                                }
                                else
                                {
                                    replyText = "Usage: /remind <minutes as positive integer> <Reminder Text>";
                                }
                            }
                            break;

                        case "/s":
                            if (body.Length < 2 || update.Message.ReplyToMessage == null)
                            { 
                                replyText = "This must be done as a reply in the format /s/replace this/replace with/";
                            }
                            else
                            {
                                var sed = body.Split('/');
                                if (sed.Length != 4)
                                    replyText = "The only sed command parsed is /s/replace this/replace with/";
                                else
                                {
                                    replyTextMarkdown = "*" + update.Message.ReplyToMessage.From.FirstName + " " + update.Message.ReplyToMessage.From.LastName + "* \r\n" + update.Message.ReplyToMessage.Text.Replace(sed[1], sed[2]);
                                }
                            }
                            break;

                        case "/satellite":
                            if (body.Length < 2)
                            { 
                                body = "Cincinnati, OH";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic rsat = JObject.Parse(httpClient.DownloadString("http://api.wunderground.com/api/" + wundergroundKey + "/satellite/q/" + body + ".json").Result);
                            if (rsat.satellite == null || rsat.satellite.image_url == null)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try \"City, ST\" or \"City, Country\" next time.";
                            }
                            else
                            {
                                string saturl = rsat.satellite.image_url;
                                replyImage = saturl.Replace("height=300", "height=1280").Replace("width=300", "width=1280").Replace("radius=75", "radius=250");
                                replyImageCaption = body + " as of " + DateTime.Now.ToString("MM/dd/yyy HH:mm:ss");
                            }
                            break;

                        case "/stock":
                            if (body.Length < 1 || body.Length > 5)
                            { 
                                body = "^DJI";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            replyImage = "https://chart.yahoo.com/t?s=" + body + "&lang=en-US&region=US&width=1200&height=765";
                            replyImageCaption = "Chart for " + body + " as of " + DateTime.Now.ToString("MM/dd/yyy HH:mm:ss");
                            break;

                        case "/stock5":
                            if (body.Length < 1 || body.Length > 5)
                            { 
                                body = "^DJI";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            replyImage = "https://chart.yahoo.com/w?s=" + body + "&lang=en-US&region=US&width=1200&height=765";
                            replyImageCaption = "5 day chart for " + body + " as of " + DateTime.Now.ToString("MM/dd/yyy HH:mm:ss");
                            break;

                        case "/stockyear":
                            if (body.Length < 1 || body.Length > 5)
                            { 
                                body = "^DJI";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            replyImage = "https://chart.yahoo.com/c/1y/" + body;
                            replyImageCaption = "Year chart for " + body + " as of " + DateTime.Now.ToString("MM/dd/yyy HH:mm:ss");
                            break;

                        case "/translateto":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /translateto <Language Code> <English Text>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            var lang = body.Substring(0, body.IndexOf(" ", StringComparison.Ordinal));
                            var query = body.Substring(body.IndexOf(" ", StringComparison.Ordinal) + 1);
                            httpClient.AuthorizationHeader = "Basic " + bingKey;
                            dynamic dtto = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Bing/MicrosoftTranslator/v1/Translate?Text=%27" + HttpUtility.UrlEncode(query) + "%27&To=%27" + lang + "%27&$format=json").Result);
                            httpClient.AuthorizationHeader = string.Empty;
                            if (dtto.d == null || dtto.d.results == null || Enumerable.Count(dtto.d.results) < 1 || dtto.d.results[0].Text == null)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                            }
                            else
                            { 
                                replyText = dtto.d.results[0].Text;
                            }
                            break;

                        case "/translate":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /translate <Foreign Text>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            httpClient.AuthorizationHeader = "Basic " + bingKey;
                            dynamic dtrans = JObject.Parse(httpClient.DownloadString("https://api.datamarket.azure.com/Bing/MicrosoftTranslator/v1/Translate?Text=%27" + HttpUtility.UrlEncode(body) + "%27&To=%27en%27&$format=json").Result);
                            httpClient.AuthorizationHeader = string.Empty;
                            if (dtrans.d == null || dtrans.d.results == null || Enumerable.Count(dtrans.d.results) < 1 || dtrans.d.results[0].Text == null)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                            }
                            else
                            { 
                                replyText = dtrans.d.results[0].Text;
                            }
                            break;

                        case "/trixie":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /trixie <Query>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(httpClient.DownloadString("http://api.wolframalpha.com/v2/query?input=" + HttpUtility.UrlEncode(body) + "&appid=" + wolframAppId).Result);
                            var queryResult = xmlDoc.SelectSingleNode("/queryresult");
                            if (queryResult == null || queryResult?.Attributes == null || queryResult.Attributes?["success"] == null || queryResult.Attributes?["success"].Value != "true")
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try harder next time.";
                                break;
                            }
                            var pods = queryResult.SelectNodes("pod");
                            foreach (var pod in pods.Cast<XmlNode>().Where(pod => pod.Attributes != null && pod.Attributes["title"].Value != "Input interpretation"))
                            {
                                // Parse Image
                                if (replyImage == string.Empty)
                                {
                                    try
                                    {
                                        var subPodImage = pod.SelectSingleNode("subpod/img");
                                        if (subPodImage.Attributes != null)
                                        {
                                            replyImage = subPodImage.Attributes?["src"].Value.Trim();
                                        }
                                    }
                                    catch
                                    {
                                        // Don't care
                                    }
                                }

                                // Parse plain text
                                try
                                {
                                    var subPodPlainText = pod.SelectSingleNode("subpod/plaintext");
                                    if (subPodPlainText == null || subPodPlainText.InnerText.Trim().Length <= 0) continue;
                                    var podName = pod.Attributes?["title"].Value.Trim();
                                    if (podName == "Response" || podName == "Result")
                                    { 
                                        stringBuilder.AppendLine(subPodPlainText.InnerText);
                                    }
                                    else
                                    { 
                                        stringBuilder.AppendLine(podName + ": " + subPodPlainText.InnerText);
                                    }
                                }
                                catch
                                {
                                    // Don't care
                                }
                            }
                            break;

                        case "/version":
                        case "/about":
                            replyText = "Trixie Is Best Pony Bot\r\nRelease fourty-two\r\nBy http://scottrfrost.github.io";
                            break;

                        case "/weather":
                            if (body.Length < 2)
                            { 
                                body = "Cincinnati, OH";
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            dynamic dwthr = JObject.Parse(httpClient.DownloadString("http://api.wunderground.com/api/" + wundergroundKey + "/conditions/q/" + body + ".json").Result);
                            if (dwthr.current_observation == null)
                            { 
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.  Try \"City, ST\" or \"City, Country\" next time.";
                            }
                            else
                            { 
                                replyText =
                                    dwthr.current_observation.display_location.full + " Conditions: " +
                                    dwthr.current_observation.weather +
                                    " Wind: " + dwthr.current_observation.wind_string +
                                    " Temp: " + dwthr.current_observation.temperature_string + " Feels Like: " +
                                    dwthr.current_observation.feelslike_string;
                            }
                            break;

                        case "/wiki":
                            if (body == string.Empty)
                            {
                                replyText = "Usage: /wiki <Query>";
                                break;
                            }
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                            var dwiki = JObject.Parse(httpClient.DownloadString("https://en.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro=&explaintext=&redirects=true&titles=" + HttpUtility.UrlEncode(body)).Result);
                            if (dwiki["query"].HasValues && dwiki["query"]["pages"].HasValues)
                            {
                                var page = dwiki["query"]["pages"].First().First();
                                if (Convert.ToString(page["pageid"]).Length > 0)
                                    replyTextMarkdown = "*" + page["title"] + "*\r\n" + page["extract"] + "\r\n" + "https://en.wikipedia.org/?curid=" + page["pageid"];
                                else
                                {
                                    replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.";
                                }
                            }
                            else
                            {
                                replyText = "You have disappointed Trixie.  \"" + body + "\" is bullshit and you know it.";
                                
                            }
                            break;

                        case "/ww":
                            var split = body.Split(' ');
                            if (split.Length != 4)
                            {
                                replyText = "Usage: /ww <carbs> <fat> <fiber> <protein>";
                                break;
                            }
                            try
                            {
                                var wwcarbs = Convert.ToDouble(split[0]);
                                var wwfat = Convert.ToDouble(split[1]);
                                var wwfiber = Convert.ToDouble(split[2]);
                                var wwprotein = Convert.ToDouble(split[3]);
                                replyText = "WW PointsPlus value for " + wwcarbs + "g carbs, " + wwfat + "g fat, " + wwfiber + "g fiber, " + wwprotein + "g protein is: " + Math.Round((wwprotein / 10.9375) + (wwcarbs / 9.2105) + (wwfat / 3.8889) - (wwfiber / 12.5), 1);
                            }
                            catch
                            {
                                replyText = "Trixie is disappointed that you used /ww incorrectly. The correct usage is: /ww <carbs> <fat> <fiber> <protein>";
                            }
                            break;
                    }

                    // Output
                    replyText += stringBuilder.ToString();
                    if (!string.IsNullOrEmpty(replyText))
                    {
                        Console.WriteLine(update.Message.Chat.Id + " > " + replyText);
                        await bot.SendTextMessageAsync(update.Message.Chat.Id, replyText);
                    }
                    if (!string.IsNullOrEmpty(replyTextMarkdown))
                    {
                        Console.WriteLine(update.Message.Chat.Id + " > " + replyTextMarkdown);
                        await bot.SendTextMessageAsync(update.Message.Chat.Id, replyTextMarkdown, false, false, 0, null, ParseMode.Markdown);
                    }

                    if (!string.IsNullOrEmpty(replyImage) && replyImage.Length > 5)
                    {
                        Console.WriteLine(update.Message.Chat.Id + " > " + replyImage);
                        await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.Typing);
                        try
                        {
                            var stream = httpClient.DownloadData(replyImage).Result;
                            var extension = ".jpg";
                            if (replyImage.Contains(".gif") || replyImage.Contains("image/gif"))
                            { 
                                extension = ".gif";
                            }
                            else if (replyImage.Contains(".png") || replyImage.Contains("image/png"))
                            { 
                                extension = ".png";
                            }
                            else if (replyImage.Contains(".tif"))
                            { 
                                extension = ".tif";
                            }
                            else if (replyImage.Contains(".bmp"))
                            { 
                                extension = ".bmp";
                            }
                            var photo = new FileToSend("Photo" + extension, stream);
                            await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.UploadPhoto);
                            if (extension == ".gif")
                            { 
                                await bot.SendDocumentAsync(update.Message.Chat.Id, photo);
                            }
                            else
                            { 
                                await bot.SendPhotoAsync(update.Message.Chat.Id, photo, replyImageCaption == string.Empty ? replyImage : replyImageCaption);
                            }
                        }
                        catch (System.Net.Http.HttpRequestException ex)
                        {
                            Console.WriteLine("Unable to download " + ex.HResult + " " + ex.Message);
                            await bot.SendTextMessageAsync(update.Message.Chat.Id, replyImage);
                        }
                        catch (System.Net.WebException ex)
                        {
                            Console.WriteLine("Unable to download " + ex.HResult + " " + ex.Message);
                            await bot.SendTextMessageAsync(update.Message.Chat.Id, replyImage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(replyImage + " Threw: " + ex.Message);
                            await bot.SendTextMessageAsync(update.Message.Chat.Id, replyImage);
                        }
                    }

                    if (!string.IsNullOrEmpty(replyDocument) && replyDocument.Length > 5)
                    {
                        Console.WriteLine(update.Message.Chat.Id + " > " + replyDocument);
                        await bot.SendChatActionAsync(update.Message.Chat.Id, ChatAction.UploadDocument);
                        var stream = httpClient.DownloadData(replyDocument).Result;
                        var filename = replyDocument.Substring(replyDocument.LastIndexOf("/", StringComparison.Ordinal));
                        var document = new FileToSend(filename, stream);
                        await bot.SendDocumentAsync(update.Message.Chat.Id, document);
                    }
                }
            }
            catch (System.Net.WebException ex)
            {
                Console.WriteLine("Unable to download " + ex.HResult + " " + ex.Message);
                await bot.SendTextMessageAsync(update.Message.Chat.Id, "The Great & Powerful Trixie got bored while waiting for that to download.  Try later.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR - " + ex);
            }
        }

        static async void DelayedMessage(TelegramBotClient bot, long chatId, string message, int minutesToWait)
        {
            await Task.Delay(minutesToWait * 60000);
            await bot.SendTextMessageAsync(chatId, message);
        }
    }
}