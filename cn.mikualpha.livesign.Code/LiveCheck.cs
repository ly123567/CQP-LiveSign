﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Native.Sdk.Cqp.Enum;
using Native.Tool.Http;

internal abstract class LiveCheck
{
    private Thread thread = null;

    private string[] groups, admins;
    public bool running = false;
    internal enum LivingStatus { OFFLINE, ONLINE, OTHER, ERROR };

    internal LiveCheck() { }

    public bool isGroup(string input)
    {
        groups = (getOptions()["Group"] as string).Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string i in groups)
        {
            if (i == "0") return true;
            if (i == input) return true;
        }
        return false;
    }

    public bool isAdmin(string input)
    {
        admins = (getOptions()["Admin"] as string).Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string i in admins)
        {
            if (i == "0") return true;
            if (i == input) return true;
        }
        return false;
    }

    public void startCheck()
    {
        if (running) return;
        running = true;
        thread = new Thread(checkStatus);
        thread.Start();
    }

    public void endCheck()
    {
        if (!running) return;
        try
        {
            running = false;
            thread.Abort();
        }
        catch (ThreadAbortException) { }
    }

    private void checkStatus()
    {
        while (running)
        {
            string[] rooms = getSQLiteManager().getRooms();
            foreach (string i in rooms)
            {

                int room_status = getDataRoomStatus(i);
                //ApiModel.CQLog.Debug("LivingStatusDebug-" + i, room_status.ToString());
                if (room_status == (int)LivingStatus.ERROR) continue;

                if (getSQLiteManager().getLiveStatus(i) != room_status)
                {
                    getSQLiteManager().setLiveStatus(i, room_status);
                    if (room_status == (int)LivingStatus.ONLINE) //正在直播
                    {
                        string[] users = getSQLiteManager().getUserByRoom(i); //获取所有订阅用户并发送消息
                        foreach (string j in users) { sendPrivateMessage(j); }

                        string[] groups = getSQLiteManager().getGroupByRoom(i); //获取所有订阅群组并发送消息
                        foreach (string k in groups) { sendGroupMessage(k); }
                    }
                }
            }
            Thread.Sleep(5000);
        }
    }

    private void sendGroupMessage(string group)
    {
        string msg = getOnlineMessage();
        int atAll = int.Parse(getOptions()["AtAll"]);
        int userType = (int)ApiModel.CQApi.GetGroupMemberInfo(long.Parse(group), ApiModel.CQApi.GetLoginQQ().Id).MemberType;
        if (atAll > 0 && userType > 1 /* 非普通群员 */) msg = "[CQ:at,qq=all] " + msg;
        ApiModel.CQApi.SendGroupMessage(long.Parse(group), msg);
    }

    private void sendPrivateMessage(string qq)
    {
        ApiModel.CQApi.SendPrivateMessage(long.Parse(qq), getOnlineMessage());
    }

    //获取订阅列表
    public string getUserSubscribe(long user)
    {
        string str = getSQLiteManager().getUserSubscribeList(user);
        if (str == "") return "列表为空！";
        string[] array = str.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string output = "";
        for (int i = 0; i < array.Length; ++i)
        {
            if (output != "") output += "\r\n";
            output += array[i];
        }
        return output;
    }

    protected Dictionary<string, string> getOptions() { return FileOptions.GetInstance().GetOptions(); }

    private string getOnlineMessage() //对返回的消息模板进行进一步处理，为附加前后缀处理预留接口
    {
        return getOnlineMessageModel();
    }

    public void SubscribeByUser(long user, string room)
    {
        getSQLiteManager().addSubscribe(user.ToString(), room, 0);
    }

    public void SubscribeByGroup(long group, string room)
    {
        getSQLiteManager().addSubscribe(group.ToString(), room, 1);
    }

    public void Desubscribe(long user, string room, int group = 0)
    {
        getSQLiteManager().deleteSubscribe(user.ToString(), room, group);
    }

    protected string getProxyAddress()
    {
        return getOptions()["ProxyAddress"];
    }

    protected int getProxyPort()
    {
        return int.Parse(getOptions()["ProxyPort"]);
    }

    protected string getHttpProxy(string url, Dictionary<string, string> header = null)
    {
        try
        {
            WebHeaderCollection webHeaderCollection = new WebHeaderCollection();
            CookieCollection cookies = new CookieCollection();
            string accept = "";

            if (header != null)
            {
                foreach (KeyValuePair<string, string> kv in header)
                {
                    if (kv.Key == "Accept") accept = kv.Value;
                    else webHeaderCollection.Add(kv.Key, kv.Value);
                }

            }

            if (int.Parse(getOptions()["EnableProxy"]) > 0) {
                return Encoding.UTF8.GetString(HttpWebClient.Get(url, "", "", accept, 0, ref cookies, ref webHeaderCollection, new WebProxy(getProxyAddress(), getProxyPort()), Encoding.UTF8));
            }
            else
            {
                return Encoding.UTF8.GetString(HttpWebClient.Get(url, "", "", accept, 0, ref cookies, ref webHeaderCollection, null, Encoding.UTF8));
            }
        } catch (WebException) { return ""; }
        
    }

    protected abstract SQLiteManager getSQLiteManager(); //获取SQLite管理实例

    protected abstract int getDataRoomStatus(string room); //检查开播状态

    public abstract string getOwnerName(string room); //启用&禁用时获取主播名

    protected abstract string getHttp(string room); //API内容获取(处理另行实现)

    protected abstract string getOnlineMessageModel(); //获取发送消息格式

    protected abstract string getEasterEggStr(string room_id); //获取彩蛋语句
}
