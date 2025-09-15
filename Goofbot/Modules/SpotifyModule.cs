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
using System.Timers;
using TwitchLib.Client.Events;

internal class SpotifyModule : GoofbotModule
{
    private const double QueueModeRemainingDurationThreshold = 60;
    private const string QueueModePlaylistID = "3du4cQHr1wlUagJ0UGf5sR";

    private readonly System.Timers.Timer timer = new (TimeSpan.FromSeconds(20));
    private readonly SpotifyAPI spotifyAPI;
    private bool queueModeBackingValue;

    private bool removedFromPlaylist = false;
    private bool addedToQueue = false;

    private string? currentlyPlayingID;

    public SpotifyModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        string spotifyCredentialsFile = Path.Join(this.moduleDataFolder, "spotify_credentials.json");
        dynamic spotifyCredentials = Program.ParseJsonFile(spotifyCredentialsFile);
        string clientID = Convert.ToString(spotifyCredentials.client_id);
        string clientSecret = Convert.ToString(spotifyCredentials.client_secret);

        this.spotifyAPI = new SpotifyAPI(clientID, clientSecret);

        this.timer.AutoReset = true;
        this.timer.Elapsed += this.QueueModeCallback;

        this.bot.CommandDictionary.TryAddCommand(new Command("song", this.SongCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("album", this.AlbumCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("queuemode", this.QueueModeCommand, CommandAccessibilityModifier.StreamerOnly));
    }

    public bool QueueMode
    {
        get
        {
            return this.queueModeBackingValue;
        }

        set
        {
            if (value)
            {
                this.timer.Start();
            }
            else
            {
                this.timer.Stop();
            }

            this.queueModeBackingValue = value;
        }
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

    private static double? GetRemainingDuration(QueueResponse? queue, CurrentlyPlayingContext? context)
    {
        if (queue == null || context == null)
        {
            return null;
        }

        if (queue.CurrentlyPlaying is FullTrack track)
        {
            int duration = track.DurationMs;
            int remainingDuration = duration - context.ProgressMs;
            return remainingDuration / 1000.0;
        }
        else
        {
            return null;
        }
    }

    private static FullTrack? GetNextInQueue(QueueResponse? queue)
    {
        if (queue == null)
        {
            return null;
        }

        if (queue.Queue[0] is FullTrack track)
        {
            return track;
        }
        else
        {
            return null;
        }
    }

    private static FullTrack? GetFirstInPlaylist(FullPlaylist? playlist)
    {
        if (playlist == null || playlist.Tracks == null || playlist.Tracks.Items == null)
        {
            return null;
        }

        var playableItem = playlist.Tracks.Items[0].Track;
        if (playableItem is FullTrack track)
        {
            return track;
        }
        else
        {
            return null;
        }
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

    private async Task QueueModeCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        this.QueueMode = !this.QueueMode;
        string chatMessage = this.QueueMode ? "Queue Mode has been enabled" : "Queue mode has been disabled";
        this.bot.SendMessage(chatMessage, isReversed);
    }

    private async void QueueModeCallback(object source, ElapsedEventArgs e)
    {
        ContextAndQueue contextAndQueue = await this.spotifyAPI.GetCurrentlyPlayingContextAndQueueAsync();
        var context = contextAndQueue.Context;
        var queue = contextAndQueue.Queue;

        // if spotify is playing
        if (context != null && context.IsPlaying)
        {
            // get what's currently playing, see if it's changed
            // from the last time we checked
            var currentlyPlaying = GetCurrentlyPlaying(queue);
            string? currentlyPlayingID = currentlyPlaying?.Id;
            if (currentlyPlayingID != null && currentlyPlayingID != this.currentlyPlayingID)
            {
                // IF now playing a different song than before...
                // set currently playing song info
                this.currentlyPlayingID = currentlyPlayingID;

                // set these to false, meaning these actions are
                // queued to happen (will be set to true when the action is taken
                // so the action only happens once)
                this.addedToQueue = false;
                this.removedFromPlaylist = false;
            }

            // get remaining duration of current song, next song in queue
            double? remainingDuration = GetRemainingDuration(queue, context);
            var nextInQueue = GetNextInQueue(queue);
            string? nextInQueueID = nextInQueue?.Id;

            // if a certain duration remains in the current song
            if (remainingDuration != null && remainingDuration < QueueModeRemainingDurationThreshold)
            {
                // get queue playlist
                var playlist = await this.spotifyAPI.GetPlaylistAsync(QueueModePlaylistID);

                // get first song in queue playlist
                var firstInPlaylist = GetFirstInPlaylist(playlist);
                string? firstInPlaylistID = firstInPlaylist?.Id;
                string? firstInPlaylistURI = firstInPlaylist?.Uri;

                if (!this.removedFromPlaylist && currentlyPlayingID != null && currentlyPlayingID == firstInPlaylistID)
                {
                    // if first song in queue playlist is currently playing, remove it from head of playlist
                    // (do this only once until currently playing song changes)
                    this.removedFromPlaylist = true;
                    await this.spotifyAPI.RemoveFirstSongFromPlaylist(playlist);
                }
                else if (!this.addedToQueue && nextInQueueID != null && firstInPlaylistURI != null && nextInQueueID != firstInPlaylistID)
                {
                    // if first song in queue playlist is NOT currently playing, add first song in queue playlist to queue
                    // (do this only once until currently playing song changes)
                    this.addedToQueue = true;
                    await this.spotifyAPI.AddSongToQueueAsync(firstInPlaylistURI);
                }
            }
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

    private readonly struct ContextAndQueue
    {
        public readonly CurrentlyPlayingContext Context;
        public readonly QueueResponse Queue;

        public ContextAndQueue(CurrentlyPlayingContext context, QueueResponse Queue)
        {
            this.Context = context;
            this.Queue = Queue;
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
            await this.semaphore.WaitAsync();
            try
            {
                return await this.GetCurrentlyPlayingHelperAsync(false);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public async Task<NameAndArtistNames> GetCurrentlyPlayingAlbumAndArtistsAsync()
        {
            await this.semaphore.WaitAsync();
            try
            {
                return await this.GetCurrentlyPlayingHelperAsync(true);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public async Task<ContextAndQueue> GetCurrentlyPlayingContextAndQueueAsync()
        {
            await this.semaphore.WaitAsync();
            try
            {
                await this.RefreshCachedApiResponsesAsync();
                return new ContextAndQueue(this.context, this.queue);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public async Task RemoveFirstSongFromPlaylist(FullPlaylist playlist)
        {
            IList<int>? indicesToRemove = [0,];
            string? snapshotId = playlist.SnapshotId;
            await this.spotify.Playlists.RemoveItems(playlist.Id, new PlaylistRemoveItemsRequest { Positions = indicesToRemove, SnapshotId = snapshotId });
        }

        public async Task AddSongToQueueAsync(string songURI)
        {
            await this.spotify.Player.AddToQueue(new PlayerAddToQueueRequest(songURI));
        }

        public async Task<FullPlaylist> GetPlaylistAsync(string playlistID)
        {
            return await this.spotify.Playlists.Get(playlistID);
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
            this.server.Dispose();
        }

        private async Task<NameAndArtistNames> GetCurrentlyPlayingHelperAsync(bool album = false)
        {
            string name = string.Empty;
            List<string> artistNames = [];

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
