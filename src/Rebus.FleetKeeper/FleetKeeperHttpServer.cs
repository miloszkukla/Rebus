﻿using System;
using System.Net;
using System.Reactive.Linq;

namespace Rebus.FleetKeeper
{
    internal class FleetKeeperHttpServer : IDisposable
    {
        readonly IObservable<Diagnostics> diagnostics;
        IDisposable subscription;

        public FleetKeeperHttpServer(IObservable<Diagnostics> diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        public void Start()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            subscription = Observable.FromAsync(listener.GetContextAsync)
                                     .Repeat()
                                     .Retry()
                                     .Subscribe(context =>
                                     {
                                         switch (context.Request.RawUrl)
                                         {
                                             case "/":
                                                 File(context, "..\\..\\Client\\index.html");
                                                 break;
                                             case "/stream":
                                                 break;
                                             default:
                                                 File(context, "..\\..\\Client" + context.Request.RawUrl);
                                                 break;
                                         }
                                     });
        }

        static void File(HttpListenerContext context, string filename)
        {
            var bytes = System.IO.File.ReadAllBytes(filename);
            var hasBOM = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var skip = (hasBOM) ? 3 : 0;
            context.Response.OutputStream.Write(bytes, skip, bytes.Length - skip);
            context.Response.Close();
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}