using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Serilog;

namespace SAM.Picker
{
    internal class LogoReadyEventArgs : EventArgs
    {
        public GameInfo Game { get; }
        public Bitmap Bitmap { get; }

        public LogoReadyEventArgs(GameInfo game, Bitmap bitmap)
        {
            Game = game;
            Bitmap = bitmap;
        }
    }

    internal class LogoDownloadService
    {
        public event EventHandler<LogoReadyEventArgs> LogoReady;

        private const int MaxParallelDownloads = 6;

        private readonly object _Lock = new object();
        private readonly ConcurrentQueue<GameInfo> _Queue = new ConcurrentQueue<GameInfo>();
        private readonly HashSet<string> _Attempting = new HashSet<string>();
        private readonly HashSet<string> _Attempted = new HashSet<string>();
        private int _ActiveDownloads;

        public int Remaining
        {
            get
            {
                lock (_Lock)
                    return _ActiveDownloads + _Queue.Count;
            }
        }

        public void Enqueue(GameInfo info)
        {
            if (string.IsNullOrEmpty(info.ImageUrl))
                return;

            lock (_Lock)
            {
                if (_Attempting.Contains(info.ImageUrl) || _Attempted.Contains(info.ImageUrl))
                    return;

                _Attempting.Add(info.ImageUrl);
                _Queue.Enqueue(info);
            }
        }

        public void ClearQueue()
        {
            lock (_Lock)
                _Attempting.Clear();
            while (_Queue.TryDequeue(out _)) { }
        }

        public void ProcessQueue(Func<GameInfo, bool> shouldDownload)
        {
            bool startDownloads;
            lock (_Lock)
            {
                startDownloads = _ActiveDownloads < MaxParallelDownloads && _Queue.Count > 0;
            }

            if (!startDownloads) return;

            lock (_Lock)
            {
                while (_ActiveDownloads < MaxParallelDownloads)
                {
                    GameInfo info = DequeueNext(shouldDownload);
                    if (info == null) break;

                    _ActiveDownloads++;
                    _Attempted.Add(info.ImageUrl);

                    var captured = info;
                    Task.Run(() => DownloadTask(captured));
                }
            }
        }

        private GameInfo DequeueNext(Func<GameInfo, bool> shouldDownload)
        {
            while (_Queue.TryDequeue(out var info))
            {
                if (shouldDownload == null || shouldDownload(info))
                    return info;

                _Attempting.Remove(info.ImageUrl);
            }
            return null;
        }

        private void DownloadTask(GameInfo info)
        {
            Bitmap bitmap = null;
            try
            {
                using (var client = new WebClient())
                {
                    var data = client.DownloadData(new Uri(info.ImageUrl));
                    using (var stream = new MemoryStream(data, false))
                    {
                        bitmap = new Bitmap(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to download logo for appId {AppId} from {Url}", info.Id, info.ImageUrl);
            }

            lock (_Lock)
                _ActiveDownloads--;

            LogoReady?.Invoke(this, new LogoReadyEventArgs(info, bitmap));
        }
    }
}
