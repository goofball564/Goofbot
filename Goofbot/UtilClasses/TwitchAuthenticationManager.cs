namespace Goofbot.UtilClasses;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Models;

internal class TwitchAuthenticationManager : IDisposable
{
    public const string TwitchAppRedirectUrl = "http://localhost:3000/";
    public const string TwitchAuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
    public const string TwitchTokenRequestUrl = "https://id.twitch.tv/oauth2/token";

    private static readonly List<string> BotScopes = ["user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit", "moderator:manage:banned_users"];
    private static readonly List<string> ChannelScopes = ["channel:bot", "channel:read:redemptions", "channel:manage:redemptions"];

    private readonly HttpClient httpClient = new ();
    private readonly WebServer server = new (TwitchAppRedirectUrl);

    private readonly SemaphoreSlim botTokensSemaphore = new (1, 1);
    private readonly SemaphoreSlim channelTokensSemaphore = new (1, 1);

    private readonly Bot bot;
    private readonly TwitchClient twitchClient;
    private readonly TwitchAPI twitchChannelAPI;
    private readonly TwitchAPI twitchBotAPI;
    private readonly string twitchClientID;
    private readonly string twitchClientSecret;

    private string twitchBotAccessToken;
    private string twitchChannelAccessToken;

    public TwitchAuthenticationManager(Bot bot, TwitchClient twitchClient, TwitchAPI twitchChannelAPI, TwitchAPI twitchBotAPI, string twitchClientID, string twitchClientSecret)
    {
        this.bot = bot;
        this.twitchClient = twitchClient;
        this.twitchChannelAPI = twitchChannelAPI;
        this.twitchBotAPI = twitchBotAPI;
        this.twitchClientID = twitchClientID;
        this.twitchClientSecret = twitchClientSecret;
    }

    public void Dispose()
    {
        this.botTokensSemaphore.Dispose();
        this.channelTokensSemaphore.Dispose();
        this.httpClient.Dispose();
        this.server.Dispose();
    }

    public async Task InitializeAsync()
    {
        this.twitchBotAccessToken = await this.RefreshTwitchAccessToken(true);
        this.twitchChannelAccessToken = await this.RefreshTwitchAccessToken(false);

        this.twitchChannelAPI.Settings.ClientId = this.twitchClientID;
        this.twitchChannelAPI.Settings.AccessToken = this.twitchChannelAccessToken;

        this.twitchBotAPI.Settings.ClientId = this.twitchClientID;
        this.twitchBotAPI.Settings.AccessToken = this.twitchBotAccessToken;

        var credentials = new ConnectionCredentials(this.bot.TwitchBotUsername, this.twitchBotAccessToken);
        this.twitchClient.Initialize(credentials, this.bot.TwitchChannelUsername);
    }

    private async Task<string> RefreshTwitchAccessToken(bool botToken)
    {
        string code = await this.RequestTwitchAuthorizationCode(botToken);

        string accessToken;
        SemaphoreSlim semaphore = botToken ? this.botTokensSemaphore : this.channelTokensSemaphore;
        await semaphore.WaitAsync();
        try
        {
            string tokensString = await this.RequestTwitchTokensWithAuthorizationCode(code);

            string tokensFile = botToken ? "bot_tokens.json" : "channel_tokens.json";
            tokensFile = Path.Join(this.bot.StuffFolder, tokensFile);
            File.WriteAllText(tokensFile, tokensString);

            dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
            accessToken = Convert.ToString(tokensObject.access_token);
        }
        finally
        {
            semaphore.Release();
        }

        return accessToken;
    }

    private async Task<string> RequestTokensWithRefreshToken(string refreshToken)
    {
        var values = new Dictionary<string, string>
        {
            { "refresh_token", refreshToken },
            { "client_id", Convert.ToString(this.twitchClientID) },
            { "client_secret", Convert.ToString(this.twitchClientSecret) },
            { "grant_type", "refresh_token" },
        };
        var content = new FormUrlEncodedContent(values);
        var response = await this.httpClient.PostAsync(TwitchTokenRequestUrl, content);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> RequestTwitchTokensWithAuthorizationCode(string twitchAuthorizationCode)
    {
        var values = new Dictionary<string, string>
        {
            { "code", twitchAuthorizationCode },
            { "client_id", Convert.ToString(this.twitchClientID) },
            { "client_secret", Convert.ToString(this.twitchClientSecret) },
            { "grant_type", "authorization_code" },
            { "redirect_uri", TwitchAppRedirectUrl },
        };
        var content = new FormUrlEncodedContent(values);
        var response = await this.httpClient.PostAsync(TwitchTokenRequestUrl, content);
        return await response.Content.ReadAsStringAsync();
    }

    private string GetTwitchAuthorizationCodeRequestUrl(List<string> scopes)
    {
        return $"{TwitchAuthorizationCodeRequestUrlBase}?" +
               $"client_id={this.twitchClientID}&" +
               $"redirect_uri={System.Web.HttpUtility.UrlEncode(TwitchAppRedirectUrl)}&" +
               "response_type=code&" +
               $"scope={string.Join('+', scopes)}";
    }

    private async Task<string> RequestTwitchAuthorizationCode(bool useChrome)
    {
        if (useChrome)
        {
            Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", this.GetTwitchAuthorizationCodeRequestUrl(BotScopes));
        }
        else
        {
            Process.Start(new ProcessStartInfo { FileName = this.GetTwitchAuthorizationCodeRequestUrl(ChannelScopes), UseShellExecute = true });
        }

        return await this.server.Listen();
    }
}
