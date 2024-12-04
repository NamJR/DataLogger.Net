using DataLogger_NetCore.Class;
using OneSignalApi.Api;
using OneSignalApi.Client;
using OneSignalApi.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataLogger_NetCore.Notifications
{
    public static class OneSignalService
    {
        public static DefaultApi apiInstance;
        static string appID = "9a82e4b7-ef74-4142-bf1b-ad44af1b7e71";
        static DateTime lastsend;
        static OneSignalService()
        {
            init();
        }
        static void init()
        {
            var appConfig = new Configuration();
            appConfig.BasePath = "https://onesignal.com/api/v1";
            appConfig.AccessToken = "MjcyZDRhMjAtNDM5YS00NTE2LTg2YTktYWNmYWZhNjkyZDM1";

            apiInstance = new DefaultApi(appConfig);
        }
        public static void createNotification(Device device, string msg, List<string> targetUsers)
        {
            if ((DateTime.UtcNow - lastsend).TotalMinutes < 1) return;
            lastsend = DateTime.UtcNow;

            var not = new Notification(appId: appID);
            not.Contents = new StringMap(en: msg);
            not.IncludeExternalUserIds = targetUsers;
            not.TargetChannel = Notification.TargetChannelEnum.Push;
            not.AndroidChannelId = "c47da6f5-9d08-41da-8702-c7543a5863b4";
            //not.IncludedSegments = new List<string> { "All" };
            try
            {
                // Create notification
                var res = apiInstance.CreateNotificationAsync(not).Result;
                Program.saveLog("noti from " + device.Device_id + " to " + string.Join(",", device.notiUserList) + " sent to " + res.Recipients + " recipients");
            }
            catch (ApiException e)
            {
                Program.saveLog(e.ToString());
            }
        }

    }
}
