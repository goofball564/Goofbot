namespace Goofbot.Utils;

using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Websockets.Extensions;
using TwitchLib.Api;

internal class ChannelPointRedemptionEventSub
{
    public readonly EventSubWebsocketClient EventSubWebsocketClient;

    private readonly IHost app;

    public ChannelPointRedemptionEventSub(TwitchAPI twitchAPI)
    {
        ChannelPointRedemptionEventSubService service = new (twitchAPI);
        this.EventSubWebsocketClient = service.EventSubWebsocketClient;

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTwitchLibEventSubWebsockets();
        builder.Services.AddSingleton<IHostedService>(provider => service);
        this.app = builder.Build();
    }

    public void Start()
    {
        this.app.Start();
    }

    private class ChannelPointRedemptionEventSubService : IHostedService
    {
        public readonly EventSubWebsocketClient EventSubWebsocketClient = new ();
        private readonly TwitchAPI twitchAPI;

        // You need the UserID for the User/Channel you want to get Events from.
        // You can use await _api.Helix.Users.GetUsersAsync() for that.
        private const string UserId = "600829895";

        public ChannelPointRedemptionEventSubService(TwitchAPI twitchAPI)
        {
            this.twitchAPI = twitchAPI;
            this.EventSubWebsocketClient.WebsocketConnected += this.OnWebsocketConnected;
            this.EventSubWebsocketClient.WebsocketDisconnected += this.OnWebsocketDisconnected;
            this.EventSubWebsocketClient.WebsocketReconnected += this.OnWebsocketReconnected;
            this.EventSubWebsocketClient.ErrorOccurred += this.OnErrorOccurred;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await this.EventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await this.EventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            Console.WriteLine($"Websocket {this.EventSubWebsocketClient.SessionId} connected!");

            if (!e.IsRequestedReconnect)
            {
                // subscribe to topics
                var condition = new Dictionary<string, string> { { "broadcaster_user_id", UserId } };
                await this.twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, this.EventSubWebsocketClient.SessionId);
            }
        }

        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine($"Websocket {this.EventSubWebsocketClient.SessionId} disconnected!");

            int delay = 1000;
            while (!await this.EventSubWebsocketClient.ReconnectAsync())
            {
                Console.WriteLine("Websocket reconnect failed!");
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
            Console.WriteLine($"Websocket {this.EventSubWebsocketClient.SessionId} reconnected");
        }

        private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
        {
            Console.WriteLine($"Websocket {this.EventSubWebsocketClient.SessionId} - Error occurred!");
        }
    }
}
