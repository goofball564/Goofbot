namespace Goofbot.Modules;

using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Websockets.Extensions;
using Goofbot.Utils;

internal class SoundAlertModule
{
    public SoundAlertModule()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddHostedService<SoundAlertModuleService>();
        var app = builder.Build();
        app.Start();
    }

    private class SoundAlertModuleService : IHostedService
    {
        public static readonly string SoundAlertFolderPath = Path.Combine(Program.StuffFolder, "SoundAlertModule");

        // You need the UserID for the User/Channel you want to get Events from.
        // You can use await _api.Helix.Users.GetUsersAsync() for that.
        private const string UserId = "600829895";

        private readonly EventSubWebsocketClient eventSubWebsocketClient = new ();

        private readonly string soundAlertsCSVFilePath = Path.Join(SoundAlertFolderPath, "SoundAlerts.csv");
        private readonly SoundAlertDictionary soundAlertDictionary;

        public SoundAlertModuleService()
        {
            this.soundAlertDictionary = new SoundAlertDictionary(this.soundAlertsCSVFilePath);

            this.eventSubWebsocketClient.WebsocketConnected += this.OnWebsocketConnected;
            this.eventSubWebsocketClient.WebsocketDisconnected += this.OnWebsocketDisconnected;
            this.eventSubWebsocketClient.WebsocketReconnected += this.OnWebsocketReconnected;
            this.eventSubWebsocketClient.ErrorOccurred += this.OnErrorOccurred;

            this.eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await this.eventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await this.eventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            Console.WriteLine($"Websocket {this.eventSubWebsocketClient.SessionId} connected!");

            if (!e.IsRequestedReconnect)
            {
                // subscribe to topics
                var condition = new Dictionary<string, string> { { "broadcaster_user_id", UserId } };
                await Program.TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, this.eventSubWebsocketClient.SessionId);
            }
        }

        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine($"Websocket {this.eventSubWebsocketClient.SessionId} disconnected!");

            int delay = 1000;
            while (!await this.eventSubWebsocketClient.ReconnectAsync())
            {
                Console.WriteLine("Websocket reconnect failed!");
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
            Console.WriteLine($"Websocket {this.eventSubWebsocketClient.SessionId} reconnected");
        }

        private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
        {
            Console.WriteLine($"Websocket {this.eventSubWebsocketClient.SessionId} - Error occurred!");
        }

        private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            string reward = e.Notification.Payload.Event.Reward.Title.ToLowerInvariant();
            string sound = this.soundAlertDictionary.TryGetRandomFromList(reward);

            await Task.Delay(1000);
            new SoundPlayer(sound);
        }
    }

    private class SoundAlertDictionary
    {
        private readonly Random random = new ();
        private Dictionary<string, string[]> soundAlertDictionary = [];

        public SoundAlertDictionary(string soundAlertCSVFilename)
        {
            foreach (string line in File.ReadLines(soundAlertCSVFilename))
            {
                string[] csv = line.Split(",");

                if (csv[2].Contains('.'))
                {
                    // If csv[2] contains . it is a file name.
                    string redemption = csv[0];
                    string sound = csv[2];
                    sound = Path.Join(SoundAlertModuleService.SoundAlertFolderPath, sound);
                    string[] sounds = [sound];
                    this.soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
                }
                else
                {
                    // Otherwise, it is a folder name.
                    string redemption = csv[0];
                    string folder = csv[2];
                    folder = Path.Join(SoundAlertModuleService.SoundAlertFolderPath, folder);
                    string[] sounds = Directory.GetFiles(folder);
                    this.soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
                }
            }
        }

        public string TryGetRandomFromList(string key)
        {
            if (this.soundAlertDictionary.TryGetValue(key, out string[] sounds))
            {
                int randomIndex = this.random.Next(0, sounds.Length);
                return sounds[randomIndex];
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
