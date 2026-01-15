using HPSocket;
using HPSocket.Adapter;
using HPSocket.Base;
using HPSocket.Sdk;
using HPSocket.Tcp;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeviceCommunicationBase
{
    /// <summary>
    /// 简单的 TCP 网口实现
    /// </summary>
    public class HPSocketPort_Client : ICommPort
    {
        public HPSocketPort_Client()
        {
            mClient = new TcpClient();
            // 绑定事件
            mClient.OnConnect += OnClientConnect;
            mClient.OnReceive += OnClientReceive;
            mClient.OnClose += OnClientClose;

            mFrameSplitter.FrameCompleted += OnFrameCompleted;
        }

        public HPSocketPort_Client(string ip, ushort port): this() 
        {
            RemoteIp = ip;
            RemotePort = port;
        }

        // 接收缓冲
        private readonly ConcurrentQueue<byte[]> mFrameQueue = new ConcurrentQueue<byte[]>();
        private readonly AutoResetEvent mFrameArrived = new AutoResetEvent(false);
        private readonly TcpClient mClient = null;

        private volatile bool mReadEnabled = false;
        string mRemoteIp = "127.0.0.1";
        ushort mRemotePort = 502;
        IFrameSplitter mFrameSplitter = DelimiterFrameSplitter.FromString("\r\n");  

        public event Action<ICommPort, ReadOnlyMemory<byte>> OnDataReceived;
        public event Action<ICommPort> OnDisconnect;
        public event Action<ICommPort> OnConnect;

        /// <summary>
        /// IP地址
        /// </summary>
        public string RemoteIp { get { return mRemoteIp; } set { mRemoteIp = value; } }
        /// <summary>
        /// 端口
        /// </summary>
        public ushort RemotePort { get { return mRemotePort; } set { mRemotePort = value; } }

        public PortType PortType => PortType.Ethernet;

        public string Name => $"{RemoteIp}:{RemotePort}";

        public bool IsOpen { get { return mClient == null ? false : mClient.IsConnected; } }

        public IFrameSplitter FrameSplitter { 
            set 
            {
                mFrameSplitter.FrameCompleted -= OnFrameCompleted;
                mFrameSplitter = value;
                mFrameSplitter.Reset();
                mFrameSplitter.FrameCompleted += OnFrameCompleted;
            }
        }

        private HandleResult OnClientConnect(IClient sender)
        {
            OnConnect?.Invoke(this);
            return HandleResult.Ok;
        }

        private HandleResult OnClientClose(IClient sender, SocketOperation so, int errorCode)
        {
            OnDisconnect?.Invoke(this);
            return HandleResult.Ok;
        }

        private HandleResult OnClientReceive(IClient sender, byte[] data)
        {
            // 由分包器来判断何时构成“完整一帧”
            mFrameSplitter.Feed(data);
            return HandleResult.Ok;
        }

        /// <summary>
        /// 分包器判定“完成一帧”后触发
        /// </summary>
        private void OnFrameCompleted(ReadOnlyMemory<byte> frame)
        {
            if (mReadEnabled)//同步读取
            {
                // 入队（供同步 Read 使用）
                mFrameQueue.Enqueue(frame.ToArray());
                // 通知 Read 有新帧到达
                mFrameArrived.Set();
                mReadEnabled = false;
            }

            // 抛给外部事件
            OnDataReceived?.Invoke(this, frame);
        }

        public void Disconnect()
        {
            if (IsOpen)
            {
                mClient.Stop();
            }
        }

        public bool Connect()
        {
            if (IsOpen) return true;

           
            bool startOk = mClient.Connect(RemoteIp, RemotePort);
            if (!startOk)
                return false;

            // 简单等待，防止立即调用 Write 失败
            int waitMs = 500;
            int start = Environment.TickCount;

            while (!IsOpen && Environment.TickCount - start < waitMs)
            {
                Thread.Sleep(10);
            }

            return IsOpen;
        }

        public bool ReConnect()
        {
            Disconnect();
            return Connect();
        }

        public int Read(Span<byte> buffer, int timeoutMs = 1000)
        {
            if (!IsOpen && !Connect())
                return 0;
            while (mFrameQueue.TryDequeue(out _)) { }
            int deadline = Environment.TickCount + timeoutMs;
            mReadEnabled = true;
            mFrameArrived.Reset();
            try
            {
                while (Environment.TickCount < deadline)
                {
                    if (mFrameQueue.TryDequeue(out var frame))
                    {
                        int bl = buffer.Length;
                        int fl = frame.Length;
                        if (bl < fl)
                        {
                            throw new Exception($"{Name}中当前空间大小{bl} < 结果大小{fl}");
                        }
                        new ReadOnlySpan<byte>(frame, 0, fl).CopyTo(buffer);
                        return fl; // 返回实际拷贝的字节数
                    }

                    int remain = deadline - Environment.TickCount;
                    if (remain <= 0)
                        break;

                    mFrameArrived.WaitOne(Math.Min(remain, 50));
                }
            }
            finally
            { 
                mReadEnabled = false;
                while (mFrameQueue.TryDequeue(out _)) { }
                mFrameArrived.Reset();
            }
            return 0; // 超时无帧
        }

        public bool Write(ReadOnlySpan<byte> buffer)
        {
            if (!IsOpen && !Connect())
                return false;

            var data = buffer.ToArray();
            bool ok = mClient.Send(data, data.Length);
            if (!ok)
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            Disconnect();
            mFrameArrived.Dispose();
        }
    }
}
