using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationBase
{
    /// <summary>
    /// 字数据解码器
    /// </summary>
    public class WordValueDecoder : IValueDecoder
    {
        public byte[] Code(object value, DataType dataType,ushort length, DataEndianKind dataEndian)
        {
            byte[] buffer = null;
            switch (dataType)
            {
                case DataType.INT16:
                    {
                        buffer = new byte[2];
                        short val = Convert.ToInt16(value);
                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteInt16BigEndian(buffer, val);
                        else
                            BinaryPrimitives.WriteInt16LittleEndian(buffer, val);
                    }
                    break;
                case DataType.UINT16:
                    {
                        buffer = new byte[2];
                        ushort val = Convert.ToUInt16(value);
                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteUInt16BigEndian(buffer, val);
                        else
                            BinaryPrimitives.WriteUInt16LittleEndian(buffer, val);
                    }
                    break;
                case DataType.INT32:
                    {
                        buffer = new byte[4];
                        int val = Convert.ToInt32(value);
                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteInt32BigEndian(buffer, val);
                        else
                            BinaryPrimitives.WriteInt32LittleEndian(buffer, val);
                    }
                    break;
                case DataType.UINT32:
                    {
                        buffer = new byte[4];
                        uint val = Convert.ToUInt32(value);
                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteUInt32BigEndian(buffer, val);
                        else
                            BinaryPrimitives.WriteUInt32LittleEndian(buffer, val);
                    }
                    break;
                case DataType.SINGLE:
                    {
                        buffer = new byte[4];
                        float val = Convert.ToSingle(value);
                        // 获取浮点数的二进制整数表示（System Endian）
                        byte[] temp = BitConverter.GetBytes(val);
                        uint raw = BitConverter.ToUInt32(temp, 0);

                        // 根据目标端序写入
                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteUInt32BigEndian(buffer, raw);
                        else
                            BinaryPrimitives.WriteUInt32LittleEndian(buffer, raw);
                    }
                    break;
                case DataType.DOUBLE:
                    {
                        buffer = new byte[8];
                        double val = Convert.ToDouble(value);
                        // 获取双精度的二进制整数表示
                        long raw = BitConverter.DoubleToInt64Bits(val);

                        if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                            BinaryPrimitives.WriteInt64BigEndian(buffer, raw);
                        else
                            BinaryPrimitives.WriteInt64LittleEndian(buffer, raw);
                    }
                    break;
                case DataType.UTF32:
                    {
                        string val = value?.ToString() ?? string.Empty;
                        // 根据端序选择编码器
                        Encoding encoder = dataEndian.HasFlag(DataEndianKind.IsBigEndian)
                            ? new UTF32Encoding(true, false)
                            : new UTF32Encoding(false, false);
                        buffer = encoder.GetBytes(val);
                    }
                    break;
                case DataType.ASCII:
                    {
                        string val = value?.ToString() ?? string.Empty;
                        buffer = Encoding.ASCII.GetBytes(val);
                        //检查是否为奇数长度
                        if (buffer.Length < length)
                        {
                            //调整数组大小（长度+1），会自动用 0x00 填充
                            Array.Resize(ref buffer, length);
                            //补空格 (0x20)
                            //buffer[buffer.Length - 1] = 0x20; 
                        }
                        return buffer;
                    }
                case DataType.BIT:
                default:
                    throw new NotSupportedException($"未实现的 DataType: {dataType}");
            }
            // 处理字内交换 ,每两个字节互换位置 (AB CD -> BA DC)
            if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord) && buffer.Length >= 2)
            {
                for (int i = 0; i < buffer.Length; i += 2)
                {
                    if (i + 1 < buffer.Length)
                    {
                        byte temp = buffer[i];
                        buffer[i] = buffer[i + 1];
                        buffer[i + 1] = temp;
                    }
                }
            }

            return buffer;
        }

        public DeviceValue Decode(ReadOnlySpan<byte> buffer, DataType dataType, DataEndianKind dataEndian)
        {
            Span<byte> temp = buffer.Length <= 256
                ? stackalloc byte[buffer.Length]
                : new byte[buffer.Length];
            switch (dataType)
            {
                case DataType.INT16:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        temp[1] = buffer[0];
                        temp[0] = buffer[1];
                    }
                    else
                    {
                        buffer.Slice(0, 2).CopyTo(temp);

                    }
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        return new DeviceValue { INT16 = BinaryPrimitives.ReadInt16BigEndian(temp) };
                    else
                        return new DeviceValue { INT16 = BinaryPrimitives.ReadInt16LittleEndian(temp) };
                case DataType.UINT16:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        temp[1] = buffer[0];
                        temp[0] = buffer[1];
                    }
                    else
                    {
                        buffer.Slice(0, 2).CopyTo(temp);

                    }
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        return new DeviceValue { UINT16 = BinaryPrimitives.ReadUInt16BigEndian(temp) };
                    else
                        return new DeviceValue { UINT16 = BinaryPrimitives.ReadUInt16LittleEndian(temp) };
                case DataType.INT32:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        int count = buffer.Length / 2;
                        for (global::System.Int32 i = 0; i < count; i++)
                        {
                            temp[1 + i * 2] = buffer[0 + i * 2];
                            temp[0 + i * 2] = buffer[1 + i * 2];
                        }
                    }
                    else
                    {
                        buffer.Slice(0, 4).CopyTo(temp);
                    }
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        return new DeviceValue { INT32 = BinaryPrimitives.ReadInt32BigEndian(temp) };
                    else
                        return new DeviceValue { INT32 = BinaryPrimitives.ReadInt32LittleEndian(temp) };
                case DataType.UINT32:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        int count = buffer.Length / 2;
                        for (global::System.Int32 i = 0; i < count; i++)
                        {
                            temp[1 + i * 2] = buffer[0 + i * 2];
                            temp[0 + i * 2] = buffer[1 + i * 2];
                        }
                    }
                    else
                    {
                        buffer.Slice(0, 4).CopyTo(temp);
                    }
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        return new DeviceValue { UINT32 = BinaryPrimitives.ReadUInt32BigEndian(temp) };
                    else
                        return new DeviceValue { UINT32 = BinaryPrimitives.ReadUInt32LittleEndian(temp) };
                case DataType.SINGLE:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        int count = buffer.Length / 2;
                        for (global::System.Int32 i = 0; i < count; i++)
                        {
                            temp[1 + i * 2] = buffer[0 + i * 2];
                            temp[0 + i * 2] = buffer[1 + i * 2];
                        }
                    }
                    else
                    {
                        buffer.Slice(0, 4).CopyTo(temp);
                    }
                    uint raw;
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        raw = BinaryPrimitives.ReadUInt32BigEndian(temp);
                    else
                        raw = BinaryPrimitives.ReadUInt32LittleEndian(temp);
                    byte[] bytes = BitConverter.GetBytes(raw);
                    return new DeviceValue { SINGLE = BitConverter.ToSingle(bytes,0) };
                case DataType.DOUBLE:
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        int count = buffer.Length / 2;
                        for (global::System.Int32 i = 0; i < count; i++)
                        {
                            temp[1 + i * 2] = buffer[0 + i * 2];
                            temp[0 + i * 2] = buffer[1 + i * 2];
                        }
                    }
                    else
                    {
                        buffer.Slice(0, 8).CopyTo(temp);
                    }
                    ulong draw;
                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        draw = BinaryPrimitives.ReadUInt64BigEndian(temp);
                    else
                        draw = BinaryPrimitives.ReadUInt64LittleEndian(temp);
                    byte[] dbytes = BitConverter.GetBytes(draw);
                    return new DeviceValue { DOUBLE = BitConverter.ToDouble(dbytes,0) };
                case DataType.UTF32:
                    byte[] utf32temp = new byte[buffer.Length];
                    if (dataEndian.HasFlag(DataEndianKind.IsSwapBytesInWord))
                    {
                        int count = buffer.Length / 2;
                        for (global::System.Int32 i = 0; i < count; i++)
                        {
                            utf32temp[1 + i * 2] = buffer[0 + i * 2];
                            utf32temp[0 + i * 2] = buffer[1 + i * 2];
                        }
                    }
                    else
                    {
                        buffer.Slice(0, buffer.Length).CopyTo(utf32temp);
                    }
                    System.Text.Encoding decoder;

                    if (dataEndian.HasFlag(DataEndianKind.IsBigEndian))
                        decoder = new System.Text.UTF32Encoding(true, false);
                    else
                        decoder = System.Text.Encoding.UTF32;
                    return new DeviceValue { STRING = decoder.GetString(utf32temp) };
                case DataType.ASCII:
                    byte[] asciitemp = new byte[buffer.Length];
                    buffer.Slice(0, buffer.Length).CopyTo(asciitemp);
                    return new DeviceValue { STRING = System.Text.ASCIIEncoding.ASCII.GetString(asciitemp) };
                case DataType.BIT:
            default:
                    throw new NotSupportedException($"未实现的 DataType: {dataType}");
            }
        }
    }
}
