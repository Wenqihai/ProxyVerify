namespace Proxy.Service
{
    using Proxy.Contracts;
    using Proxy.Data;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Serialization.Json;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;

    [ServiceBehavior(ConcurrencyMode=ConcurrencyMode.Multiple, InstanceContextMode=InstanceContextMode.Single)]
    public class ProxyService : IProxyService
    {
        public static Dictionary<string, OperationContext> ContextDic = new Dictionary<string, OperationContext>();
        public static Dictionary<string, DateTime> HeartBeatDic = new Dictionary<string, DateTime>();
        private static ProxyService instance;
        private static DataManger manager;
        public static object syncRoot = new object();

        public static  event Action<string> Canceling;

        public static  event Action<string> Error;

        public static  event Action<RegisterEntity> Registering;

        public ProxyService()
        {
            if (instance == null)
            {
                instance = this;
            }
            manager = new DataManger(ConfigurationManager.ConnectionStrings["ProxyServiceContext"].ConnectionString);
            manager.Error += new Action<string>(this.manager_Error);
        }

        public void AllowDial(string mac, bool allow)
        {
            if (ContextDic[mac].Channel.State == CommunicationState.Opened)
            {
                IProxyServiceCallback callbackChannel = ContextDic[mac].GetCallbackChannel<IProxyServiceCallback>();
                this.DeleteProxy(mac);
                ContextDic.Remove(mac);
                try
                {
                    callbackChannel.AllowDial(allow);
                }
                catch (Exception exception)
                {
                    Error("更改拨号设置失败：" + exception.Message);
                }
            }
        }

        public void CancelProxy(string mac)
        {
            lock (syncRoot)
            {
                if (ContextDic.ContainsKey(mac))
                {
                    ContextDic[mac].Channel.Abort();
                    ContextDic.Remove(mac);
                }
                OperationContext.Current.Channel.Abort();
                this.DeleteProxy(mac);
            }
        }

        public void ClearFault()
        {
            Error("开始扫描掉线代理。");
            KeyValuePair<string, DateTime>[] pairArray = HeartBeatDic.ToArray<KeyValuePair<string, DateTime>>();
            lock (syncRoot)
            {
                foreach (KeyValuePair<string, DateTime> pair in pairArray)
                {
                    if ((DateTime.Now - pair.Value) > new TimeSpan(0, 0, 30))
                    {
                        Error(pair.Key + "长时间无响应");
                        if (ContextDic.ContainsKey(pair.Key))
                        {
                            ContextDic[pair.Key].Channel.Abort();
                            ContextDic.Remove(pair.Key);
                            this.DeleteProxy(pair.Key);
                        }
                        else
                        {
                            HeartBeatDic.Remove(pair.Key);
                        }
                    }
                }
            }
            Error("清理完毕。");
        }

        private void DeleteProxy(string mac)
        {
            if (manager.DeleteProxy(mac))
            {
                this.OnCanceling(mac);
            }
        }

        public string GetIP()
        {
            RemoteEndpointMessageProperty property = OperationContext.Current.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
            return property.Address;
        }

        private string GetLoctaion(string ip)
        {
            string str;
            using (StreamReader reader = new StreamReader(WebRequest.Create("http://ip.taobao.com//service/getIpInfo.php?ip=" + ip).GetResponse().GetResponseStream()))
            {
                str = reader.ReadToEnd();
            }
            JsonResponse response2 = new JavaScriptSerializer().Deserialize<JsonResponse>(str);
            if (response2.Code == 0)
            {
                return string.Concat(new object[] { response2.Data.Country, '\t', response2.Data.City, response2.Data.Isp });
            }
            return "\t";
        }

        public bool HeartBeatMessage(string mac)
        {
            if (!ContextDic.ContainsKey(mac))
            {
                return false;
            }
            lock (HeartBeatDic)
            {
                if (HeartBeatDic.ContainsKey(mac))
                {
                    HeartBeatDic[mac] = DateTime.Now;
                }
                else
                {
                    HeartBeatDic.Add(mac, DateTime.Now);
                }
            }
            return true;
        }

        public static object JsonToObject(string jsonString, System.Type type)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(type);
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            return serializer.ReadObject(stream);
        }

        public void LetCancel(string mac)
        {
            if (ContextDic.ContainsKey(mac) && (ContextDic[mac].Channel.State == CommunicationState.Opened))
            {
                ContextDic[mac].GetCallbackChannel<IProxyServiceCallback>().Exit();
                lock (syncRoot)
                {
                    ContextDic[mac].Channel.Abort();
                    ContextDic.Remove(mac);
                }
                this.DeleteProxy(mac);
            }
        }

        public void LetDial(string mac)
        {
            if (ContextDic.ContainsKey(mac) && (ContextDic[mac].Channel.State == CommunicationState.Opened))
            {
                ContextDic[mac].GetCallbackChannel<IProxyServiceCallback>().Restart(true);
            }
        }

        public void LetRestart(string mac)
        {
            if (ContextDic.ContainsKey(mac) && (ContextDic[mac].Channel.State == CommunicationState.Opened))
            {
                ContextDic[mac].GetCallbackChannel<IProxyServiceCallback>().Restart(false);
            }
        }

        private void manager_Error(string obj)
        {
            if (Error != null)
            {
                Error(obj);
            }
        }

        private void OnCanceling(string mac)
        {
            if (Canceling != null)
            {
                Canceling(mac);
            }
        }

        private void OnRegistering(RegisterEntity regInfo)
        {
            if (Registering != null)
            {
                Registering(regInfo);
            }
        }

        public bool RegisterProxy(RegisterEntity regInfo)
        {
            bool flag2;
            regInfo.Location = this.GetLoctaion(regInfo.Ip);
            try
            {
                lock (syncRoot)
                {
                    if (!ContextDic.ContainsKey(regInfo.Mac))
                    {
                        ContextDic.Add(regInfo.Mac, OperationContext.Current);
                        try
                        {
                            if (manager.AddProxy(regInfo))
                            {
                                this.OnRegistering(regInfo);
                            }
                            else
                            {
                                return false;
                            }
                        }
                        catch (Exception exception)
                        {
                            Error("注册失败：" + exception.Message);
                            return false;
                        }
                        return true;
                    }
                    ContextDic[regInfo.Mac].Channel.Abort();
                    ContextDic[regInfo.Mac] = OperationContext.Current;
                    this.DeleteProxy(regInfo.Mac);
                    manager.AddProxy(regInfo);
                    this.OnRegistering(regInfo);
                    flag2 = true;
                }
            }
            catch (Exception exception2)
            {
                Error(regInfo.PcName + "\t" + regInfo.Location + "\t注册失败:" + exception2.Message + exception2.StackTrace);
                flag2 = false;
            }
            return flag2;
        }

        public static ProxyService Instance
        {
            get
            {
                return instance;
            }
        }

        public static DataManger Manager
        {
            get
            {
                return manager;
            }
        }
    }
}

