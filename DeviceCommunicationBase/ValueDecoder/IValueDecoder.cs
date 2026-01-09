using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationBase
{
    [Flags]
    /// <summary>
    /// 多字（word）数据的端序模式：字内部 + 多字顺序
    /// 适用于所有以 16/32bit word 为单位的数据协议（Modbus、MC、S7 等）
    /// </summary>
    public enum DataEndianKind
    {
        /// <summary>
        /// 标准大端：word 内高字节在前；多 word 时高 word 在前（AB CD）
        /// </summary>
        IsBigEndian = 1,

        /// <summary>
        /// 字内交换：（BA CD）
        /// </summary>
        IsSwapBytesInWord = 2
    }

    public interface IValueDecoder
    {
        /// <summary>
        /// 解码器，用于将数据解释成可读数据
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="dataType"></param>
        /// <param name="dataEndian"></param>
        /// <returns></returns>
        DeviceValue Decode(ReadOnlySpan<byte> buffer, DataType dataType, DataEndianKind dataEndian);
        /// <summary>
        /// 编码器，用于将数据转成发送的数据
        /// </summary>
        /// <param name="value"></param>
        /// <param name="dataType"></param>
        /// <param name="dataEndian"></param>
        /// <returns></returns>
        byte[] Code(object value, DataType dataType, ushort length, DataEndianKind dataEndian);
    }
}
