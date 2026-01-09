using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace DP_Common.FileEX
{
    /// <summary>
    /// CSV帮助
    /// </summary>
    public static class CSVHelper
    {
        /// <summary>
        /// 将dgv列表数据转换为datatable数据
        /// </summary>
        /// <param name="dgv">当前dgv列表对象</param>
        /// <returns>datatable对象</returns>
        public static DataTable DataGridView2DataTable(DataGridView dgv)
        {
            DataTable dt = new DataTable();

            // 列强制转换
            for (int count = 0; count < dgv.Columns.Count; count++)
            {
                System.Data.DataColumn dc = new System.Data.DataColumn(dgv.Columns[count].Name.ToString());
                dt.Columns.Add(dc);
            }

            // 循环行
            for (int count = 0; count < dgv.Rows.Count; count++)
            {
                DataRow dr = dt.NewRow();
                for (int countsub = 0; countsub < dgv.Columns.Count; countsub++)
                {
                    dr[countsub] = Convert.ToString(dgv.Rows[count].Cells[countsub].Value);
                }
                dt.Rows.Add(dr);
            }
            return dt;
        }

        /// <summary>
        /// 将DataTable中的数据保存到CSV中
        /// </summary>
        /// <param name="dt">DataTable</param>
        /// <param name="fullPath">文件路径</param>
        public static void DataTable2CSV(DataTable dt, string fullPath)
        {
            //判断文件是否存在
            System.IO.FileInfo fi = new System.IO.FileInfo(fullPath);
            if (!fi.Directory.Exists)
            {
                fi.Directory.Create();
            }

            //打开文件流
            System.IO.FileStream fs = new System.IO.FileStream(fullPath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

            string data = "";

            for (int i = 0; i < dt.Columns.Count; i++)//写入列名
            {
                data += dt.Columns[i].ColumnName.ToString();
                if (i < dt.Columns.Count - 1)
                {
                    data += ",";
                }
            }
            sw.WriteLine(data);

            for (int i = 0; i < dt.Rows.Count; i++) //写入各行数据
            {
                data = "";
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    string str = dt.Rows[i][j].ToString();
                    str = str.Replace("\"", "\"\"");//替换英文冒号 英文冒号需要换成两个冒号
                    if (str.Contains(',') || str.Contains('"')
                      || str.Contains('\r') || str.Contains('\n')) //含逗号 冒号 换行符的需要放到引号中
                    {
                        str = string.Format("\"{0}\"", str);
                    }

                    data += str;
                    if (j < dt.Columns.Count - 1)
                    {
                        data += ",";
                    }
                }
                sw.WriteLine(data);
            }
            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 将Csv文件导入DataTable表中
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public static DataTable CSV2DataTable(string fullPath) 
        {
            DataTable ret = new DataTable();
            //判断文件是否存在
            System.IO.FileInfo fi = new System.IO.FileInfo(fullPath);
            if (!fi.Directory.Exists)
            {
                return null;
            }

            //打开文件流
            System.IO.FileStream fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            System.IO.StreamReader sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8);

            bool isFirstLine = true;

            string lineStr = "";

            while ((lineStr = sr.ReadLine())!= null)
            {
                string[] strArr;
                if (isFirstLine)
                {
                    strArr = lineStr.Split(',');
                    isFirstLine = false;
                    foreach (var item in strArr)//新建列
                    {
                        ret.Columns.Add(item);
                    }
                }
                else
                {
                    DataRow dr = ret.NewRow();
                    strArr = lineStr.Split(',');
                    for (int i = 0; i < ret.Columns.Count; i++)
                    {
                        dr[i] = strArr[i];
                    }
                    ret.Rows.Add(dr);
                }
            }
            sr.Close();
            fs.Close();
            return ret;

        }

        /// <summary>
        /// 添加表格数据至CSV文件
        /// </summary>
        /// <param name="dt">数据表格</param>
        /// <param name="fullPath">地址</param>
        /// <param name="hearderAdd">是否再次输入列名</param>
        public static void Add2CSV(DataTable dt, string fullPath,bool hearderAdd = false) 
        {
            bool isHeaderHave = true;
            //判断文件是否存在
            System.IO.FileInfo fi = new System.IO.FileInfo(fullPath);
            if (!fi.Directory.Exists)
            {
                fi.Directory.Create();
                isHeaderHave = false;
            }

            //打开文件流
            System.IO.FileStream fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

            string data = "";

            if (hearderAdd)
            {
                isHeaderHave = false;
            }

            if (!isHeaderHave)
            {
                for (int i = 0; i < dt.Columns.Count; i++)//写入列名
                {
                    data += dt.Columns[i].ColumnName.ToString();
                    if (i < dt.Columns.Count - 1)
                    {
                        data += ",";
                    }
                }
                sw.WriteLine(data);
            }

            for (int i = 0; i < dt.Rows.Count; i++) //写入各行数据
            {
                data = "";
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    string str = dt.Rows[i][j].ToString();
                    str = str.Replace("\"", "\"\"");//替换英文冒号 英文冒号需要换成两个冒号
                    if (str.Contains(',') || str.Contains('"')
                      || str.Contains('\r') || str.Contains('\n')) //含逗号 冒号 换行符的需要放到引号中
                    {
                        str = string.Format("\"{0}\"", str);
                    }

                    data += str;
                    if (j < dt.Columns.Count - 1)
                    {
                        data += ",";
                    }
                }
                sw.WriteLine(data);
            }
            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 添加表格数据至CSV文件
        /// </summary>
        /// <param name="valueStr">数据字符串</param>
        /// <param name="fullPath">地址</param>
        public static void Add2CSV(string valueStr, string fullPath)
        {
            //判断文件是否存在
            System.IO.FileInfo fi = new System.IO.FileInfo(fullPath);
            if (!fi.Directory.Exists)
            {
                throw new Exception("文件不存在！");
            }

            //打开文件流
            System.IO.FileStream fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

            string[] strArr = valueStr.Split(',');
            string data = "";
            for (int i = 0; i < strArr.Length; i++)
            {
                string str = strArr[i];
                str = str.Replace("\"", "\"\"");//替换英文冒号 英文冒号需要换成两个冒号
                if (str.Contains(',') || str.Contains('"')
                  || str.Contains('\r') || str.Contains('\n')) //含逗号 冒号 换行符的需要放到引号中
                {
                    str = string.Format("\"{0}\"", str);
                }

                data += str;
                if (i < strArr.Length - 1)
                {
                    data += ",";
                }
            }
            sw.WriteLine(valueStr);

            sw.Close();
            fs.Close();

        }

        /// <summary>
        /// 通过列明获取DataGridView的列索引
        /// </summary>
        /// <param name="dgv">DataGridView</param>
        /// <param name="colName">列名</param>
        /// <returns></returns>
        public static int GetDGVColIndexByName(DataGridView dgv, string colName)
        {
            for (int i = 0; i < dgv.Columns.Count; i++)
            {
                if (dgv.Columns[i].Name == colName)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
