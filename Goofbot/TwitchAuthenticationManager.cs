using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Interfaces;

namespace Goofbot
{
    internal class TwitchAuthenticationManager
    {
        public const string TwitchAppRedirectUrl = "http://localhost:3000/";
        public const string TwitchAuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
        public const string TwitchTokenRequestUrl = "https://id.twitch.tv/oauth2/token";

        private static readonly List<string> s_botScopes = ["user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit"];
        private static readonly List<string> s_channelScopes = ["channel:bot", "channel:read:redemptions"];

        private readonly HttpClient _httpClient = new();
        private readonly WebServer _server = new(TwitchAppRedirectUrl);

        private readonly SemaphoreSlim _webServerSemaphore = new(1, 1);
        private readonly SemaphoreSlim _botTokensSemaphore = new(1, 1);
        private readonly SemaphoreSlim _channelTokensSemaphore = new(1, 1);

        private string _twitchClientID;
        private string _twitchClientSecret;
        public string _twitchBotAccessToken; // should be made private
        private string _twitchChannelAccessToken;

        private TwitchClient _twitchClient;
        private TwitchAPI _twitchAPI;

        public TwitchAuthenticationManager(string twitchClientID, string twitchClientSecret, TwitchClient twitchClient, TwitchAPI twitchAPI)
        {
            _twitchClientID = twitchClientID;
            _twitchClientSecret = twitchClientSecret;
            _twitchClient = twitchClient;
            _twitchAPI = twitchAPI;
        }

        public async Task Initialize()
        {
            Task botAccessTokenTask = RefreshTwitchAccessToken(true);
            Task channelAccessTokenTask = RefreshTwitchAccessToken(false);

            await botAccessTokenTask;
            await channelAccessTokenTask;

            _twitchAPI.Settings.ClientId = _twitchClientID;
            _twitchAPI.Settings.AccessToken = _twitchChannelAccessToken;

            var credentials = new ConnectionCredentials(Program.TwitchBotUsername, _twitchBotAccessToken);
            _twitchClient.Initialize(credentials, Program.TwitchChannelUsername);
        }

        private async Task RefreshTwitchAccessToken(bool botToken)
        {
            string tokensFile = botToken ? "bot_tokens.json" : "channel_tokens.json";
            tokensFile = Path.Join(Program.StuffFolder, tokensFile);

            string code;
            await _webServerSemaphore.WaitAsync();
            try
            {
                code = await RequestTwitchAuthorizationCode(botToken);
            }
            finally
            {
                _webServerSemaphore.Release();
            }

            SemaphoreSlim semaphore = botToken ? _botTokensSemaphore : _channelTokensSemaphore;
            await semaphore.WaitAsync();
            try
            {
                string tokensString = await RequestTwitchTokensWithAuthorizationCode(code);

                CancellationToken cancellationToken = new();
                Task writeAllTextTask = File.WriteAllTextAsync(tokensFile, tokensString, cancellationToken);

                dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
                if (botToken)
                {
                    _twitchBotAccessToken = Convert.ToString(tokensObject.access_token);
                }
                else
                {
                    _twitchChannelAccessToken = Convert.ToString(tokensObject.access_token);
                }

                await writeAllTextTask;
            }
            finally
            {
                semaphore.Release();
            }

        }

        private async Task<string> RequestTokensWithRefreshToken(string refreshToken)
        {
            var values = new Dictionary<string, string>
            {
                { "refresh_token", refreshToken },
                { "client_id", Convert.ToString(_twitchClientID) },
                { "client_secret", Convert.ToString(_twitchClientSecret) },
                { "grant_type", "refresh_token" },
            };
            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(TwitchTokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> RequestTwitchTokensWithAuthorizationCode(string twitchAuthorizationCode)
        {
            var values = new Dictionary<string, string>
            {
                { "code", twitchAuthorizationCode },
                { "client_id", Convert.ToString(_twitchClientID) },
                { "client_secret", Convert.ToString(_twitchClientSecret) },
                { "grant_type", "authorization_code" },
                { "redirect_uri", TwitchAppRedirectUrl }
            };
            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(TwitchTokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        private string GetTwitchAuthorizationCodeRequestUrl(List<string> scopes)
        {
            return $"{TwitchAuthorizationCodeRequestUrlBase}?" +
                   $"client_id={_twitchClientID}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(TwitchAppRedirectUrl)}&" +
                   "response_type=code&" +
                   $"scope={String.Join('+', scopes)}";
        }

        private async Task<string> RequestTwitchAuthorizationCode(bool useChrome)
        {
            if (useChrome)
            {
                Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", GetTwitchAuthorizationCodeRequestUrl(s_botScopes));
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = GetTwitchAuthorizationCodeRequestUrl(s_channelScopes), UseShellExecute = true });
            }
            return await _server.Listen();
        }
    }
}
