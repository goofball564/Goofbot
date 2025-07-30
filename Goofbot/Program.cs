using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;
using System.Diagnostics;

namespace Goofbot
{
    class Program
    {
        private static string clientId;
        public static string ClientId { get { return clientId; } }

        private static string accessToken;
        public static string AccessToken { get { return accessToken; } }

        public const string GuysFolder = "Stuff\\Guys";
        public const string ColorNamesFile = "Stuff\\color_names.json";
        public const string ClientInfoFile = "Stuff\\client_info.json";

        public const string RedirectUrl = "http://localhost:3000/";
        public const string AuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
        public const string TokenRequestUrl = "https://id.twitch.tv/oauth2/token";

        public const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";
        
        private const string BotAccount = "goofbotthebot";
        private const string ChannelToJoin = "goofballthecat";

        private static readonly List<string> s_scopes = new List<string> { "chat:read", "chat:edit", "channel:read:redemptions" };
        private static readonly dynamic s_clientInfo = ParseJsonFile(ClientInfoFile);
        private static readonly HttpClient s_httpClient = new();

        public static async Task Main(string[] args)
        {
            clientId = s_clientInfo.client_id;
            string code = await RequestTwitchAuthorizationCode();
            
            string tokensString = await RequestTwitchTokens(code);
            dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
            accessToken = Convert.ToString(tokensObject.access_token);

            MagickNET.Initialize();
            Directory.CreateDirectory(GuysFolder);

            /*string colorNamesString = await RequestColorNames();
            File.WriteAllText(ColorNamesFile, colorNamesString);*/

            Bot bot = new Bot(BotAccount, ChannelToJoin, accessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        public static async Task<string> RequestTwitchAuthorizationCode()
        {
            var server = new WebServer(RedirectUrl);
            string url = GetTwitchAuthorizationCodeRequestUrl();
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return await server.Listen();
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }

        public static async Task<string> RequestColorNames()
        {
            var response = await s_httpClient.GetAsync(ColorNamesRequestUrl);
            return await response.Content.ReadAsStringAsync();
        }

        /*private static async Task<string> RequestTokensWithRefreshToken()
        {
            dynamic client = ParseJsonFile(ClientInfoFile);
            dynamic tokens = ParseJsonFile(TokensFile);

            var values = new Dictionary<string, string>
            {
                { "refresh_token", Convert.ToString(tokens.refresh_token) },
                { "client_id", Convert.ToString(client.client_id) },
                { "client_secret", Convert.ToString(client.client_secret) },
                { "grant_type", "refresh_token" },
            };
            var content = new FormUrlEncodedContent(values);
            var response = await Program.s_httpClient.PostAsync(TokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }*/

        public static async Task<string> RequestTwitchTokens(string twitchAuthorizationCode)
        {
            var values = new Dictionary<string, string>
            {
                { "code", twitchAuthorizationCode },
                { "client_id", Convert.ToString(s_clientInfo.client_id) },
                { "client_secret", Convert.ToString(s_clientInfo.client_secret) },
                { "grant_type", "authorization_code" },
                { "redirect_uri", RedirectUrl }
            };
            var content = new FormUrlEncodedContent(values);
            var response = await s_httpClient.PostAsync(TokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        public static string GetTwitchAuthorizationCodeRequestUrl()
        {
            return $"{AuthorizationCodeRequestUrlBase}?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(RedirectUrl)}&" +
                   "response_type=code&" +
                   $"scope={String.Join('+', s_scopes)}";
        }
    }
}
