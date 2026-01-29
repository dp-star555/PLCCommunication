using DeviceCommunicationBase;
using DeviceCommunicationBase.Stream;
using Prism.Ioc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CommunicationBase
{
    public enum DeviceProtocolType
    {
        ModbusTCP,
        ModbusRTU,
        OPCUA,
        MC3E,
        PROFINET,
        ETHERNETIP,
        BACnet
    }

    /// <summary>
    /// 数据类型
    /// </summary>
    public enum DataType
    {
        BIT,
        BYTE,
        INT16,
        UINT16,
        INT32,
        UINT32,
        SINGLE,
        DOUBLE,
        UTF32,
        ASCII
    }

    public interface ICanAutoRead
    {
        bool CanAutoRead { get; }
    }

    /// <summary>
    /// 用于设备读写的接口
    /// </summary>
    public interface ICommunication
    {
        /// <summary>
        /// 点位索引器
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        ICommunicationDataPoint this[string name] { get; }
        /// <summary>
        /// 名称
        /// </summary>
        string Name { get; }
        /// <summary>
        /// 设备ID
        /// </summary>
        string ID { get; }
        /// <summary>
        /// 是否链接
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// 设备通讯的类型
        /// </summary>
        DeviceProtocolType CommunicationType { get; }
        /// <summary>
        /// 用于单次的读写的超时时间设置，单位毫秒
        /// </summary>
        int TimeOutMs { get; set; }
        /// <summary>
        /// 用于解析输入字符串的解析器
        /// </summary>
        IInputConverter InputConverter { get; set; }
        /// <summary>
        /// 用于数据解析转换真实值的转换器
        /// </summary>
        IValueDecoder ValueDecoder { get; set; }
        /// <summary>
        /// 链接
        /// </summary>
        /// <returns></returns>
        Task Connect();
        /// <summary>
        /// 断链
        /// </summary>
        void Disconnect();
        /// <summary>
        /// 异步读取所有数据
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task ReadAsync(CancellationToken ct = default);
        /// <summary>
        /// 异步读取单个数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default);
        /// <summary>
        /// 连续批量写入数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objects"></param>
        void Write(params (ICommunicationDataPoint dp, object value)[] pvs);
        /// <summary>
        /// 写入单点数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        void Write(ICommunicationDataPoint dp, object value);
        /// <summary>
        /// 异步连续批量写入数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objects"></param>
        Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs);
        /// <summary>
        /// 异步写入单点数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        Task WriteAsync(ICommunicationDataPoint dp, object value);
    }

    public abstract class DeviceCommunication : ICommunication, ICanAutoRead
    {
        public DeviceCommunication(IContainerProvider container) 
        {
            mChangedMge = container.Resolve<IValueChangedMge>();
        }

        protected IValueChangedMge mChangedMge;

        public abstract ICommunicationDataPoint this[string name] { get; }

        public string Name { get; set; }

        public string ID { get; set; }

        public bool IsConnected { get; set; }

        public int TimeOutMs { get; set; }

        public virtual IInputConverter InputConverter { get; set; }

        public IValueDecoder ValueDecoder { get; set; } = new WordValueDecoder();

        public abstract DeviceProtocolType CommunicationType { get; }

        public abstract bool CanAutoRead { get; }

        public abstract Task Connect();

        public abstract void Disconnect();

        public abstract Task ReadAsync(CancellationToken ct = default);

        public abstract Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default);

        public abstract void Write(params (ICommunicationDataPoint dp, object value)[] pvs);

        public abstract void Write(ICommunicationDataPoint dp, object value);

        public abstract Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs);

        public abstract Task WriteAsync(ICommunicationDataPoint dp, object value);


    }
}
