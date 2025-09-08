namespace Goofbot.Modules;

using Goofbot.Utils;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

internal class SpotifyModule : GoofbotModule
{
    private const int QueueModeLoopInterval = 12000;
    private const double QueueModeRemainingDurationThreshold = 60;

    private readonly string spotifyCredentialsFile;
    private readonly string clientId;
    private readonly string clientSecret;

    private readonly CachedApiResponses cachedApiResponses;

    private readonly ThreadSafeObject<string> currentlyPlayingSongName = new ();
    private readonly ThreadSafeObject<List<SimpleArtist>> currentlyPlayingArtistsNames = new ();

    private EmbedIOAuthServer server;
    private SpotifyClient spotify;

    public SpotifyModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.spotifyCredentialsFile = Path.Join(this.ModuleDataFolder, "spotify_credentials.json");
        dynamic spotifyCredentials = Program.ParseJsonFile(this.spotifyCredentialsFile);

        this.cachedApiResponses = new CachedApiResponses();

        this.clientId = Convert.ToString(spotifyCredentials.client_id);
        this.clientSecret = Convert.ToString(spotifyCredentials.client_secret);

        Program.CommandDictionary.TryAddCommand(new Command("song", this.SongCommand));
    }

    public string CurrentlyPlayingSongName
    {
        get
        {
            return this.currentlyPlayingSongName.Value;
        }
    }

    public List<string> CurrentlyPlayingArtistsNames
    {
        get
        {
            var artistsNamesAsString = new List<string>();
            var artistsAsSimpleArtist = this.currentlyPlayingArtistsNames.Value;
            if (artistsAsSimpleArtist != null)
            {
                foreach (var artist in artistsAsSimpleArtist)
                {
                    artistsNamesAsString.Add(artist.Name);
                }
            }

            return artistsNamesAsString;
        }
    }

    public async Task Initialize()
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

    public async Task<bool> RefreshCurrentlyPlaying()
    {
        await this.cachedApiResponses.RefreshCachedApiResponses(this.spotify);
        var context = this.cachedApiResponses.Context;
        var queue = this.cachedApiResponses.Queue;

        if (context != null && context.IsPlaying)
        {
            var currentlyPlaying = GetCurrentlyPlaying(queue);
            this.currentlyPlayingSongName.Value = currentlyPlaying?.Name;
            this.currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;
        }
        else
        {
            this.currentlyPlayingSongName.Value = string.Empty;
            this.currentlyPlayingArtistsNames.Value = [];
        }

        return true;
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
        await this.RefreshCurrentlyPlaying();
        string artists = string.Join(", ", this.CurrentlyPlayingArtistsNames);
        string song = this.CurrentlyPlayingSongName;
        if (song == string.Empty || artists == string.Empty)
        {
            return "Ain't nothing playing";
        }
        else
        {
            return song + " by " + artists;
        }
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

    public class ThreadSafeObject<T>
    {
        private readonly object lockObject = new ();
        private T value;

        public T Value
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.value;
                }
            }

            set
            {
                lock (this.lockObject)
                {
                    this.value = value;
                }
            }
        }
    }

    private class CachedApiResponses
    {
        private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);
        private readonly ThreadSafeObject<CurrentlyPlayingContext> contextCached = new ();
        private readonly ThreadSafeObject<QueueResponse> queueCached = new ();

        private DateTime timeOfLastCall = DateTime.MinValue;

        public CurrentlyPlayingContext Context
        {
            get
            {
                return this.contextCached.Value;
            }
        }

        public QueueResponse Queue
        {
            get
            {
                return this.queueCached.Value;
            }
        }

        public async Task<bool> RefreshCachedApiResponses(SpotifyClient spotify)
        {
            DateTime now = DateTime.UtcNow;
            DateTime timeoutTime = this.timeOfLastCall.Add(this.callAgainTimeout);
            if (timeoutTime.CompareTo(now) < 0)
            {
                this.timeOfLastCall = now;
                var context = spotify.Player.GetCurrentPlayback();
                var queue = spotify.Player.GetQueue();

                this.contextCached.Value = await context;
                this.queueCached.Value = await queue;
            }

            return true;
        }
    }
}