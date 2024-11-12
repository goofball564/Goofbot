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
        public const string ColorNamesFile = "Stuff\\color_names.json";
        public const string GuysFolder = "Stuff\\Guys";

        private const string TokenRequestUrl = "https://id.twitch.tv/oauth2/token";
        private const string RedirectUrl = "http://localhost:3000/";

        private const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";
        
        private const string TokensFile = "Stuff\\tokens.json";
        private const string ClientInfoFile = "Stuff\\client_info.json";
        private const string BotAccount = "goofbotthebot";
        private const string ChannelToJoin = "goofballthecat";

        private static readonly List<string> s_scopes = new List<string> { "chat:read", "chat:edit" };
        private static readonly dynamic s_clientInfo = ParseJsonFile(ClientInfoFile);
        private static readonly HttpClient s_httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            string clientId = s_clientInfo.client_id;
            var server = new WebServer(RedirectUrl);
            string url = getAuthorizationCodeUrl(clientId, RedirectUrl, s_scopes);
            Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", url); // me-specific hack
            string code = await server.Listen();
            
            string tokensString = await RequestTokensWithAuthorizationCode(code);
            File.WriteAllText(TokensFile, tokensString);
            dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
            string accessToken = Convert.ToString(tokensObject.access_token);

            MagickNET.Initialize();
            Directory.CreateDirectory(GuysFolder);

            /*string colorNamesString = await RequestColorNames();
            File.WriteAllText(colorNamesFile, colorNamesString);*/

            Bot bot = new Bot(BotAccount, ChannelToJoin, accessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        private static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);

            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   "response_type=code&" +
                   $"scope={scopesStr}";
        }

        static async Task<string> RequestColorNames()
        {
            var response = await s_httpClient.GetAsync(ColorNamesRequestUrl);
            return await response.Content.ReadAsStringAsync();
        }

        static async Task<string> RequestTokensWithRefreshToken()
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
        }

        static async Task<string> RequestTokensWithAuthorizationCode(string code)
        {
            dynamic client = ParseJsonFile(ClientInfoFile);
            dynamic tokens = ParseJsonFile(TokensFile);

            var values = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", Convert.ToString(client.client_id) },
                { "client_secret", Convert.ToString(client.client_secret) },
                { "grant_type", "authorization_code" },
                { "redirect_uri", RedirectUrl }
            };
            var content = new FormUrlEncodedContent(values);
            var response = await Program.s_httpClient.PostAsync(TokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }
    }
}