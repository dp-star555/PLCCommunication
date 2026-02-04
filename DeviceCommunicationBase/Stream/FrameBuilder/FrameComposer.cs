using System;
using System.Collections.Generic;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 构建器主体
    /// </summary>
    public sealed class FrameComposer
    {
        private readonly List<IFrameModule> mModules = new List<IFrameModule>();

        public FrameComposer Add(IFrameModule module)
        {
            mModules.Add(module);
            return this;
        }

        public byte[] Build()
        {
            var ctx = new FrameBuildContext();

            //// 第1遍：写入
            //foreach (var m in mModules)
            //    m.Encode_Emit(ctx);

            //// 第2遍：回填
            //foreach (var m in mModules)
            //    m.Encode_Fixup(ctx);

            return ctx.ToArray();
        }
    }

}
