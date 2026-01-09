using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase
{
    /// <summary>
    /// 从连续字节流中拆分出完整帧的通用接口
    /// </summary>
    public interface IFrameSplitter
    {
        /// <summary>
        /// 追加收到的数据（可能是半包、粘包）
        /// </summary>
        void Feed(ReadOnlySpan<byte> data);

        /// <summary>
        /// 每拆出一帧完整包时触发
        /// </summary>
        event Action<ReadOnlyMemory<byte>> FrameCompleted;

        /// <summary>
        /// 清空内部缓冲
        /// </summary>
        void Reset();
    }
}
