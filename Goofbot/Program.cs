using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;

namespace Goofbot
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private const string tokenRequestUrl = "https://id.twitch.tv/oauth2/token";
        private const string colorNamesRequestUrl = "https://api.color.pizza/v1/";
        private const string tokensFile = "Stuff\\tokens.json";
        private const string tokenOptsFile = "Stuff\\token_opts.json";
        private const string botAccount = "goofbotthebot";
        private const string channelToJoin = "goofballthecat";

        public const string colorNamesFile = "Stuff\\color_names.json";
        public const string guysFolder = "Stuff\\Guys";


        static async Task Main(string[] args)
        {
            string tokensString = await RequestTokens();
            File.WriteAllText(tokensFile, tokensString);

            dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
            string accessToken = Convert.ToString(tokensObject.access_token);

            MagickNET.Initialize();

            Directory.CreateDirectory(guysFolder);

            /*string colorNamesString = await RequestColorNames();
            File.WriteAllText(colorNamesFile, colorNamesString);*/

            Bot bot = new Bot(botAccount, channelToJoin, accessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        static async Task<string> RequestColorNames()
        {
            var response = await client.GetAsync(colorNamesRequestUrl);
            return await response.Content.ReadAsStringAsync();
        }

        static async Task<string> RequestTokens()
        {
            dynamic tokenOpts = ParseJsonFile(tokenOptsFile);
            dynamic tokens = ParseJsonFile(tokensFile);

            var values = new Dictionary<string, string>
            {
                { "refresh_token", Convert.ToString(tokens.refresh_token) },
                { "client_id", Convert.ToString(tokenOpts.client_id) },
                { "client_secret", Convert.ToString(tokenOpts.client_secret) },
                { "grant_type", Convert.ToString(tokenOpts.grant_type) },
            };
            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(tokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }
    }
}