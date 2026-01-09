using CommunicationBase;
using DeviceDataCommunication_Modbus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCCommunication_Base.INOVANCE
{
    /// <summary>
    /// 支持汇川小型plc-H5U,Easy的通讯交互
    /// 支持自动转换点位：
    /// 线圈：     M,B,S,X,Y
    /// 寄存器：   D.R 
    /// </summary>
    public class ModbusTCP_INOV_Small: ModbusTCP_Device
    {
        public ModbusTCP_INOV_Small() 
        {
            mCoils = new bool[65535];
            mHoldingRegisters = new ushort[45055];
            ModbusAddressConverter_General center = new ModbusAddressConverter_General();
            center.Flag_Coils.AddRange(new string[] { "M" , "B", "S", "X", "Y" });
            center.Flag_HoldingRegisters.AddRange(new string[] { "D", "R" });
            mConverter = center;
        }
        public override void SetNum(ushort coilNum, ushort discreteInputsNum, ushort holdingRegistersNum, ushort inputRegistersNum)
        {
            throw new NotImplementedException("具体实现，已不支持手动写入长度");
        }

        ModbusAddressConverter_General mConverter;

        public override IInputConverter InputConverter 
        {
            get { return mConverter; }
            set { throw new NotImplementedException("具体实现，已不支持手动写入转换器"); }
        }
    }
}
