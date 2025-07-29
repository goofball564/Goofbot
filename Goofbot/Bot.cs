using System;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Goofbot.Modules;

namespace Goofbot
{
    internal class Bot
    {
        private readonly TwitchClient Client;

        private readonly ChatInteractionModule ChatInteractionModule;
        private readonly PipeServerModule PipeServerModule;
        private readonly BlueGuyModule BlueGuyModule;
        private readonly CommandParsingModule CommandParsingModule;
        private readonly SpotifyModule SpotifyModule;
        private readonly SoundAlertModule SoundAlertModule;

        // private Task WaitTask;

        private readonly string Channel;

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

            var credentials = new ConnectionCredentials(botAccount, accessToken);
            /*var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };*/
            var customClient = new WebSocketClient();//clientOptions);

            Client = new TwitchClient(customClient);
            Client.Initialize(credentials, channelToJoin);
            Client.OnLog += Client_OnLog;
            Client.OnJoinedChannel += Client_OnJoinedChannel;
            Client.OnMessageReceived += Client_OnMessageReceived;
            Client.OnConnected += Client_OnConnected;
            Client.Connect();

            CommandParsingModule = new CommandParsingModule();
            PipeServerModule = new PipeServerModule();
            BlueGuyModule = new BlueGuyModule();
            SpotifyModule = new SpotifyModule();

            CommandParsingModule.BlueGuyCommand.ExecuteCommand += BlueGuyModule.OnGuyCommand;
            CommandParsingModule.QueueModeCommand.ExecuteCommand += CommandParsingModule_OnQueueModeCommand;
            CommandParsingModule.SongCommand.ExecuteCommand += CommandParsingModule_OnSongCommand;

            CommandParsingModule.TimeoutNotElapsed += CommandParsingModule_OnTimeoutNotElapsed;
            CommandParsingModule.NotBroadcaster += CommandParsingModule_OnNotBroadcaster;

            BlueGuyModule.ColorChange += BlueGuyModule_OnColorChange;
            BlueGuyModule.UnknownColor += BlueGuyModule_OnUnknownColor;
            BlueGuyModule.NoArgument += BlueGuyModule_OnNoArgument;
            BlueGuyModule.RandomColor += BlueGuyModule_OnRandomColor;
            BlueGuyModule.SameColor += BlueGuyModule_OnSameColor;

            PipeServerModule.RunStart += PiperServerModule_OnRunStart;
            PipeServerModule.RunReset += PipeServerModule_OnRunReset;
            PipeServerModule.RunSplit += PipeServerModule_OnRunSplit;

            PipeServerModule.Start();
            Client.SendMessage(Channel, "Goofbot is activated and at your service MrDestructoid");
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            CommandParsingModule.ParseMessageForCommand(e);
        }

        private async void CommandParsingModule_OnSongCommand(object sender, string e)
        {
            await SpotifyModule.RefreshCurrentlyPlaying();
            string artists = string.Join(", ", SpotifyModule.CurrentlyPlayingArtistsNames);
            string song = SpotifyModule.CurrentlyPlayingSongName;
            if (song == "" || artists == "")
            {
                Client.SendMessage(Channel, "Ain't nothing playing.");
            }
            else
            {
                Client.SendMessage(Channel, song + " by " + artists);
            }
        }

        private void CommandParsingModule_OnQueueModeCommand(object sender, string args)
        {
            const string successResponse = "Aye Aye, Captain! FrankerZ 7";
            args = args.ToLowerInvariant();
            if (args == "on")
            {
                SpotifyModule.QueueMode = true;
                Client.SendMessage(Channel, successResponse);
            }
            else if (args == "off")
            {
                SpotifyModule.QueueMode = false;
                Client.SendMessage(Channel, successResponse);
            }
            else
            {
                Client.SendMessage(Channel, "?");
            }
        }

        private void CommandParsingModule_OnTimeoutNotElapsed(object sender, TimeSpan timeUntilTimeoutElapses)
        {
            Client.SendMessage(Channel, String.Format("Please wait {0} minutes and {1} seconds, then try again.", timeUntilTimeoutElapses.Minutes, timeUntilTimeoutElapses.Seconds));
        }

        private void CommandParsingModule_OnNotBroadcaster(object sender, EventArgs e)
        {
            Client.SendMessage(Channel, "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)");
        }

        private void PiperServerModule_OnRunStart(object sender, int runCount)
        {
            // Client.SendMessage(Channel, String.Format("Run {0}! dinkDonk Give it up for run {1}! dinkDonk", runCount, runCount));
            SpotifyModule.QueueMode = true;
        }

        private void PipeServerModule_OnRunReset(object sender, int runCount)
        {
            if (!SpotifyModule.FarmMode)
                SpotifyModule.QueueMode = false;
        }

        private void PipeServerModule_OnRunSplit(object sender, RunSplitEventArgs e)
        {
            if (e.CurrentSplitIndex < 5)
            {
                SpotifyModule.QueueMode = true;
            }
            else
            {
                SpotifyModule.QueueMode = false;
            }
        }

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
