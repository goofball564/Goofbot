using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Websockets.Extensions;
using System.Linq;


namespace Goofbot.Modules
{
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
            private readonly string _soundAlertsCSVFilePath = Path.Join(SoundAlertFolderPath, "SoundAlerts.csv");

            private readonly EventSubWebsocketClient _eventSubWebsocketClient = new();

            private SoundAlertDictionary _soundAlertDictionary;

            public SoundAlertModuleService()
            {
                _soundAlertDictionary = new SoundAlertDictionary(_soundAlertsCSVFilePath);

                _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
                _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
                _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
                _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

                _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemptionAdd;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _eventSubWebsocketClient.ConnectAsync();
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _eventSubWebsocketClient.DisconnectAsync();
            }

            private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
            {
                Console.WriteLine($"Websocket {_eventSubWebsocketClient.SessionId} connected!");

                if (!e.IsRequestedReconnect)
                {
                    // subscribe to topics
                    var condition = new Dictionary<string, string> { { "broadcaster_user_id", UserId } };
                    await Program.TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
                }
            }

            private async Task OnWebsocketDisconnected(object sender, EventArgs e)
            {
                Console.WriteLine($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

                int delay = 1000;
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    Console.WriteLine("Websocket reconnect failed!");
                    await Task.Delay(delay);
                    delay *= 2;
                }
            }

            private async Task OnWebsocketReconnected(object sender, EventArgs e)
            {
                Console.WriteLine($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
            }

            private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
            {
                Console.WriteLine($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred!");
            }

            private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
            {
                string reward = e.Notification.Payload.Event.Reward.Title.ToLowerInvariant();
                string sound = _soundAlertDictionary.TryGetRandomFromList(reward);

                await Task.Delay(1000);
                new SoundPlayer(sound).Play();
            }
        }

        private class SoundAlertDictionary
        {
            private Dictionary<string, string[]> _soundAlertDictionary = new();
            private readonly Random _random = new();

            public SoundAlertDictionary(string soundAlertCSVFilename)
            {
                foreach (string line in File.ReadLines(soundAlertCSVFilename))
                {
                    string[] csv = line.Split(",");

                    // If csv[2] contains . it is a file name.
                    if (csv[2].Contains("."))
                    {
                        string redemption = csv[0];
                        string sound = csv[2];
                        sound = Path.Join(SoundAlertModuleService.SoundAlertFolderPath, sound);
                        string[] sounds = [sound];
                        _soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
                    }
                    // Otherwise, it is a folder name.
                    else
                    {
                        string redemption = csv[0];
                        string folder = csv[2];
                        folder = Path.Join(SoundAlertModuleService.SoundAlertFolderPath, folder);
                        string[] sounds = Directory.GetFiles(folder);
                        _soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
                    }

                }
            }

            public string TryGetRandomFromList(string key)
            {
                string[] sounds;
                if (_soundAlertDictionary.TryGetValue(key, out sounds))
                {
                    int randomIndex = _random.Next(0, sounds.Length);
                    return sounds[randomIndex];
                }
                else
                {
                    return "";
                }
            }
        }
    }
}
