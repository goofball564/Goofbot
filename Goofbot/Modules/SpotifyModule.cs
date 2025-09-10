namespace Goofbot.Modules;

using Goofbot.Utils;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class SpotifyModule : GoofbotModule
{
    private readonly string spotifyCredentialsFile;
    private readonly string clientId;
    private readonly string clientSecret;

    private readonly CachedApiResponses cachedApiResponses;
    private readonly SemaphoreSlim semaphore = new (1, 1);

    private EmbedIOAuthServer server;
    private SpotifyClient spotify;

    public SpotifyModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.spotifyCredentialsFile = Path.Join(this.moduleDataFolder, "spotify_credentials.json");
        dynamic spotifyCredentials = Program.ParseJsonFile(this.spotifyCredentialsFile);

        this.cachedApiResponses = new CachedApiResponses();

        this.clientId = Convert.ToString(spotifyCredentials.client_id);
        this.clientSecret = Convert.ToString(spotifyCredentials.client_secret);

        this.bot.CommandDictionary.TryAddCommand(new Command("song", this.SongCommand));
    }

    public async Task InitializeAsync()
    {
        // Make sure "http://localhost:5543/callback" is in your spotify application as redirect uri!
        this.server = new EmbedIOAuthServer(new Uri("http://localhost:5543/callback"), 5543);
        await this.server.Start();

        this.server.AuthorizationCodeReceived += this.OnAuthorizationCodeReceived;
        this.server.ErrorReceived += this.OnErrorReceived;

        var request = new LoginRequest(this.server.BaseUri, this.clientId, LoginRequest.ResponseType.Code)
        {
            Scope = [Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPrivate,],
        };
        BrowserUtil.Open(request.ToUri());
    }

    private static FullTrack? GetCurrentlyPlaying(QueueResponse? queue)
    {
        if (queue == null)
        {
            return null;
        }

        if (queue.CurrentlyPlaying is FullTrack track)
        {
            return track;
        }
        else
        {
            return null;
        }
    }

    private async Task<string> SongCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        string message = string.Empty;
        await this.semaphore.WaitAsync();
        try
        {
            await this.cachedApiResponses.RefreshCachedApiResponses(this.spotify);
            var context = this.cachedApiResponses.Context;
            var queue = this.cachedApiResponses.Queue;

            string currentlyPlayingSongName = string.Empty;
            List<SimpleArtist> currentlyPlayingArtists = [];
            if (context != null && context.IsPlaying)
            {
                var currentlyPlaying = GetCurrentlyPlaying(queue);
                currentlyPlayingSongName = currentlyPlaying?.Name;
                currentlyPlayingArtists = currentlyPlaying?.Artists;
            }

            string currentlyPlayingArtistNames = string.Join(", ", currentlyPlayingArtists);
            if (currentlyPlayingSongName.Equals(string.Empty) || currentlyPlayingArtistNames.Equals(string.Empty))
            {
                message = "Ain't nothing playing";
            }
            else
            {
                message = currentlyPlayingSongName + " by " + currentlyPlayingArtistNames;
            }
        }
        finally
        {
            this.semaphore.Release();
        }

        return message;
    }

    private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
    {
        await this.server.Stop();

        var tokenResponse = await new OAuthClient().RequestToken(
          new AuthorizationCodeTokenRequest(
              this.clientId, this.clientSecret, response.Code, new Uri("http://localhost:5543/callback")));

        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(this.clientId, this.clientSecret, tokenResponse));
        this.spotify = new SpotifyClient(config);
    }

    private async Task OnErrorReceived(object sender, string error, string state)
    {
        Console.WriteLine($"Aborting authorization, error received: {error}");
        await this.server.Stop();
    }

    private class CachedApiResponses
    {
        private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);
        private readonly SemaphoreSlim semaphore = new (1, 1);
        private DateTime timeOfLastCall = DateTime.MinValue;

        public CurrentlyPlayingContext Context { get; private set; }

        public QueueResponse Queue { get; private set; }

        public async Task<bool> RefreshCachedApiResponses(SpotifyClient spotify)
        {
            DateTime now = DateTime.UtcNow;
            DateTime timeoutTime = this.timeOfLastCall.Add(this.callAgainTimeout);
            if (timeoutTime.CompareTo(now) < 0)
            {
                await this.semaphore.WaitAsync();
                try
                {
                    this.timeOfLastCall = now;
                    var getCurrentContextTask = spotify.Player.GetCurrentPlayback();
                    var getQueueTask = spotify.Player.GetQueue();

                    this.Context = await getCurrentContextTask;
                    this.Queue = await getQueueTask;
                }
                finally
                {
                    this.semaphore.Release();
                }
            }

            return true;
        }
    }
}