using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLogger_NetCore.Class
{

    public class ActivityLog
    {
        public DateTime datetime;
        public string username;
        public string deviceid;
        public string content;
        public int level;

        public ActivityLog(DateTime datetime, string username, string deviceid, string content, int level)
        {
            this.datetime = datetime;
            this.username = username;
            this.deviceid = deviceid;
            this.content = content;
            this.level = level;
        }

        public static void insertData(DateTime timestamp, string username, string deviceid, string content, int level = 0)
        {
            var activity = new ActivityLog(timestamp, username, deviceid, content, level);
            try
            {
                Program.ActivityLog_Collection.InsertOneAsync(activity);
            }
            catch (Exception) { }
        }

        public static List<ActivityLog> searchData(DateTime fromDate, DateTime toDate, User user)
        {
            var filter = Builders<ActivityLog>.Filter.Gte(x => x.datetime, fromDate) & Builders<ActivityLog>.Filter.Lte(x => x.datetime, toDate);
            var allLog = Program.ActivityLog_Collection.Find(filter).ToList();
            if (user.permission == "admin") return allLog;
            return allLog.Where(x => x.username == user.username || user.listDevices.Contains(x.deviceid)).ToList();
        }
    }

}
