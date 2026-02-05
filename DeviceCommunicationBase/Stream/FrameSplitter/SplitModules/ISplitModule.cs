using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter
{
    public interface ISplitModule
    {
        E_SplitResult Apply(ref FrameSplitContext ctx);
    }

    public enum E_SplitResult 
    {
        /// <summary>
        /// 匹配完成
        /// </summary>
        Ok,  
        /// <summary>
        /// 失败
        /// </summary>
        None,
        /// <summary>
        /// 半包
        /// </summary>
        NeedMore,
        /// <summary>
        /// 错位/尾不匹配
        /// </summary>
        BadAlign    
    }
}
