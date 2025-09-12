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
    private readonly SpotifyAPI spotifyAPI;

    public SpotifyModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        string spotifyCredentialsFile = Path.Join(this.moduleDataFolder, "spotify_credentials.json");
        dynamic spotifyCredentials = Program.ParseJsonFile(spotifyCredentialsFile);
        string clientID = Convert.ToString(spotifyCredentials.client_id);
        string clientSecret = Convert.ToString(spotifyCredentials.client_secret);

        this.spotifyAPI = new SpotifyAPI(clientID, clientSecret);

        this.bot.CommandDictionary.TryAddCommand(new Command("song", this.SongCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("album", this.AlbumCommand));
    }

    public async Task InitializeAsync()
    {
        await this.spotifyAPI.InitializeAsync();
    }

    public override void Dispose()
    {
        this.spotifyAPI.Dispose();
        base.Dispose();
    }

    private async Task SongCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        string message = string.Empty;
        SongAndArtistNames songAndArtistNames = await this.spotifyAPI.GetCurrentlyPlayingSongAndArtistsAsync();
        string currentlyPlayingSongName = songAndArtistNames.SongName;
        string currentlyPlayingArtistNames = string.Join(", ", songAndArtistNames.ArtistNames);
        if (currentlyPlayingSongName.Equals(string.Empty) || currentlyPlayingArtistNames.Equals(string.Empty))
        {
            this.bot.SendMessage("Ain't nothing playing", isReversed);
        }
        else
        {
            this.bot.SendMessage(currentlyPlayingSongName + " by " + currentlyPlayingArtistNames, isReversed);
        }
    }

    private async Task AlbumCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs, bool isReversed)
    {
        AlbumAndArtistNames currentlyPlayingAlbum = await this.spotifyAPI.GetCurrentlyPlayingAlbumAndArtistsAsync();
        string currentlyPlayingAlbumName = currentlyPlayingAlbum.AlbumName;
        string currentlyPlayingArtistNames = string.Join(", ", currentlyPlayingAlbum.ArtistNames);
        if (currentlyPlayingAlbumName.Equals(string.Empty) || currentlyPlayingArtistNames.Equals(string.Empty))
        {
            this.bot.SendMessage("Ain't nothing playing", isReversed);
        }
        else
        {
            this.bot.SendMessage(currentlyPlayingAlbumName + " by " + currentlyPlayingArtistNames, isReversed);
        }
    }

    private struct SongAndArtistNames
    {
        public string SongName { get; set; }

        public List<string> ArtistNames { get; set; }
    }

    private struct AlbumAndArtistNames
    {
        public string AlbumName { get; set; }

        public List<string> ArtistNames { get; set; }
    }

    private class SpotifyAPI : IDisposable
    {
        private readonly string clientID;
        private readonly string clientSecret;

        private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);
        private readonly SemaphoreSlim semaphore = new (1, 1);
        private DateTime timeOfLastCall = DateTime.MinValue;

        private EmbedIOAuthServer server;
        private SpotifyClient spotify;

        private CurrentlyPlayingContext context;
        private QueueResponse queue;

        public SpotifyAPI(string clientID, string clientSecret)
        {
            this.clientID = clientID;
            this.clientSecret = clientSecret;
        }

        public async Task InitializeAsync()
        {
            // Make sure "http://localhost:5543/callback" is in your spotify application as redirect uri!
            this.server = new EmbedIOAuthServer(new Uri("http://localhost:5543/callback"), 5543);
            await this.server.Start();

            this.server.AuthorizationCodeReceived += this.OnAuthorizationCodeReceived;
            this.server.ErrorReceived += this.OnErrorReceived;

            var request = new LoginRequest(this.server.BaseUri, this.clientID, LoginRequest.ResponseType.Code)
            {
                Scope = [Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPrivate,],
            };
            BrowserUtil.Open(request.ToUri());
        }

        public async Task<SongAndArtistNames> GetCurrentlyPlayingSongAndArtistsAsync()
        {
            string currentlyPlayingSongName = string.Empty;
            List<string> currentlyPlayingArtistNames = [];

            await this.semaphore.WaitAsync();
            try
            {
                await this.RefreshCachedApiResponsesAsync();
                var context = this.context;
                var queue = this.queue;

                List<SimpleArtist> currentlyPlayingArtists = [];
                if (context != null && context.IsPlaying)
                {
                    var currentlyPlaying = GetCurrentlyPlaying(queue);
                    currentlyPlayingSongName = currentlyPlaying?.Name;
                    currentlyPlayingArtists = currentlyPlaying?.Artists;
                }

                foreach (var artist in currentlyPlayingArtists)
                {
                    currentlyPlayingArtistNames.Add(artist.Name);
                }
            }
            finally
            {
                this.semaphore.Release();
            }

            return new SongAndArtistNames { SongName = currentlyPlayingSongName, ArtistNames = currentlyPlayingArtistNames };
        }

        public async Task<AlbumAndArtistNames> GetCurrentlyPlayingAlbumAndArtistsAsync()
        {
            string currentlyPlayingAlbumName = string.Empty;
            List<string> currentlyPlayingArtistNames = [];

            await this.semaphore.WaitAsync();
            try
            {
                await this.RefreshCachedApiResponsesAsync();

                var context = this.context;
                var queue = this.queue;

                List<SimpleArtist> currentlyPlayingArtists = [];
                if (context != null && context.IsPlaying)
                {
                    var currentlyPlaying = GetCurrentlyPlaying(queue);
                    currentlyPlayingAlbumName = currentlyPlaying?.Album.Name;
                    currentlyPlayingArtists = currentlyPlaying?.Album.Artists;
                }

                foreach (var artist in currentlyPlayingArtists)
                {
                    currentlyPlayingArtistNames.Add(artist.Name);
                }
            }
            finally
            {
                this.semaphore.Release();
            }

            return new AlbumAndArtistNames { AlbumName = currentlyPlayingAlbumName, ArtistNames = currentlyPlayingArtistNames };
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
            this.server.Dispose();
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

        private async Task RefreshCachedApiResponsesAsync()
        {
            DateTime now = DateTime.UtcNow;
            DateTime timeoutTime = this.timeOfLastCall.Add(this.callAgainTimeout);
            if (timeoutTime.CompareTo(now) < 0)
            {
                this.timeOfLastCall = now;
                var getCurrentContextTask = this.spotify.Player.GetCurrentPlayback();
                var getQueueTask = this.spotify.Player.GetQueue();

                this.context = await getCurrentContextTask;
                this.queue = await getQueueTask;
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await this.server.Stop();

            var tokenResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeTokenRequest(
                  this.clientID, this.clientSecret, response.Code, new Uri("http://localhost:5543/callback")));

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(this.clientID, this.clientSecret, tokenResponse));
            this.spotify = new SpotifyClient(config);
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await this.server.Stop();
        }
    }
}
