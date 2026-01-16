using CommunicationBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCCommunication_Base.Modbus
{
    public class ModbusDataPoint : ICommunicationDataPoint
    {

        /// <summary>
        /// 数据点回调
        /// </summary>
        ValueChangedDelegate valueChanged;

        DeviceCommunication mPanel;

        public string Name { get; set; } = string.Empty;

        public string Input { get; set; }

        public DataType DataType {get;set;}

        public int StrLength{ get; set; }

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

        /// <summary>
        /// 解码后的数据
        /// </summary>
        public ModbusDecodeData DecodeData { get; set; }

        public int LastGeneration { get; set; }

        public DeviceCommunication Panel { set { mPanel = value; } }

        public event ValueChangedDelegate OnValueChanged
        {
            add
            {
                //防止重复添加
                if (valueChanged != null && valueChanged.GetInvocationList().Contains(value))
                {
                    return;
                }
                valueChanged += value;
            }
            remove
            {
                valueChanged -= value;
            }
        }

        public DeviceValue GetValue()
        {
            //串行读取
            return ((ModbusTCP_Device)mPanel).DecodeValue(this);
        }

        public void SetValue(object val)
        {
            ((ModbusTCP_Device)mPanel).WriteAsync(this, val);
        }

        public ValueChangedDelegate GetValChangeDel()
        {
            return valueChanged;
        }
    }

    /// <summary>
    /// 解析后的数据
    /// </summary>
    public class ModbusDecodeData
    {
        public ModbusArea Area;
        public string GroupStr;
        public ushort Address;
    }
    /// <summary>
    /// 用于Modbus拥有的区域
    /// </summary>
    public enum ModbusArea
    {
        Coil,
        DiscreteInput,
        HoldingRegister,
        InputRegister
    }
}
