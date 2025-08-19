using Goofbot.Modules;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using WindowsAPICodePack.Dialogs;

namespace Goofbot
{
    class Program
    {
        public const string TwitchBotUsername = "goofbotthebot";
        public const string TwitchChannelUsername = "goofballthecat";

        private static readonly string s_goofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

        public static TwitchAuthenticationManager TwitchAuthenticationManager { get; private set; }
        public static ColorDictionary ColorDictionary { get; private set; }
        public static TwitchAPI TwitchAPI { get; private set; } = new();
        public static TwitchClient TwitchClient { get; private set; } = new();
        public static string StuffFolder { get; private set; }

        public static async Task Main(string[] args)
        {
            // Get location of bot data folder
            string stuffLocationFile = Path.Join(s_goofbotAppDataFolder, "stufflocation.txt");
            StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

            // Create color dictionary
            string colorNamesFile = Path.Join(StuffFolder, "color_names.json");
            ColorDictionary = new(colorNamesFile);
            Task colorDictionaryTask = Task.Run(async () => { await ColorDictionary.Initialize(); });

            // initialize TwitchClient and TwitchAPI, authenticate with twitch
            string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
            dynamic twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);
            string clientID = twitchAppCredentials.client_id;
            string clientSecret = twitchAppCredentials.client_secret;
            TwitchAuthenticationManager = new(clientID, clientSecret, TwitchClient, TwitchAPI);
            Task authenticationManagerInitializeTask = TwitchAuthenticationManager.Initialize();

            // initialize magick.net
            MagickNET.Initialize();

            await colorDictionaryTask;
            await authenticationManagerInitializeTask;

            TwitchClient.OnLog += Client_OnLog;
            TwitchClient.OnJoinedChannel += Client_OnJoinedChannel;
            TwitchClient.OnMessageReceived += Client_OnMessageReceived;
            TwitchClient.OnConnected += Client_OnConnected;
            TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
            TwitchClient.Connect();

            Bot bot = new Bot(TwitchBotUsername, TwitchChannelUsername, TwitchAuthenticationManager._twitchBotAccessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }

        private static void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private static void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }

        private static void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {

        }

        private static void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            // _commandParsingModule.ParseMessageForCommand(e);
        }

        private static void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {

        }
    }
}
