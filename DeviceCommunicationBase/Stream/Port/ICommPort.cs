using DeviceCommunicationBase.Stream.FrameSplitter;
using System;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 物理端口类型：网口 / 串口 / 其他
    /// </summary>
    public enum PortType
    {
        /// <summary>
        /// 网口
        /// </summary>
        Ethernet,
        /// <summary>
        /// 串口
        /// </summary>
        Serial,
    }

    /// <summary>
    /// 底层物理端口统一抽象：不关心协议，只负责收发字节
    /// </summary>
    public interface ICommPort : IDisposable
    {
        /// <summary>
        /// 端口类型
        /// </summary>
        PortType PortType { get; }

        /// <summary>
        /// 端口名称，用于标识。如 "COM3" 或 "192.168.0.10:502"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 用于自动分包的分封包器(默认通过\r\n进行分包)
        /// </summary>
        IFrameSplitter FrameSplitter { set; }

        /// <summary>
        /// 当前是否已打开
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 打开
        /// </summary>
        bool Connect();
        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        bool ReConnect();
        /// <summary>
        /// 关闭
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 写入数据,只发送，不等待结果
        /// </summary>
        Task<bool> WriteOnlyAsync(byte[] buffer);

        /// <summary>
        /// 写入数据,等待结果
        /// </summary>
        Task<byte[]> WriteRequestAsync(byte[] buffer, int timeoutMs = 1000);

        /// <summary>
        /// 原始数据回调（收到任何数据时触发）
        /// </summary>
        event Action<ICommPort, ReadOnlyMemory<byte>> OnDataReceived;
        /// <summary>
        /// 链接回调
        /// </summary>
        event Action<ICommPort> OnConnect;
        /// <summary>
        /// 断连回调
        /// </summary>
        event Action<ICommPort> OnDisconnect;
    }
}
