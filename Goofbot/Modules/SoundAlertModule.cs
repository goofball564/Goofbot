using System.Collections.Generic;
using System;
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
            app.Run();
        }

        private class SoundAlertModuleService : IHostedService
        {
            private readonly EventSubWebsocketClient _eventSubWebsocketClient = new();
            private readonly TwitchAPI _twitchApi = new();
            private string _userId;

            public SoundAlertModuleService()
            {
                _twitchApi.Settings.ClientId = Program.ClientId;
                _twitchApi.Settings.AccessToken = Program.AccessToken;

                _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
                _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
                _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
                _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

                // You need the UserID for the User/Channel you want to get Events from.
                // You can use await _api.Helix.Users.GetUsersAsync() for that.
                _userId = "600829895";

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
                    var condition = new Dictionary<string, string> { { "broadcaster_user_id", _userId } };
                    await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
                }
            }

            private async Task OnWebsocketDisconnected(object sender, EventArgs e)
            {
                Console.WriteLine($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

                // Don't do this in production. You should implement a better reconnect strategy with exponential backoff
                while (!await _eventSubWebsocketClient.ReconnectAsync())
                {
                    Console.WriteLine("Websocket reconnect failed!");
                    await Task.Delay(1000);
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
                var reward = e.Notification.Payload.Event.Reward.Title;
                Console.WriteLine($"REWARD REDEEMED: {reward}");
            }

        }

        
    }
}
