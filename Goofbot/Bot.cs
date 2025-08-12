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
        private readonly TwitchClient _client;
        private readonly string _channel;

        // private readonly ChatInteractionModule ChatInteractionModule;
        // private readonly PipeServerModule PipeServerModule;
        private readonly BlueGuyModule _blueGuyModule;
        private readonly CommandParsingModule _commandParsingModule;
        private readonly SpotifyModule _spotifyModule;
        private readonly SoundAlertModule _soundAlertModule;

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


        public Bot(string botUsername, string channelUsername, string botAccessToken)
        {
            _channel = channelUsername;

            var credentials = new ConnectionCredentials(botUsername, botAccessToken);
            
            var clientOptions = new ClientOptions();
            var customClient = new WebSocketClient(clientOptions);

            _client = new TwitchClient(customClient);
            _client.Initialize(credentials, channelUsername);
            _client.OnLog += Client_OnLog;
            _client.OnJoinedChannel += Client_OnJoinedChannel;
            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnConnected += Client_OnConnected;
            _client.OnIncorrectLogin += Client_OnIncorrectLogin;
            _client.Connect();

            _commandParsingModule = new CommandParsingModule();
            _blueGuyModule = new BlueGuyModule();
            _spotifyModule = new SpotifyModule();
            _soundAlertModule = new SoundAlertModule();

            _commandParsingModule.BlueGuyCommand.ExecuteCommand += _blueGuyModule.OnGuyCommand;
            // CommandParsingModule.QueueModeCommand.ExecuteCommand += CommandParsingModule_OnQueueModeCommand;
            _commandParsingModule.SongCommand.ExecuteCommand += CommandParsingModule_OnSongCommand;

            CommandParsingModule.TimeoutNotElapsed += CommandParsingModule_OnTimeoutNotElapsed;
            CommandParsingModule.NotBroadcaster += CommandParsingModule_OnNotBroadcaster;

            _blueGuyModule.ColorChange += BlueGuyModule_OnColorChange;
            _blueGuyModule.UnknownColor += BlueGuyModule_OnUnknownColor;
            _blueGuyModule.NoArgument += BlueGuyModule_OnNoArgument;
            _blueGuyModule.RandomColor += BlueGuyModule_OnRandomColor;
            _blueGuyModule.SameColor += BlueGuyModule_OnSameColor;

            // PipeServerModule.RunStart += PiperServerModule_OnRunStart;
            // PipeServerModule.RunReset += PipeServerModule_OnRunReset;
            // PipeServerModule.RunSplit += PipeServerModule_OnRunSplit;*/
            // PipeServerModule.Start();

            _client.SendMessage(_channel, "Goofbot is activated and at your service MrDestructoid");
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
            _commandParsingModule.ParseMessageForCommand(e);
        }

        private void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {

        }

        private async void CommandParsingModule_OnSongCommand(object sender, string e)
        {
            await _spotifyModule.RefreshCurrentlyPlaying();
            string artists = string.Join(", ", _spotifyModule.CurrentlyPlayingArtistsNames);
            string song = _spotifyModule.CurrentlyPlayingSongName;
            if (song == "" || artists == "")
            {
                _client.SendMessage(_channel, "Ain't nothing playing.");
            }
            else
            {
                _client.SendMessage(_channel, song + " by " + artists);
            }
        }

        /*private void CommandParsingModule_OnQueueModeCommand(object sender, string args)
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
        }*/

        private void CommandParsingModule_OnTimeoutNotElapsed(object sender, TimeSpan timeUntilTimeoutElapses)
        {
            _client.SendMessage(_channel, String.Format("Please wait {0} minutes and {1} seconds, then try again.", timeUntilTimeoutElapses.Minutes, timeUntilTimeoutElapses.Seconds));
        }

        private void CommandParsingModule_OnNotBroadcaster(object sender, EventArgs e)
        {
            _client.SendMessage(_channel, "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)");
        }

        /*private void PiperServerModule_OnRunStart(object sender, int runCount)
        {
            // Client.SendMessage(Channel, String.Format("Run {0}! dinkDonk Give it up for run {1}! dinkDonk", runCount, runCount));
            SpotifyModule.QueueMode = true;
        }*/

        /*private void PipeServerModule_OnRunReset(object sender, int runCount)
        {
            if (!SpotifyModule.FarmMode)
                SpotifyModule.QueueMode = false;
        }*/

        /*private void PipeServerModule_OnRunSplit(object sender, RunSplitEventArgs e)
        {
            if (e.CurrentSplitIndex < 5)
            {
                SpotifyModule.QueueMode = true;
            }
            else
            {
                SpotifyModule.QueueMode = false;
            }
        }*/

        private void BlueGuyModule_OnColorChange(object sender, EventArgs e)
        {
            _client.SendMessage(_channel, "Oooooh... pretty! OhISee");
        }

        private void BlueGuyModule_OnUnknownColor(object sender, string unknownColor)
        {
            _client.SendMessage(_channel, String.Format("I'm not familiar with this color birbAnalysis", unknownColor));
        }

        private void BlueGuyModule_OnNoArgument(object sender, EventArgs e)
        {
            _client.SendMessage(_channel, "To change the Guy's color, try \"!guy purple\", \"!guy random\", or \"!guy #ff0000\"");
        }

        private void BlueGuyModule_OnRandomColor(object sender, string colorName)
        {
            _client.SendMessage(_channel, String.Format("Let's try {0} LilAnalysis", colorName));
        }

        private void BlueGuyModule_OnSameColor(object sender, EventArgs e)
        {
            _client.SendMessage(_channel, "The Guy is already that color Sussy");
        }
    }
}
