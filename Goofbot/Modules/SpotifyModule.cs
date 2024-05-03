using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Goofbot.Modules
{
    public class SpotifyModule
    {
        private EmbedIOAuthServer server;
        private SpotifyClient spotify;
        private VolumeControlModule volumeControlModule;

        private const string spotifyIdsFile = "Stuff\\spotify_ids.json";
        private const string playlistId = "3xtP334FUG7v3hE46Uavbi"; // Driving
        private const int mainLoopInterval = 12000;
        private const double queueModeSomething = 60;

        private readonly TimeSpan callAgainTimeout = TimeSpan.FromSeconds(6);

        private readonly string clientId;
        private readonly string clientSecret;

        private volatile bool queueMode = false;
        private volatile bool farmMode = true;

        private volatile bool usedCached = false;

        private bool removedFromPlaylist = false;
        private bool addedToQueue = false;

        private string currentlyPlayingId = "";

        private DateTime timeOfLastContextCall = DateTime.MinValue;
        private object contextLock = new object();
        private volatile CurrentlyPlayingContext contextCached;

        private DateTime timeOfLastQueueCall = DateTime.MinValue;
        private object queueLock = new object();
        private volatile QueueResponse queueCached;

        private object songLock = new object();
        private volatile string currentlyPlayingSongName = "";

        private object artistLock = new object();
        private volatile List<SimpleArtist> currentlyPlayingArtistsNames;

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

        public string CurrentlyPlayingSongName
        {
            get
            {
                lock (songLock)
                {
                    return currentlyPlayingSongName;
                }    
            }
        }

        public List<string> CurrentlyPlayingArtistsNames
        {
            get
            {
                var artists = new List<string>();
                lock (artistLock)
                {
                    if (currentlyPlayingArtistsNames != null)
                    {
                        foreach (var artist in currentlyPlayingArtistsNames)
                        {
                            artists.Add(artist.Name);
                        }
                    } 
                }
                return artists;
            }
        }

        public SpotifyModule()
        {
            volumeControlModule = new VolumeControlModule();
            dynamic tokenOpts = Program.ParseJsonFile(spotifyIdsFile);
            clientId = Convert.ToString(tokenOpts.client_id);
            clientSecret = Convert.ToString(tokenOpts.client_secret);

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

        private async Task MainLoop()
        {
            while (true)
            {
                Thread.Sleep(mainLoopInterval);
                var context = await GetContext();

                if (context != null && context.IsPlaying)
                {
                    var queue = await GetQueue(usedCached);

                    var nextInQueue = GetNextInQueue(queue);
                    double? remainingDuration = GetRemainingDuration(queue, context);
                    var currentlyPlaying = GetCurrentlyPlaying(queue);

                    string? currentlyPlayingId = currentlyPlaying?.Id;
                    string? nextInQueueId = nextInQueue?.Id;                  

                    if (currentlyPlayingId != null && currentlyPlayingId != this.currentlyPlayingId)
                    {
                        this.currentlyPlayingId = currentlyPlayingId;
                        addedToQueue = false;
                        removedFromPlaylist = false;

                        lock (songLock)
                            this.currentlyPlayingSongName = currentlyPlaying?.Name;
                        lock (artistLock)
                            this.currentlyPlayingArtistsNames = currentlyPlaying?.Artists;
                    }

                    if (remainingDuration != null && remainingDuration < queueModeSomething)
                    {
                        var playlist = await spotify.Playlists.Get(playlistId);
                        var firstInPlaylist = GetFirstInPlaylist(playlist);

                        string? firstInPlaylistId = firstInPlaylist?.Id;
                        string? firstInPlaylistUri = firstInPlaylist?.Uri;

                        if (!removedFromPlaylist && currentlyPlayingId != null && currentlyPlayingId == firstInPlaylistId)
                        {
                            removedFromPlaylist = true;
                            IList<int>? indicesToRemove = new List<int> { 0, };
                            string? snapshotId = playlist.SnapshotId;
                            spotify.Playlists.RemoveItems(playlistId, new PlaylistRemoveItemsRequest { Positions = indicesToRemove, SnapshotId = snapshotId });
                        }
                        else if (QueueMode && !addedToQueue && nextInQueueId != null && firstInPlaylistUri != null && nextInQueueId != firstInPlaylistId)
                        {
                            addedToQueue = true;
                            spotify.Player.AddToQueue(new PlayerAddToQueueRequest(firstInPlaylistUri));
                        }
                    }
                }
            }
        }

        public async void RefreshCurrentlyPlaying()
        {
            var context = await GetContext();

            if (context != null && context.IsPlaying)
            {
                var queue = await GetQueue(usedCached);
                var currentlyPlaying = GetCurrentlyPlaying(queue);
                lock (songLock)
                    this.currentlyPlayingSongName = currentlyPlaying?.Name;
                lock (artistLock)
                    this.currentlyPlayingArtistsNames = currentlyPlaying?.Artists;
            }
            else
            {
                lock (songLock)
                    this.currentlyPlayingSongName = "";
                lock (artistLock)
                    this.currentlyPlayingArtistsNames = [];
            }
        }

        private async Task<CurrentlyPlayingContext> GetContext()
        {
            DateTime now = DateTime.UtcNow;
            DateTime timeoutTime = timeOfLastContextCall.Add(callAgainTimeout);
            if (timeoutTime.CompareTo(now) < 0)
            {
                usedCached = false;
                timeOfLastContextCall = now;
                var context = await spotify.Player.GetCurrentPlayback();
                lock (contextLock)
                {
                    contextCached = context;
                }
                return context;
            }
            else
            {
                usedCached = true;
                CurrentlyPlayingContext context;
                lock (contextLock)
                {
                    context = contextCached;
                }
                return context;
            }
        }

        private async Task<QueueResponse> GetQueue(bool useCached = false)
        {
            DateTime now = DateTime.UtcNow;
            DateTime timeoutTime = timeOfLastQueueCall.Add(callAgainTimeout);
            if (!useCached || timeoutTime.CompareTo(now) < 0)
            {
                timeOfLastQueueCall = now;
                var queue = await spotify.Player.GetQueue();
                lock (queueLock)
                {
                    queueCached = queue;
                }
                return queue;
            }
            else
            {
                QueueResponse queue;
                lock (queueLock)
                {
                    queue = queueCached;
                }
                return queue;
            }
        }

        private double? GetRemainingDuration(QueueResponse? queue, CurrentlyPlayingContext? context)
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

        private FullTrack? GetCurrentlyPlaying(QueueResponse? queue)
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

        private FullTrack? GetNextInQueue(QueueResponse? queue)
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

        private FullTrack? GetFirstInPlaylist(FullPlaylist? playlist)
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

            MainLoop();
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await server.Stop();
        }

        private class CachedResponse
        {

        }
    }
}