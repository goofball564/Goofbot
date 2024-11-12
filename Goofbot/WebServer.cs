using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace Goofbot
{
    public class WebServer
    {
        private HttpListener listener;

        public WebServer(string uri)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(uri);
        }

        public async Task<String> Listen()
        {
            listener.Start();
            return await onRequest();
        }

        private async Task<String> onRequest()
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
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
                        writer.WriteLine("No code found in query string!");
                        writer.Flush();
                    }
                }
            }
            return null;
        }
    }
}
