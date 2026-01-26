using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    public abstract class MaskBlock
    {
        public int Offset { get; set; }     // 相对于帧起始位置的偏移量
        public ushort Length { get; set; }  // 这个块的长度
    }

    //  实块：有具体数据的块
    public class SolidBlock : MaskBlock
    {
        public List<byte> Bytes { get; set; } // 期望的字节内容
    }

    //  空洞：定长度的块
    public class FixBlock : MaskBlock
    {
    }
    //  空洞：变长度的块
    public class VoidBlock : MaskBlock
    {
    }
    // 2. 搜索型空洞 (对应情况：Const夹心)
    // 逻辑：我的长度 = (下一个Const的位置) - (我的起始位置)
    public class ScanHoleBlock : MaskBlock
    {
        public byte[] Terminator { get; set; } // 下一个Const的内容作为终结符
    }
}
