using CommunicationBase;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PLCCommunication_Base.Modbus
{
    /// <summary>
    /// 基础Modbus地址转换器，标准的Modbus输入的数据
    ///0 开头 (如 000001)：输出线圈 (Coils)
    ///1 开头 (如 100001)：输入线圈(Discrete Inputs)
    ///3 开头 (如 300001)：内部寄存器(Input Registers)
    ///4 开头 (如 400001)：保持寄存器(Holding Registers)
    /// </summary>
    public class ModbusAddressConverter_Base : IInputConverter<ModbusDecodeData>
    {
        public DataEndianKind DataEndian { get; set; }

        // 正则：[0,1,2,3] 5位数字
        static readonly Regex s_addrRegex = new Regex(@"^(?<Type>[0134])(?<Offset>\d{5})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        object IInputConverter.Decode(string val) => Decode(val);
        public ModbusDecodeData Decode(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                throw new ArgumentException("地址字符串不能为空", nameof(val));

            val = val.Trim();

            // 正则拆分前缀 + 数字
            var match = s_addrRegex.Match(val);
            if (!match.Success)
                throw new FormatException($"Modbus地址格式不正确: \"{val}\"（期望: [0,1,2,3] 5位数字）");

            var prefix = match.Groups["Type"].Value;  // 比如 1,2,3,4
            var addrStr = match.Groups["Offset"].Value;   // 比如 "00100" / "10000"

            if (!ushort.TryParse(addrStr, out ushort addr))
                throw new FormatException($"地址数字超出范围或无法解析: \"{addrStr}\"");
            ModbusArea area;

            switch (prefix)
            {
                case "0":
                    area = ModbusArea.Coil;
                    break;
                case "1":
                    area = ModbusArea.DiscreteInput;
                    break;
                case "3":
                    area = ModbusArea.InputRegister;
                    break;
                case "4":
                    area = ModbusArea.HoldingRegister;
                    break;
                default:
                    throw new FormatException($"无法根据前缀 \"{prefix}\" 判断Modbus区段，请检查格式");
            }

            return new ModbusDecodeData()
            {
                Area = area,
                GroupStr = prefix,
                Address = addr,
            };
        }
    }

    /// <summary>
    /// 通用的Modbus地址转换器
    /// </summary>
    public class ModbusAddressConverter_General : IInputConverter<ModbusDecodeData>
    {
        /// <summary>
        /// 线圈标识符集合 Coils
        /// </summary>
        public List<string> Flag_Coils { get; set; } = new List<string>();

        /// <summary>
        /// 只读输入标识符集合 Inputs
        /// </summary>
        public List<string> Flag_Inputs { get; set; } = new List<string>();

        /// <summary>
        /// 只读输入量
        /// </summary>
        public List<string> Flag_InRegisters { get; set; } = new List<string>();

        /// <summary>
        /// 保持寄存器标识符集合
        /// </summary>
        public List<string> Flag_HoldingRegisters { get; set; } = new List<string>();

        public ushort CoilOffset { get;set; }
        public ushort InputOffset { get; set; }
        public ushort InRegisterOffset { get; set; }
        public ushort HoldingRegisterOffset { get; set; }
        public DataEndianKind DataEndian { get; set; } 

        // 正则：前缀 + 数字
        static readonly Regex s_addrRegex = new Regex(@"^(?<prefix>[A-Za-z]+)(?<addr>\d+)$",RegexOptions.Compiled | RegexOptions.CultureInvariant);
        object IInputConverter.Decode(string val) => Decode(val);
        public ModbusDecodeData Decode(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                throw new ArgumentException("地址字符串不能为空", nameof(val));

            val = val.Trim();

            // 正则拆分前缀 + 数字
            var match = s_addrRegex.Match(val);
            if (!match.Success)
                throw new FormatException($"Modbus地址格式不正确: \"{val}\"（期望: 前缀+数字，如 M100 / D100）");

            var prefix = match.Groups["prefix"].Value;  // 比如 "M" / "D" / "R"
            var addrStr = match.Groups["addr"].Value;   // 比如 "100" / "10000"


            if (!ushort.TryParse(addrStr, out ushort addr))
                throw new FormatException($"地址数字超出范围或无法解析: \"{addrStr}\"");
            ModbusArea area;
            if (Flag_Coils.Contains(prefix))
            {
                area = ModbusArea.Coil;
                addr = (ushort)(addr + CoilOffset);
            }
            else if (Flag_HoldingRegisters.Contains(prefix))
            {
                area = ModbusArea.HoldingRegister;
                addr = (ushort)(addr + HoldingRegisterOffset);
            }
            else if (Flag_Inputs.Contains(prefix))
            {
                area = ModbusArea.DiscreteInput;
                addr = (ushort)(addr + InputOffset);
            }
            else if (Flag_InRegisters.Contains(prefix))
            {
                area = ModbusArea.InputRegister;
                addr = (ushort)(addr + InRegisterOffset);
            }
            else
            {
                throw new FormatException($"无法根据前缀 \"{prefix}\" 判断Modbus区段，请检查Flag_*配置");
            }

            return new ModbusDecodeData()
            {
                Area = area,
                GroupStr = prefix,
                Address = addr,
            };
        }
    }
}
