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
    private readonly string playlistId;

    private readonly VolumeControlModule volumeControlModule;
    private readonly CachedApiResponses cachedApiResponses;

    private readonly ThreadSafeObject<string> currentlyPlayingId = new ();
    private readonly ThreadSafeObject<string> currentlyPlayingSongName = new ();
    private readonly ThreadSafeObject<List<SimpleArtist>> currentlyPlayingArtistsNames = new ();

    private EmbedIOAuthServer server;
    private SpotifyClient spotify;

    private volatile bool queueMode = false;
    private volatile bool farmMode = true;

    private bool removedFromPlaylist = false;
    private bool addedToQueue = false;

    public SpotifyModule(string moduleDataFolder, CommandDictionary commandDictionary)
        : base(moduleDataFolder)
    {
        this.spotifyCredentialsFile = Path.Combine(moduleDataFolder, "spotify_credentials.json");
        dynamic spotifyCredentials = Program.ParseJsonFile(this.spotifyCredentialsFile);

        this.volumeControlModule = new VolumeControlModule();
        this.cachedApiResponses = new CachedApiResponses();

        this.clientId = Convert.ToString(spotifyCredentials.client_id);
        this.clientSecret = Convert.ToString(spotifyCredentials.client_secret);

        // _playlistId = Convert.ToString(spotifyCredentials.playlist_id);
        var songCommandLambda = async (object module, string commandArgs, OnChatCommandReceivedArgs eventArgs) => { return await ((SpotifyModule)module).SongCommand(); };
        commandDictionary.TryAddCommand(new Command("song", this, songCommandLambda, 1));
    }

    public bool QueueMode
    {
        get
        {
            return this.queueMode;
        }

        set
        {
            if (value == true)
            {
                this.volumeControlModule.SpotifyVolume = 0.07f;
                this.volumeControlModule.DarkSoulsVolume = 0.22f;
            }
            else
            {
                this.volumeControlModule.SpotifyVolume = 0.05f;
                this.volumeControlModule.DarkSoulsVolume = 0.27f;
            }

            this.queueMode = value;
        }
    }

    public bool FarmMode
    {
        get
        {
            return this.farmMode;
        }

        set
        {
            this.farmMode = value;
        }
    }

    public string CurrentlyPlayingId
    {
        get
        {
            return this.currentlyPlayingId.Value;
        }
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
            this.currentlyPlayingId.Value = currentlyPlaying?.Id;
            this.currentlyPlayingSongName.Value = currentlyPlaying?.Name;
            this.currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;
        }
        else
        {
            this.currentlyPlayingId.Value = string.Empty;
            this.currentlyPlayingSongName.Value = string.Empty;
            this.currentlyPlayingArtistsNames.Value = [];
        }

        return true;
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

    private async Task<string> SongCommand()
    {
        await this.RefreshCurrentlyPlaying();
        string artists = string.Join(", ", this.CurrentlyPlayingArtistsNames);
        string song = this.CurrentlyPlayingSongName;
        if (song == string.Empty || artists == string.Empty)
        {
            return "Ain't nothing playing.";
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

        // QueueModeLoop();
    }

    private async Task OnErrorReceived(object sender, string error, string state)
    {
        Console.WriteLine($"Aborting authorization, error received: {error}");
        await this.server.Stop();
    }

    // ADDED HELLA COMMENTS CUZ THIS CODE IS BAD
    // loop specifically handles the QueueMode functinality
    // where if the mode is enabled, when current song is going to end,
    // the next song is queued up by the bot from a queue playlist,
    // (and then deleted from the playlist; the bot consumes the playlist)
    // rather than letting spotify continue to do whatever it was doing
    private async Task QueueModeLoop()
    {
        while (true)
        {
            // every 12 seconds
            // call spotify API to refresh currently known data
            await Task.Delay(QueueModeLoopInterval);

            await this.cachedApiResponses.RefreshCachedApiResponses(this.spotify);
            var context = this.cachedApiResponses.Context;
            var queue = this.cachedApiResponses.Queue;

            // if spotify is playing
            if (context != null && context.IsPlaying)
            {
                // get what's currently playing, see if it's changed
                // from the last time we checked
                var currentlyPlaying = GetCurrentlyPlaying(queue);
                string? currentlyPlayingId = currentlyPlaying?.Id;
                if (currentlyPlayingId != null && currentlyPlayingId != this.CurrentlyPlayingId)
                {
                    // IF now playing a different song than before...
                    // set currently playing song info
                    this.currentlyPlayingId.Value = currentlyPlayingId;
                    this.currentlyPlayingSongName.Value = currentlyPlaying?.Name;
                    this.currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;

                    // set these to false, meaning these actions are
                    // queued to happen (will be set to true when the action is taken
                    // so the action only happens once)
                    this.addedToQueue = false;
                    this.removedFromPlaylist = false;
                }

                // get remaining duration of current song, next song in queue
                double? remainingDuration = GetRemainingDuration(queue, context);
                var nextInQueue = GetNextInQueue(queue);
                string? nextInQueueId = nextInQueue?.Id;

                // if a certain duration remains in the current song
                if (remainingDuration != null && remainingDuration < QueueModeRemainingDurationThreshold)
                {
                    // get queue playlist
                    var playlist = await this.spotify.Playlists.Get(this.playlistId);

                    // get first song in queue playlist
                    var firstInPlaylist = GetFirstInPlaylist(playlist);
                    string? firstInPlaylistId = firstInPlaylist?.Id;
                    string? firstInPlaylistUri = firstInPlaylist?.Uri;

                    if (!this.removedFromPlaylist && currentlyPlayingId != null && currentlyPlayingId == firstInPlaylistId)
                    {
                        // if first song in queue playlist is currently playing, remove it from head of playlist
                        // (do this only once until currently playing song changes)
                        this.removedFromPlaylist = true;
                        await this.RemoveFirstSongFromPlaylist(playlist, this.playlistId);
                    }
                    else if (this.QueueMode && !this.addedToQueue && nextInQueueId != null && firstInPlaylistUri != null && nextInQueueId != firstInPlaylistId)
                    {
                        // if first song in queue playlist is NOT currently playing, add first song in queue playlist to queue
                        // (do this only once until currently playing song changes)
                        this.addedToQueue = true;
                        await this.spotify.Player.AddToQueue(new PlayerAddToQueueRequest(firstInPlaylistUri));
                    }
                }
            }
        }
    }

    private async Task RemoveFirstSongFromPlaylist(FullPlaylist playlist, string playlistId)
    {
        IList<int>? indicesToRemove = [0,];
        string? snapshotId = playlist.SnapshotId;
        await this.spotify.Playlists.RemoveItems(playlistId, new PlaylistRemoveItemsRequest { Positions = indicesToRemove, SnapshotId = snapshotId });
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