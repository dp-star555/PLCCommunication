using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationBase
{
    public interface IInputConverter
    {
        /// <summary>
        /// 解码输入的数据，转换成实际的地址
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        object Decode(string val);
    }

    public interface IInputConverter<out T> : IInputConverter
    {
        // 泛型版本，返回具体类型
        new T Decode(string val);
    }
}
