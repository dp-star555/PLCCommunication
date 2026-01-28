using CommunicationBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceCommunicationBase
{
    public sealed class AutoReadOptions
    {
        /// <summary>
        /// 自动读写的间隔时间/ms
        /// </summary>
        public int IntervalMs { get; set; } = 150;
    }

    internal class DeviceAutoReader:IDisposable
    {
        private readonly ICommunication mDevice;
        public AutoReadOptions Options { get; }
        private CancellationTokenSource mCts;
        private Task mLoop;

        public bool IsRunning => mLoop != null;

        public DeviceAutoReader(ICommunication device, AutoReadOptions options = null)
        {
            mDevice = device ?? throw new ArgumentNullException(nameof(device));
            Options = options ?? new AutoReadOptions();
        }

        public void Start(CancellationToken externalCt = default)
        {
            if (IsRunning) return;

            mCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            mLoop = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                while (!mCts.IsCancellationRequested)
                {
                    sw.Restart();
                    await mDevice.ReadAsync(mCts.Token).ConfigureAwait(false);

                    int sleep = Options.IntervalMs - (int)sw.ElapsedMilliseconds;
                    if (sleep > 0)
                        await Task.Delay(sleep, mCts.Token).ConfigureAwait(false);
                }
            });

        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;
            mCts.Cancel();
            try { await mLoop.ConfigureAwait(false); }
            catch { }
            finally
            {
                mLoop = null;
                mCts.Dispose();
                mCts = null;
            }
        }

        public void Dispose()
        {
            if (IsRunning) StopAsync().GetAwaiter().GetResult();
        }
    }
}
