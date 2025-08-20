using AngouriMath;
using Goofbot.Modules;
using Goofbot.Utils;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Goofbot
{
    class Program
    {
        public const string TwitchBotUsername = "goofbotthebot";
        public const string TwitchChannelUsername = "goofballthecat";

        private static readonly CommandDictionary s_commandDictionary = new();
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
            Task colorDictionaryTask = Task.Run(() => { ColorDictionary.Initialize(); });

            // initialize TwitchClient and TwitchAPI, authenticate with twitch
            string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
            dynamic twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);
            string clientID = twitchAppCredentials.client_id;
            string clientSecret = twitchAppCredentials.client_secret;
            TwitchAuthenticationManager = new(clientID, clientSecret, TwitchClient, TwitchAPI);
            Task authenticationManagerInitializeTask = TwitchAuthenticationManager.Initialize();

            SpotifyModule spotifyModule = new("SpotifyModule", s_commandDictionary);
            Task spotifyModuleInitializeTask = spotifyModule.Initialize();

            MagickNET.Initialize();

            TwitchClient.OnLog += Client_OnLog;
            TwitchClient.OnConnected += Client_OnConnected;
            TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
            TwitchClient.OnMessageReceived += Client_OnMessageReceived;

            SoundAlertModule soundAlertModule = new();
            MiscCommandsModule miscCommandsModule = new("MiscCommandsModule", s_commandDictionary);

            await authenticationManagerInitializeTask;
            await spotifyModuleInitializeTask;
            await colorDictionaryTask;
            BlueGuyModule blueGuyModule = new("BlueGuyModule", s_commandDictionary);
            TwitchClient.Connect();
            while (true)
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

        public static string ReverseString(string str)
        {
            char[] charArray = str.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        private static void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private static void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            TwitchClient.SendMessage(TwitchChannelUsername, "Goofbot is activated and at your service MrDestructoid");
        }

        private static async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string message = "";
            string commandName = ParseMessageForCommand(e, out string commandArgs);

            Command command;
            if (s_commandDictionary.TryGetCommand(commandName, out command))
            {
                message = await command.ExecuteCommandAsync(commandArgs, e);
            }
            else
            {
                string commandNameReversed = commandName[0] + ReverseString(commandName.Substring(1));

                if (s_commandDictionary.TryGetCommand(commandNameReversed, out command))
                {
                    string[] commandArgsArray = commandArgs.Split(" ");
                    for (int i = 0; i < commandArgsArray.Length; i++)
                    {
                        commandArgsArray[i] = ReverseString(commandArgsArray[i]);
                    }
                    string commandArgsReversed = String.Join(" ", commandArgsArray);

                    message = await command.ExecuteCommandAsync(commandArgsReversed, e);
                    message = ReverseString(message);
                }           
            }
            
            

            if (!message.Equals(""))
            {
                TwitchClient.SendMessage(TwitchChannelUsername, message);
            }
            else
            {
                try
                {
                    Entity expr = e.ChatMessage.Message;
                    if (expr.EvaluableNumerical)
                    {
                        var eval = expr.EvalNumerical();
                        if (eval is not Entity.Number.Rational)
                        {
                            message = String.Format("{0:F7}", (double)eval);
                        }
                        else
                        {
                            message = eval.ToString();
                        }

                        TwitchClient.SendMessage(TwitchChannelUsername, message);
                    }
                }
                catch
                {

                }
                
            }
        }

        private static void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {

        }
    }
}
