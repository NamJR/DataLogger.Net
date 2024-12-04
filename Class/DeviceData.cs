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
    [BsonIgnoreExtraElements]
    public class DateData
    {
        public DateTime datetime;
        public Dictionary<string, string> codedata;
        public DateData(DateTime datetime)
        {
            this.datetime = datetime;
            codedata = new Dictionary<string, string>();
        }
    }

    public class DeviceData
    {
        public IMongoCollection<DateData> DeviceData_Collection;
        [BsonId]
        public string device_id;
        public List<DateData> datedata;
        public bool collectionExist = false;

        public static Dictionary<string, DeviceData> listDeviceData = new Dictionary<string, DeviceData>();
        public static DeviceData getDevice(string id)
        {
            if (!listDeviceData.ContainsKey(id))
                listDeviceData[id] = new DeviceData(id);
            return listDeviceData[id];
        }
        public DeviceData(string device_id)
        {
            this.device_id = device_id;
            datedata = new List<DateData>();
            if (!collectionExist)
            {
                if (!Program.datalogger.ListCollectionNames().ToList().Contains("d_" + device_id))
                    Program.datalogger.CreateCollection("d_" + device_id, new CreateCollectionOptions { TimeSeriesOptions = new TimeSeriesOptions("datetime") });
                collectionExist = true;
            }
            DeviceData_Collection = Program.datalogger.GetCollection<DateData>("d_" + device_id);
        }

        public void insertData(DateTime timestamp, Dictionary<string, string> data)
        {
            var datedata = new DateData(timestamp);
            datedata.codedata = data;
            try
            {
                DeviceData_Collection.InsertOne(datedata);
            }
            catch (Exception) { }
        }

        public List<DateData> searchData(DateTime fromDate, DateTime toDate)
        {
            var filter = Builders<DateData>.Filter.Gte(x => x.datetime, fromDate) & Builders<DateData>.Filter.Lte(x => x.datetime, toDate);
            return DeviceData_Collection.Find(filter).ToList();
        }
    }

}