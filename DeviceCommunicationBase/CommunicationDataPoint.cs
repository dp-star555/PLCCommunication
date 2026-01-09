using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationBase
{
    public delegate void ValueChangedDelegate(DeviceValue value);

    /// <summary>
    /// 用于传输设备数据的零时结构体
    /// </summary>
    public struct DeviceValue : IDisposable
    {
        public bool? BOOL;
        public byte? BYTE;
        public Int16? INT16;
        public UInt16? UINT16;
        public Int32? INT32;
        public UInt32? UINT32;
        public Single? SINGLE;
        public double? DOUBLE;
        public String STRING;
        public object CLASS;

        public override string ToString()
        {
            if (BOOL.HasValue) return BOOL.Value.ToString();
            if (BYTE.HasValue) return BYTE.Value.ToString();
            if (INT16.HasValue) return INT16.Value.ToString();
            if (UINT16.HasValue) return UINT16.Value.ToString();
            if (INT32.HasValue) return INT32.Value.ToString();
            if (UINT32.HasValue) return UINT32.Value.ToString();
            if (SINGLE.HasValue) return SINGLE.Value.ToString();
            if (DOUBLE.HasValue) return DOUBLE.Value.ToString();
            if (!string.IsNullOrEmpty(STRING)) return STRING;
            if (CLASS != null) return CLASS.ToString();

            return string.Empty; // 都没有值
        }

        public void Dispose()
        {
            if (CLASS != null && CLASS is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public interface ICommunicationDataPoint
    {
        /// <summary>
        /// 隶属的设备
        /// </summary>
        DeviceCommunication Panel { set; }
        /// <summary>
        /// 时间变更事件
        /// </summary>
        event ValueChangedDelegate OnValueChanged;
        /// <summary>
        /// 点位名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 输入点位信息
        /// </summary>
        string Input { get; }
        /// <summary>
        /// 点位类型
        /// </summary>
        DataType DataType { get; set; }
        /// <summary>
        /// 最后一次变更触发的计数
        /// </summary>
        int LastGeneration { get; set; }
        /// <summary>
        /// 获取点位数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        DeviceValue GetValue();
        /// <summary>
        /// 写点位数据
        /// </summary>
        /// <param name="val"></param>
        void SetValue(object val);
        /// <summary>
        /// 获取实际的数据占用长度
        /// </summary>
        /// <returns></returns>
        ushort GetLength();
        /// <summary>
        /// 获取数据变更多播委托
        /// </summary>
        /// <returns></returns>
        ValueChangedDelegate GetValChangeDel();
    }

}
