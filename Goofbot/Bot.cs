using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Goofbot.Modules;
using System.Threading;

namespace Goofbot
{
    internal class Bot
    {
        private TwitchClient Client;

        private ChatInteractionModule ChatInteractionModule;
        private PipeServerModule PipeServerModule;
        private BlueGuyModule BlueGuyModule;
        private CommandParsingModule CommandParsingModule;
        private ColorDictionary ColorDictionary;

        // private Task WaitTask;

        private string Channel;

        private const string colorNamesFile = "Stuff\\color_names.json";

        // react to game or livesplit
        // track stats?
        // FrankerZ and Deadlole
        // react to messages (on received)
        // read and write game memory
        // track internal state (count things?)
        // chat commands
        // pure text commands
        // arguments?
        // special programmed commands
        // BLUE GUY
        // chatter greetings
        // timers
        // cycle through list?
        // any configuration?
        // interface to easily modify commands?
        // 


        public Bot(string botAccount, string channelToJoin, string accessToken)
        {
            Channel = channelToJoin;

            ConnectionCredentials credentials = new ConnectionCredentials(botAccount, accessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            var customClient = new WebSocketClient(clientOptions);
            Client = new TwitchClient(customClient);
            Client.Initialize(credentials, channelToJoin);
            Client.OnLog += Client_OnLog;
            Client.OnJoinedChannel += Client_OnJoinedChannel;
            Client.OnMessageReceived += Client_OnMessageReceived;
            Client.OnConnected += Client_OnConnected;
            Client.Connect();

            ColorDictionary = new ColorDictionary(colorNamesFile);

            CommandParsingModule = new CommandParsingModule();
            PipeServerModule = new PipeServerModule();
            BlueGuyModule = new BlueGuyModule(ColorDictionary);

            CommandParsingModule.GuyCommand += BlueGuyModule.OnGuyCommand;
            CommandParsingModule.TimeoutNotElapsed += CommandParsingModule_OnTimeoutNotElapsed;

            BlueGuyModule.ColorChange += BlueGuyModule_OnColorChange;
            BlueGuyModule.UnknownColor += BlueGuyModule_OnUnknownColor;
            BlueGuyModule.NoArgument += BlueGuyModule_OnNoArgument;
            BlueGuyModule.RandomColor += BlueGuyModule_OnRandomColor;
            BlueGuyModule.SameColor += BlueGuyModule_OnSameColor;

            PipeServerModule.RunStart += PiperServerModule_OnRunStart;
            PipeServerModule.RunReset += PipeServerModule_OnRunReset;
            // PipeServerModule.RunGold += PipeServerModule_OnRunGold;

            PipeServerModule.Start();
            Console.WriteLine("EVERYTHING HAS STARTED :)");
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            // Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Client.SendMessage(Channel, "Goofbot is activated and at your service MrDestructoid");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            CommandParsingModule.ParseMessageForCommand(e);
        }

        private void CommandParsingModule_OnTimeoutNotElapsed(object sender, TimeSpan timeUntilTimeoutElapses)
        {
            Client.SendMessage(Channel, String.Format("Please wait {0} minutes and {1} seconds, then try again.", timeUntilTimeoutElapses.Minutes, timeUntilTimeoutElapses.Seconds));
        }

        private void PiperServerModule_OnRunStart(object sender, int runCount)
        {
            Client.SendMessage(Channel, String.Format("Run {0}! dinkDonk Give it up for run {1}! dinkDonk", runCount, runCount));
        }

        private void PipeServerModule_OnRunReset(object sender, int runCount)
        {
            
        }

        private void PipeServerModule_OnRunSplit(object sender, RunSplitEventArgs e)
        {

        }

       /* private void PipeServerModule_OnRunGold(object sender, EventArgs e)
        {
            Console.WriteLine("Hooray?");
            GoldMessage(10000);
        }

        private async void GoldMessage(int delay)
        {
            await Task.Delay(delay);
            Client.SendMessage(Channel, "PurpleGuy");
        }*/

        private void BlueGuyModule_OnColorChange(object sender, EventArgs e)
        {
            Client.SendMessage(Channel, "Oooooh... pretty! OhISee");
        }

        private void BlueGuyModule_OnUnknownColor(object sender, string unknownColor)
        {
            Client.SendMessage(Channel, String.Format("I'm not familiar with this color birbAnalysis", unknownColor));
        }

        private void BlueGuyModule_OnNoArgument(object sender, EventArgs e)
        {
            Client.SendMessage(Channel, "To change the Guy's color, try \"!guy purple\", \"!guy random\", or \"!guy #ff0000\"");
        }

        private void BlueGuyModule_OnRandomColor(object sender, string colorName)
        {
            Client.SendMessage(Channel, String.Format("Let's try {0} LilAnalysis", colorName));
        }

        private void BlueGuyModule_OnSameColor(object sender, EventArgs e)
        {
            Client.SendMessage(Channel, "The Guy is already that color Sussy");
        }
    }
}
