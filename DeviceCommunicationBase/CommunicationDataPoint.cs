using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationBase
{
    public delegate void ValueChangedDelegate(DeviceValue value);

    public enum E_DeviceValueKind
    {
        None, Bool, Byte, Int16, UInt16, Int32, UInt32, Single, Double, String, Object
    }


    /// <summary>
    /// 用于传输设备数据的零时结构体
    /// </summary>
    public struct DeviceValue : IDisposable
    {
        public E_DeviceValueKind Kind { get; }
        public object Value { get; }

        private DeviceValue(E_DeviceValueKind kind, object value)
        {
            Kind = kind;
            Value = value;
        }

        public override string ToString() => Value?.ToString() ?? "<null>";

        public static implicit operator DeviceValue(bool v) => new DeviceValue(E_DeviceValueKind.Bool, v);
        public static implicit operator DeviceValue(byte v) => new DeviceValue(E_DeviceValueKind.Byte, v);
        public static implicit operator DeviceValue(short v) => new DeviceValue(E_DeviceValueKind.Int16, v);
        public static implicit operator DeviceValue(ushort v) => new DeviceValue(E_DeviceValueKind.UInt16, v);
        public static implicit operator DeviceValue(int v) => new DeviceValue(E_DeviceValueKind.Int32, v);
        public static implicit operator DeviceValue(uint v) => new DeviceValue(E_DeviceValueKind.UInt32, v);
        public static implicit operator DeviceValue(float v) => new DeviceValue(E_DeviceValueKind.Single, v);
        public static implicit operator DeviceValue(double v) => new DeviceValue(E_DeviceValueKind.Double, v);
        public static implicit operator DeviceValue(string v) => new DeviceValue(E_DeviceValueKind.String, v);
        public static DeviceValue FromObject(object v) => new DeviceValue(E_DeviceValueKind.Object, v);

        public bool TryGet<T>(out T value)
        {
            if (Value is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        public T As<T>() => Value is T t ? t : throw new InvalidCastException();

        public void Dispose()
        {
            if (Value is IDisposable disp)
            {
                disp.Dispose();
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
