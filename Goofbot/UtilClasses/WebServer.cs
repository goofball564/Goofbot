namespace Goofbot.UtilClasses;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System;

public class WebServer : IDisposable
{
    private readonly HttpListener listener = new ();
    private readonly SemaphoreSlim semaphore = new (1, 1);

    public WebServer(string uri)
    {
        this.listener.Prefixes.Add(uri);
    }

    public void Dispose()
    {
        this.semaphore.Dispose();
    }

    public void Close()
    {
        this.listener.Close();
    }

    public async Task<string> Listen()
    {
        string code;
        await this.semaphore.WaitAsync();
        try
        {
            this.listener.Start();
            code = await this.OnRequest();
            this.listener.Stop();
        }
        finally
        {
            this.semaphore.Release();
        }

        return code;
    }

    private async Task<string> OnRequest()
    {
        while (this.listener.IsListening)
        {
            var ctx = await this.listener.GetContextAsync();
            var req = ctx.Request;
            var resp = ctx.Response;

            using (var writer = new StreamWriter(resp.OutputStream))
            {
                if (req.QueryString.AllKeys.Any("code".Contains))
                {
                    return req.QueryString["code"];
                }
                else
                {
                    await writer.WriteLineAsync("No code found in query string!");
                    await writer.FlushAsync();
                }
            }
        }

        return null;
    }
}
