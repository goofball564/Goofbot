namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using Microsoft.VisualStudio.Threading;
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

    private async Task SongCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string message = string.Empty;
        NameAndArtistNames songAndArtistNames = await this.spotifyAPI.GetCurrentlyPlayingSongAndArtistsAsync();
        string currentlyPlayingSongName = songAndArtistNames.Name;
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

    private async Task AlbumCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        NameAndArtistNames currentlyPlayingAlbum = await this.spotifyAPI.GetCurrentlyPlayingAlbumAndArtistsAsync();
        string currentlyPlayingAlbumName = currentlyPlayingAlbum.Name;
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

    private readonly struct NameAndArtistNames
    {
        public readonly string Name;
        public readonly List<string> ArtistNames;

        public NameAndArtistNames(string songName, List<string> artistNames)
        {
            this.Name = songName;
            this.ArtistNames = artistNames;
        }
    }

    private class SpotifyAPI : IDisposable
    {
        private readonly string clientID;
        private readonly string clientSecret;

        private readonly SemaphoreSlim semaphore = new (1, 1);
        private readonly AsyncManualResetEvent spotifyClientConnected = new (false, false);

        private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);
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

            await this.spotifyClientConnected.WaitAsync();
        }

        public async Task<NameAndArtistNames> GetCurrentlyPlayingSongAndArtistsAsync()
        {
            return await this.GetCurrentlyPlayingHelperAsync(false);
        }

        public async Task<NameAndArtistNames> GetCurrentlyPlayingAlbumAndArtistsAsync()
        {
            return await this.GetCurrentlyPlayingHelperAsync(true);
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

        private async Task<NameAndArtistNames> GetCurrentlyPlayingHelperAsync(bool album = false)
        {
            string name = string.Empty;
            List<string> artistNames = [];

            await this.semaphore.WaitAsync();
            try
            {
                await this.RefreshCachedApiResponsesAsync();

                var context = this.context;
                var queue = this.queue;

                List<SimpleArtist> artists = [];
                if (context != null && context.IsPlaying)
                {
                    var currentlyPlaying = GetCurrentlyPlaying(queue);
                    if (album)
                    {
                        name = currentlyPlaying?.Album.Name;
                        artists = currentlyPlaying?.Album.Artists;
                    }
                    else
                    {
                        name = currentlyPlaying?.Name;
                        artists = currentlyPlaying?.Artists;
                    }
                }

                foreach (var artist in artists)
                {
                    artistNames.Add(artist.Name);
                }
            }
            finally
            {
                this.semaphore.Release();
            }

            return new NameAndArtistNames(name, artistNames);
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

            this.spotifyClientConnected.Set();
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await this.server.Stop();
        }
    }
}
