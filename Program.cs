using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyVerify
{
    class Program
    {
        public string localIP;
        public static SqlConnection localConn = new SqlConnection();
        static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(500, 10);
            string str = "Data Source=219.235.3.216;Initial Catalog=ProxyDB;User ID=wen;Password=andy_wen;MultipleActiveResultSets=True;";
            localConn.ConnectionString = str;
            localConn.Open();
            var table = LoadData();
            foreach (DataRow item in table.Rows)
            {
                string ip = item[0].ToString();
                int port = (int)item[1];
                ProxyVerify(ip, port);
            }
            Console.ReadLine();
        }
        public static Task<bool> VerifyHttpProxyAsync(string ip, int port)
        {
            return Task.Run
                (() =>
            {
                try
                {
                    var request = WebRequest.Create(App.Default.VerifyUrl);
                    request.Proxy = new WebProxy(ip, port);
                    request.Timeout = 10000;
                    var response = request.GetResponse();
                    return true;
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    return false;
                }
            });
        }

        public static async void ProxyVerify(string ip,int port)
        {
            bool ret = await VerifyHttpProxyAsync(ip, port);
            if(ret)
            {
                Console.WriteLine(ip + "验证成功！");
                SaveData(ip, port);
            }
            else
            {
                Console.WriteLine("验证失败！");
            }
        }

        public static DataTable LoadData()
        {
          
            string str = "Data Source=219.235.3.216;Initial Catalog=ip;User ID=wen;Password=andy_wen;MultipleActiveResultSets=True;";
            SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM tb_ProxyIp", str);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        public static void SaveData(string ip,int port)
        {
            //var cmd = localConn.CreateCommand();
            //cmd.CommandText = string.Format("INSERT INTO ProxyIp(Ip,Port,AddTime,CheckTime) VALUES('{0}',{1},'{2}','{3}')", ip, port,DateTime.Now,DateTime.Now);
            //cmd.ExecuteNonQueryAsync();
        }

    }
}
