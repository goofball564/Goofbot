using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Goofbot.Modules
{
    public class SpotifyModule
    {
        private const string spotifyIdsFile = "Stuff\\spotify_ids.json";
        private const int queueModeLoopInterval = 12000;
        private const double remainingDurationThresholdForQueueMode = 60;

        private EmbedIOAuthServer server;
        private SpotifyClient spotify;
        private readonly VolumeControlModule volumeControlModule;
        private readonly CachedApiResponses cachedApiResponses;

        private readonly dynamic spotifyIds = Program.ParseJsonFile(spotifyIdsFile);
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string playlistId;

        private volatile bool queueMode = false;
        private volatile bool farmMode = true;

        private bool removedFromPlaylist = false;
        private bool addedToQueue = false;

        private readonly ThreadSafeObject<string> currentlyPlayingId = new();
        private readonly ThreadSafeObject<string> currentlyPlayingSongName = new();
        private readonly ThreadSafeObject<List<SimpleArtist>> currentlyPlayingArtistsNames = new();

        public class ThreadSafeObject<T>
        {
            private readonly object lockObject = new();
            private T value;

            public T Value
            {
                get
                {
                    lock (lockObject)
                    {
                        return value;
                    }
                }
                set
                {
                    lock (lockObject)
                    {
                        this.value = value;
                    }
                }
            }
        }

        private class CachedApiResponses
        {
            public CurrentlyPlayingContext Context
            {
                get
                {
                    return contextCached.Value;
                }
            }

            public QueueResponse Queue
            {
                get
                {
                    return queueCached.Value;
                }
            }

            private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);
            private DateTime timeOfLastCall = DateTime.MinValue;

            private ThreadSafeObject<CurrentlyPlayingContext> contextCached = new();
            private ThreadSafeObject<QueueResponse> queueCached = new();

            public async Task<bool> RefreshCachedApiResponses(SpotifyClient spotify)
            {
                DateTime now = DateTime.UtcNow;
                DateTime timeoutTime = timeOfLastCall.Add(callAgainTimeout);
                if (timeoutTime.CompareTo(now) < 0)
                {
                    timeOfLastCall = now;
                    var context = spotify.Player.GetCurrentPlayback();
                    var queue = spotify.Player.GetQueue();

                    contextCached.Value = await context;
                    queueCached.Value = await queue;
                }
                return true;
            }
        }

        public bool QueueMode
        {
            get
            {
                return queueMode;
            }
            set
            {
                if (value == true)
                {
                    volumeControlModule.SpotifyVolume = 0.07f;
                    volumeControlModule.DarkSoulsVolume = 0.22f;
                }
                    
                else
                {
                    volumeControlModule.SpotifyVolume = 0.05f;
                    volumeControlModule.DarkSoulsVolume = 0.27f;
                }
                    
                queueMode = value;
            }
        }

        public bool FarmMode
        {
            get
            {
                return farmMode;
            }
            set
            {
                farmMode = value;
            }
        }

        public string CurrentlyPlayingId
        {
            get
            {
                return currentlyPlayingId.Value;
            }
        }

        public string CurrentlyPlayingSongName
        {
            get
            {
                return currentlyPlayingSongName.Value;   
            }
        }

        public List<string> CurrentlyPlayingArtistsNames
        {
            get
            {
                var artistsNamesAsString = new List<string>();
                var artistsAsSimpleArtist = currentlyPlayingArtistsNames.Value;
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

        public SpotifyModule()
        {
            volumeControlModule = new VolumeControlModule();
            cachedApiResponses = new CachedApiResponses();

            clientId = Convert.ToString(spotifyIds.client_id);
            clientSecret = Convert.ToString(spotifyIds.client_secret);
            playlistId = Convert.ToString(spotifyIds.playlist_id);

            Initialize();
        }

        public async Task Initialize()
        {
            // Make sure "http://localhost:5543/callback" is in your spotify application as redirect uri!
            server = new EmbedIOAuthServer(new Uri("http://localhost:5543/callback"), 5543);
            await server.Start();

            server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string> { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPrivate }
            };
            BrowserUtil.Open(request.ToUri());
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await server.Stop();

            var tokenResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeTokenRequest(
                  clientId, clientSecret, response.Code, new Uri("http://localhost:5543/callback")
              )
            );


            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse));
            spotify = new SpotifyClient(config);

            QueueModeLoop();
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await server.Stop();
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
                Thread.Sleep(queueModeLoopInterval);

                await cachedApiResponses.RefreshCachedApiResponses(spotify);
                var context = cachedApiResponses.Context;
                var queue = cachedApiResponses.Queue;

                // if spotify is playing
                if (context != null && context.IsPlaying)
                {
                    // get what's currently playing, see if it's changed
                    // from the last time we checked
                    var currentlyPlaying = GetCurrentlyPlaying(queue);
                    string? currentlyPlayingId = currentlyPlaying?.Id;
                    if (currentlyPlayingId != null && currentlyPlayingId != CurrentlyPlayingId)
                    {
                        // IF now playing a different song than before...
                        // set currently playing song info
                        this.currentlyPlayingId.Value = currentlyPlayingId;
                        this.currentlyPlayingSongName.Value = currentlyPlaying?.Name;
                        this.currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;

                        // set these to false, meaning these actions are
                        // queued to happen (will be set to true when the action is taken
                        // so the action only happens once)
                        addedToQueue = false;
                        removedFromPlaylist = false;
                    }

                    // get remaining duration of current song, next song in queue
                    double? remainingDuration = GetRemainingDuration(queue, context);
                    var nextInQueue = GetNextInQueue(queue);
                    string? nextInQueueId = nextInQueue?.Id;

                    // if a certain duration remains in the current song
                    if (remainingDuration != null && remainingDuration < remainingDurationThresholdForQueueMode)
                    {
                        // get queue playlist
                        var playlist = await spotify.Playlists.Get(playlistId);

                        // get first song in queue playlist
                        var firstInPlaylist = GetFirstInPlaylist(playlist);
                        string? firstInPlaylistId = firstInPlaylist?.Id;
                        string? firstInPlaylistUri = firstInPlaylist?.Uri;

                        // if first song in queue playlist is currently playing, remove it from head of playlist
                        // (do this only once until currently playing song changes)
                        if (!removedFromPlaylist && currentlyPlayingId != null && currentlyPlayingId == firstInPlaylistId)
                        {
                            removedFromPlaylist = true;
                            RemoveFirstSongFromPlaylist(playlist, playlistId);
                        }
                        // if first song in queue playlist is NOT currently playing, add first song in queue playlist to queue
                        // (do this only once until currently playing song changes)
                        else if (QueueMode && !addedToQueue && nextInQueueId != null && firstInPlaylistUri != null && nextInQueueId != firstInPlaylistId)
                        {
                            addedToQueue = true;
                            spotify.Player.AddToQueue(new PlayerAddToQueueRequest(firstInPlaylistUri));
                        }
                    }
                }
            }
        }

        public async Task<bool> RefreshCurrentlyPlaying()
        {
            await cachedApiResponses.RefreshCachedApiResponses(spotify);
            var context = cachedApiResponses.Context;
            var queue = cachedApiResponses.Queue;

            if (context != null && context.IsPlaying)
            {
                var currentlyPlaying = GetCurrentlyPlaying(queue);
                currentlyPlayingId.Value = currentlyPlaying?.Id;
                currentlyPlayingSongName.Value = currentlyPlaying?.Name;
                currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;
            }
            else
            {
                currentlyPlayingId.Value = "";
                currentlyPlayingSongName.Value = "";
                currentlyPlayingArtistsNames.Value = [];
            }
            return true;
        }

        private async Task RemoveFirstSongFromPlaylist(FullPlaylist playlist, string playlistId)
        {
            IList<int>? indicesToRemove = new List<int> { 0, };
            string? snapshotId = playlist.SnapshotId;
            spotify.Playlists.RemoveItems(playlistId, new PlaylistRemoveItemsRequest { Positions = indicesToRemove, SnapshotId = snapshotId });
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
    }
}