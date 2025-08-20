using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace Goofbot.Utils
{
    public class WebServer
    {
        private HttpListener _listener;

        public WebServer(string uri)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(uri);
        }

        public void Close()
        {
            _listener.Close();
        }

        public async Task<string> Listen()
        {
            _listener.Start();
            string code = await OnRequest();
            _listener.Stop();
            return code;
        }

        private async Task<string> OnRequest()
        {
            while (_listener.IsListening)
            {
                var ctx = await _listener.GetContextAsync();
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
