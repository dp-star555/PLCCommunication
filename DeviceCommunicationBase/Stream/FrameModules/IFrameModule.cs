using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{

    /// <summary>
    /// 构建器模组接口
    /// </summary>
    public interface IFrameModule
    {
        int Length { get; }                     // 模组长度，-1 表示不定长
        void Emit(FrameBuildContext ctx);       // 第一遍：顺序写（或占位 / Mark）
        void Fixup(FrameBuildContext ctx);      // 第二遍：回填（Length/CRC 用）
        void Scan(FrameDecodeContext ctx);      // 扫描分割数据
        void Decode(FrameDecodeContext ctx);    // 解析数据

        //获取预设的固定数据
        byte[] GetConstData();
    }
}
