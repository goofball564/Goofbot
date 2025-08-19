using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal class SpotifyModule : GoofbotModule
    {
        private const int QueueModeLoopInterval = 12000;
        private const double QueueModeRemainingDurationThreshold = 60;

        private readonly string _spotifyCredentialsFile;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _playlistId;

        private EmbedIOAuthServer _server;
        private SpotifyClient _spotify;
        private readonly VolumeControlModule _volumeControlModule;
        private readonly CachedApiResponses _cachedApiResponses;

        private readonly ThreadSafeObject<string> _currentlyPlayingId = new();
        private readonly ThreadSafeObject<string> _currentlyPlayingSongName = new();
        private readonly ThreadSafeObject<List<SimpleArtist>> _currentlyPlayingArtistsNames = new();

        private volatile bool _queueMode = false;
        private volatile bool _farmMode = true;

        private bool _removedFromPlaylist = false;
        private bool _addedToQueue = false;

        public bool QueueMode
        {
            get
            {
                return _queueMode;
            }
            set
            {
                if (value == true)
                {
                    _volumeControlModule.SpotifyVolume = 0.07f;
                    _volumeControlModule.DarkSoulsVolume = 0.22f;
                }
                    
                else
                {
                    _volumeControlModule.SpotifyVolume = 0.05f;
                    _volumeControlModule.DarkSoulsVolume = 0.27f;
                }
                    
                _queueMode = value;
            }
        }

        public bool FarmMode
        {
            get
            {
                return _farmMode;
            }
            set
            {
                _farmMode = value;
            }
        }

        public string CurrentlyPlayingId
        {
            get
            {
                return _currentlyPlayingId.Value;
            }
        }

        public string CurrentlyPlayingSongName
        {
            get
            {
                return _currentlyPlayingSongName.Value;   
            }
        }

        public List<string> CurrentlyPlayingArtistsNames
        {
            get
            {
                var artistsNamesAsString = new List<string>();
                var artistsAsSimpleArtist = _currentlyPlayingArtistsNames.Value;
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

        public SpotifyModule(string moduleDataFolder, TwitchClient twitchClient, TwitchAPI twitchAPI) : base(moduleDataFolder, twitchClient, twitchAPI)
        {
            _spotifyCredentialsFile = Path.Combine(_moduleDataFolder, "spotify_credentials.json");
            dynamic spotifyCredentials = Program.ParseJsonFile(_spotifyCredentialsFile);

            _volumeControlModule = new VolumeControlModule();
            _cachedApiResponses = new CachedApiResponses();

            _clientId = Convert.ToString(spotifyCredentials.client_id);
            _clientSecret = Convert.ToString(spotifyCredentials.client_secret);
            // _playlistId = Convert.ToString(spotifyCredentials.playlist_id);
        }

        private async void OnSongCommand(object sender, string e)
        {
            await RefreshCurrentlyPlaying();
            string artists = string.Join(", ", CurrentlyPlayingArtistsNames);
            string song = CurrentlyPlayingSongName;
            if (song == "" || artists == "")
            {
                _twitchClient.SendMessage(Program.TwitchChannelUsername, "Ain't nothing playing.");
            }
            else
            {
                _twitchClient.SendMessage(Program.TwitchChannelUsername, song + " by " + artists);
            }
        }

        protected override void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string command = Program.ParseMessageForCommand(e, out string args);
            if (command.Equals("!song"))
            {
                OnSongCommand(this, args);
            }
        }

        public async Task Initialize()
        {
            // Make sure "http://localhost:5543/callback" is in your spotify application as redirect uri!
            _server = new EmbedIOAuthServer(new Uri("http://localhost:5543/callback"), 5543);
            await _server.Start();

            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(_server.BaseUri, _clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string> { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPrivate }
            };
            BrowserUtil.Open(request.ToUri());
        }

        public async Task<bool> RefreshCurrentlyPlaying()
        {
            await _cachedApiResponses.RefreshCachedApiResponses(_spotify);
            var context = _cachedApiResponses.Context;
            var queue = _cachedApiResponses.Queue;

            if (context != null && context.IsPlaying)
            {
                var currentlyPlaying = GetCurrentlyPlaying(queue);
                _currentlyPlayingId.Value = currentlyPlaying?.Id;
                _currentlyPlayingSongName.Value = currentlyPlaying?.Name;
                _currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;
            }
            else
            {
                _currentlyPlayingId.Value = "";
                _currentlyPlayingSongName.Value = "";
                _currentlyPlayingArtistsNames.Value = [];
            }
            return true;
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();

            var tokenResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeTokenRequest(
                  _clientId, _clientSecret, response.Code, new Uri("http://localhost:5543/callback")
              )
            );

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(_clientId, _clientSecret, tokenResponse));
            _spotify = new SpotifyClient(config);

            // QueueModeLoop();
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
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

                await _cachedApiResponses.RefreshCachedApiResponses(_spotify);
                var context = _cachedApiResponses.Context;
                var queue = _cachedApiResponses.Queue;

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
                        this._currentlyPlayingId.Value = currentlyPlayingId;
                        this._currentlyPlayingSongName.Value = currentlyPlaying?.Name;
                        this._currentlyPlayingArtistsNames.Value = currentlyPlaying?.Artists;

                        // set these to false, meaning these actions are
                        // queued to happen (will be set to true when the action is taken
                        // so the action only happens once)
                        _addedToQueue = false;
                        _removedFromPlaylist = false;
                    }

                    // get remaining duration of current song, next song in queue
                    double? remainingDuration = GetRemainingDuration(queue, context);
                    var nextInQueue = GetNextInQueue(queue);
                    string? nextInQueueId = nextInQueue?.Id;

                    // if a certain duration remains in the current song
                    if (remainingDuration != null && remainingDuration < QueueModeRemainingDurationThreshold)
                    {
                        // get queue playlist
                        var playlist = await _spotify.Playlists.Get(_playlistId);

                        // get first song in queue playlist
                        var firstInPlaylist = GetFirstInPlaylist(playlist);
                        string? firstInPlaylistId = firstInPlaylist?.Id;
                        string? firstInPlaylistUri = firstInPlaylist?.Uri;

                        // if first song in queue playlist is currently playing, remove it from head of playlist
                        // (do this only once until currently playing song changes)
                        if (!_removedFromPlaylist && currentlyPlayingId != null && currentlyPlayingId == firstInPlaylistId)
                        {
                            _removedFromPlaylist = true;
                            await RemoveFirstSongFromPlaylist(playlist, _playlistId);
                        }
                        // if first song in queue playlist is NOT currently playing, add first song in queue playlist to queue
                        // (do this only once until currently playing song changes)
                        else if (QueueMode && !_addedToQueue && nextInQueueId != null && firstInPlaylistUri != null && nextInQueueId != firstInPlaylistId)
                        {
                            _addedToQueue = true;
                            await _spotify.Player.AddToQueue(new PlayerAddToQueueRequest(firstInPlaylistUri));
                        }
                    }
                }
            }
        }

        private async Task RemoveFirstSongFromPlaylist(FullPlaylist playlist, string playlistId)
        {
            IList<int>? indicesToRemove = new List<int> { 0, };
            string? snapshotId = playlist.SnapshotId;
            await _spotify.Playlists.RemoveItems(playlistId, new PlaylistRemoveItemsRequest { Positions = indicesToRemove, SnapshotId = snapshotId });
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

        public class ThreadSafeObject<T>
        {
            private readonly object _lockObject = new();
            private T _value;

            public T Value
            {
                get
                {
                    lock (_lockObject)
                    {
                        return _value;
                    }
                }
                set
                {
                    lock (_lockObject)
                    {
                        this._value = value;
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
                    return _contextCached.Value;
                }
            }

            public QueueResponse Queue
            {
                get
                {
                    return _queueCached.Value;
                }
            }

            private readonly TimeSpan _callAgainTimeout = TimeSpan.FromSeconds(6);
            private DateTime _timeOfLastCall = DateTime.MinValue;

            private ThreadSafeObject<CurrentlyPlayingContext> _contextCached = new();
            private ThreadSafeObject<QueueResponse> _queueCached = new();

            public async Task<bool> RefreshCachedApiResponses(SpotifyClient spotify)
            {
                DateTime now = DateTime.UtcNow;
                DateTime timeoutTime = _timeOfLastCall.Add(_callAgainTimeout);
                if (timeoutTime.CompareTo(now) < 0)
                {
                    _timeOfLastCall = now;
                    var context = spotify.Player.GetCurrentPlayback();
                    var queue = spotify.Player.GetQueue();

                    _contextCached.Value = await context;
                    _queueCached.Value = await queue;
                }
                return true;
            }
        }
    }
}