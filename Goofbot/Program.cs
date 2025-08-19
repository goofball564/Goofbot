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

        private static BlueGuyModule _blueGuyModule;
        private static CommandParsingModule _commandParsingModule;
        private static SpotifyModule _spotifyModule;
        private static SoundAlertModule _soundAlertModule;

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

            await authenticationManagerInitializeTask;

            TwitchClient.OnLog += Client_OnLog;
            TwitchClient.OnConnected += Client_OnConnected;
            TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
            TwitchClient.Connect();

            _spotifyModule = new("SpotifyModule", TwitchClient, TwitchAPI);
            Task spotifyModuleInitializeTask = _spotifyModule.Initialize();

            _soundAlertModule = new();

            await colorDictionaryTask;
            _blueGuyModule = new("BlueGuyModule", TwitchClient, TwitchAPI);

            await spotifyModuleInitializeTask;
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

        public static string ParseMessageForCommand(OnMessageReceivedArgs messageArgs, out string commandArgs)
        {
            DateTime invocationTime = DateTime.UtcNow;
            string trimmedMessage = messageArgs.ChatMessage.Message.Trim();
            int indexOfSpace = trimmedMessage.IndexOf(' ');

            string command;

            if (indexOfSpace != -1)
            {
                command = trimmedMessage.Substring(0, indexOfSpace);
                commandArgs = trimmedMessage.Substring(indexOfSpace + 1);
            }
            else
            {
                command = trimmedMessage;
                commandArgs = "";
            }

            commandArgs = commandArgs.Trim();
            return command.ToLowerInvariant();

        }

        private static void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private static void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }

        private static void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {

        }
    }
}
