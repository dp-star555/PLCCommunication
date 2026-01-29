using CommunicationBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DeviceCommunicationBase
{

    public interface IValueChangedMge : IDisposable
    {
        bool IsChannelRunning { get; }
        void Enqueue(ValueChangedDelegate cb, DeviceValue value, E_CallBackWeight weight);
        Task StopAsync();
    } 

    public class DeviceValueChangedMge: IValueChangedMge
    {

        public DeviceValueChangedMge(int lightDop = 2, int lightcapacity = 502, int heavyDop = 1, int heavycapacity = 502) 
        {
            mLightDop = lightDop;
            mHeavyDop = heavyDop;
            // 有界队列，避免内存无限增长
            mLightChannel = Channel.CreateBounded<(ValueChangedDelegate cb, DeviceValue value)>(new BoundedChannelOptions(lightcapacity)
            {
                SingleWriter = false, // 允许多个生产者（多个设备同时写入）
                SingleReader = false,  // 允许多个消费者（并行处理回调）
                FullMode = BoundedChannelFullMode.DropOldest
            });
            mHeavyChannel = Channel.CreateBounded<(ValueChangedDelegate cb, DeviceValue value)>(new BoundedChannelOptions(heavycapacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });


        }

        // 轻重线程消费者数量
        readonly int mLightDop;
        readonly int mHeavyDop;

        // 轻/重通道
        Channel<(ValueChangedDelegate cb, DeviceValue value)> mLightChannel;
        Channel<(ValueChangedDelegate cb, DeviceValue value)> mHeavyChannel;

        CancellationTokenSource mCtsChannel;

        Task[] mLightTasks;
        Task[] mHeavyTasks;

        /// <summary>
        /// 通道是否正在工作
        /// </summary>
        public bool IsChannelRunning { get; private set; }

        /// <summary>
        /// 添加数据到回调通道
        /// </summary>
        /// <param name="cb"></param>
        /// <param name="value"></param>
        public void Enqueue(ValueChangedDelegate cb, DeviceValue value,E_CallBackWeight weight)
        {
            if (cb == null) return;
            EnsureStarted();

            if (weight == E_CallBackWeight.Heavy)
                mHeavyChannel.Writer.TryWrite((cb, value));
            else
                mLightChannel.Writer.TryWrite((cb, value));
        }

        public async Task StopAsync()
        {
            if (!IsChannelRunning) return;

            mLightChannel.Writer.TryComplete();
            mHeavyChannel.Writer.TryComplete();

            try
            {
                mCtsChannel?.Cancel();
                if (mLightTasks?.Length > 0) await Task.WhenAll(mLightTasks);
                if (mHeavyTasks?.Length > 0) await Task.WhenAll(mHeavyTasks);
            }
            finally
            {
                mCtsChannel?.Dispose();
                mCtsChannel = null;
                mLightTasks = null;
                mHeavyTasks = null;
                IsChannelRunning = false;
            }
        }

        readonly object mLock = new object();

        void EnsureStarted()
        {
            if (IsChannelRunning) return;

            lock (mLock)
            {
                if (IsChannelRunning) return;

                mCtsChannel = new CancellationTokenSource();
                mLightTasks = new Task[mLightDop];
                mHeavyTasks = new Task[mHeavyDop];

                for (int i = 0; i < mLightDop; i++)
                    mLightTasks[i] = StartConsumingAsync(mLightChannel, mCtsChannel.Token);

                for (int i = 0; i < mHeavyDop; i++)
                    mHeavyTasks[i] = StartConsumingAsync(mHeavyChannel, mCtsChannel.Token);

                IsChannelRunning = true;
            }
        }

        /// <summary>
        /// 启动等待通道数据的异步线程
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        async Task StartConsumingAsync(Channel<(ValueChangedDelegate cb, DeviceValue value)> channel, CancellationToken ct)
        {
            try
            {
                var reader = channel.Reader;
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            item.cb?.Invoke(item.value);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }
            }
            catch (OperationCanceledException oex)
            {
                // 正常取消
                Console.WriteLine(oex);
            }
            catch (Exception ex)
            {
                // 记录日志
                Console.WriteLine(ex);
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }

    }
}
