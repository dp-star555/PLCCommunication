using CommunicationBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCCommunication_Base.Mitsubishi3E
{
    /// <summary>
    /// MC 链接地址数据
    /// </summary>
    public class Mc3EDataPoint : ICommunicationDataPoint
    {
        ValueChangedDelegate valueChanged_Light;
        ValueChangedDelegate valueChanged_Heavy;

        DeviceCommunication mPanel;

        public DeviceCommunication Panel { set { mPanel = value; } }
        public string Name { get; set; } = string.Empty;
        public MCDecodeData DecodeData { get; set; } = new MCDecodeData();
        public string Input { get; set; }

        public DataType DataType { get; set; }

        public int StrLength { get; set; }

        public ushort GetLength()
        {
            switch (DataType)
            {
                case DataType.BIT:
                case DataType.INT16:
                case DataType.UINT16:
                    return 1;
                case DataType.INT32:
                case DataType.UINT32:
                case DataType.SINGLE:
                    return 2;
                case DataType.DOUBLE:
                    return 4;
                case DataType.UTF32:
                    return (ushort)(StrLength * 2);
                case DataType.ASCII:
                    return (ushort)((StrLength + 2 - 1) / 2);
                default:
                    throw new Exception("当前类型无法对应长度");
            }
        }

        public int LastGeneration { get; set; }

        public void OnValueChanged(ValueChangedDelegate value, E_CallBackWeight weight = E_CallBackWeight.Light)
        {
            switch (weight)
            {
                case E_CallBackWeight.Light:
                    //防止重复添加
                    if (valueChanged_Light != null && valueChanged_Light.GetInvocationList().Contains(value))
                    {
                        return;
                    }
                    valueChanged_Light += value;
                    break;
                case E_CallBackWeight.Heavy:
                    //防止重复添加
                    if (valueChanged_Heavy != null && valueChanged_Heavy.GetInvocationList().Contains(value))
                    {
                        return;
                    }
                    valueChanged_Heavy += value;
                    break;
                default:
                    break;
            }
        }

        public ValueChangedDelegate GetValChangeDel_Light()
        {
            return valueChanged_Light;
        }

        public ValueChangedDelegate GetValChangeDel_Heavy()
        {
            return valueChanged_Heavy;
        }
        public DeviceValue GetValue()
        {
            //串行读取
            return ((Mitsubis3E_Device)mPanel).DecodeValue(this);
        }

        public void SetValue(object val)
        {
            ((Mitsubis3E_Device)mPanel).Write(this, val);
        }
    }


    /// <summary>
    /// 解析后的数据
    /// </summary>
    public class MCDecodeData
    {
        public E_McDeviceCode Area;
        public ushort Address;
    }

    /// <summary>
    /// MC 设备码（3E Binary）
    /// </summary>
    public enum E_McDeviceCode : byte
    {
        // bit
        M = 0x90,
        X = 0x9C,
        Y = 0x9D,
        B = 0xA0,
        // word
        D = 0xA8,
        W = 0xB4,
        R = 0xAF,
    }
}
