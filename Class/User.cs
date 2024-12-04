using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
namespace DataLogger_NetCore.Class
{
    [BsonIgnoreExtraElements]
    public class User
    {
        public string tokenkey { get; set; }//Customer_id
        public string username { get; set; }// Name
        public string password { get; set; } // Address
        public double timeCreated { get; set; } // Thời gian tạo
        public double lastLogin { get; set; }
        public string permission { get; set; }
        public List<string> listDevices { get; set; } //  Danh sách thiết bị
        public List<string> listAreas { get; set; }
        public List<string> listUsers { get; set; }
        public Dictionary<string, DateTimeOffset> temporaryDevices { get; set; }

        //public List<string> mobilelogin { get; set; }
        //public long totDevices { get; set; }
        //public long totCellos { get; set; }
        //public long totDataLoggers { get; set; }
        [JsonConstructor]
        public User(string username, string password, string tokenkey, double timeCreated, double lastLogin, string permission, List<string> listDevices, List<string> listAreas)
        {
            this.username = username;
            this.password = password;
            this.tokenkey = tokenkey;
            this.timeCreated = timeCreated;
            this.lastLogin = lastLogin;
            this.permission = permission;
            this.listDevices = listDevices;
            this.listAreas = listAreas;
            listUsers = new List<string>();
            temporaryDevices = new Dictionary<string, DateTimeOffset>();
        }
        public void buildDeviceNotificationList()
        {
            foreach (string deviceid in listDevices.ToArray())
            {
                if (Program.Devices.ContainsKey(deviceid))
                {
                    Program.Devices[deviceid].addNotiList(username);
                    if (permission == "user")
                        Program.DeviceOwnership[deviceid] = username;
                }
                else listDevices.Remove(deviceid);
            }
        }
    }
    public class LastDataPoint
    {
        public object value { get; set; }
        public DateTime timestamp;
        public int priority;

        public double getVal()
        {
            if (value is double x)
                return x;
            return 0;
        }
    }
    [BsonIgnoreExtraElements]
    public class Device
    {
        public string Device_id { get; set; } //device_id
        public string area { get; set; }
        public bool Status { get; set; }
        public string Device_name { get; set; }
        public int type { get; set; } // device_type
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double lastEdit { get; set; }
        public double lastReceived { get; set; }
        public double lastTimeSystem { get; set; }
        public Dictionary<string, LastDataPoint> lastData { get; set; }
        public string version_running { get; set; }
        public string hw_config { get; set; }
        public List<string> notiUserList { get; set; }
        public string supportEngineer = "";
        public string owner = "";
        public string cameraId = "";
        public Dictionary<string, configInput> extraconfig = new Dictionary<string, configInput>();

        [JsonConstructor]
        public Device(string Device_id, bool Status, string Device_name, double latitude, double longitude, double lastEdit, double lastReceived, Dictionary<string, LastDataPoint> lastData, int type, string area, double lastTimeSystem)
        {
            this.Device_id = Device_id;
            this.Status = Status;
            this.Device_name = Device_name;
            this.longitude = longitude;
            this.latitude = latitude;
            this.lastReceived = lastReceived;
            this.lastEdit = lastEdit;
            this.lastData = lastData;
            this.type = type;
            this.area = area;
            this.lastTimeSystem = lastTimeSystem;
        }

        public void addNotiList(string username)
        {
            if (notiUserList == null) notiUserList = new List<string>();
            if (!notiUserList.Contains(username))
                notiUserList.Add(username);
        }

        public void removeNotiList(string username)
        {
            notiUserList.Remove(username);
        }

        public List<string> filterNotiList(string numInput)
        {
            List<string> res = new List<string>();
            foreach (string str in notiUserList)
                if (DeviceTemplate.permissionCheck(str, this, numInput))
                    res.Add(str);
            return res;
        }
    }
    public class TokenKey
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string? Time { get; set; } 
        public bool isOK { get; set; }
        public TokenKey() { }

        // Hàm khởi tạo
        [JsonConstructor]
        public TokenKey(string iKey)
        {
            try
            {
                // Nếu iKey là token admin, sử dụng thông tin mặc định
                if (iKey == "admin@aithings")
                {
                    this.UserName = "admin@aithings";
                    this.Password = "admin";
                    isOK = true;
                    return;
                }

                // Giải mã JWT
                var claimsPrincipal = TokenGenerator.ValidateToken(iKey);

                if (claimsPrincipal != null)
                {
                    // Trích xuất thông tin từ claims
                    var userId = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    var username = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
                    var role = claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value;
                    var jti = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                    // Lưu thông tin vào các thuộc tính
                    this.UserName = username;
                    this.Password = role; // Có thể thay đổi thành thông tin khác nếu cần
                    this.Time = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); // Thời gian hiện tại (hoặc trích xuất thời gian từ JWT)
                    isOK = true;
                }
                else
                {
                    isOK = false;
                }
            }
            catch (Exception)
            {
                isOK = false;
            }
        }
    }

    public class HttpRequest
    {
        public string tokenkey;
        public string username;
        public string password;
        public string newpassword;
        public string newdevicename;
        public string permission;
        public int year;
        public int month;
        public int day;
        public int hour;
        public string device_id;
        public string device_name;
        public double latitude;
        public double longitude;
        public string numInput;
        public List<string> listNumInput;
        public string area;
        public Dictionary<string, configInput> config;
        public int numberPage;
        public string fromDate;
        public string toDate;
        public Byte configMode;
        public Byte configPumpA;
        public Byte configPumpB;
        // lossblock
        public List<string> list_device;
        public double value;
        public int device_type;
        public string wifissid;
        public string wifipass;
        public Dictionary<string, int> inputPriority;
        public string camId;
    }

    public class CustomRequest<T>
    {
        public string tokenkey;
        public T data;
    }

    [BsonIgnoreExtraElements]
    public class configInput
    {
        public string numInput { get; set; }
        public string name { get; set; }
        public string unit { get; set; }
        public double Xmax { get; set; }
        public double Xmin { get; set; }
        public double Ymax { get; set; }
        public double Ymin { get; set; }
        public double high_level { get; set; }
        public double low_level { get; set; }
        public bool high_warning { get; set; }
        public bool low_warning { get; set; }
        public bool displayInfo { get; set; }
        public bool displayGraph { get; set; }
        public string controlCmd { get; set; }
        public string controlValue { get; set; }
        public List<byte> permission { get; set; }
        public bool critical { get; set; }

        [JsonConstructor]
        public configInput(string numInput, string name, string unit, double Xmax, double Xmin, double Ymax, double Ymin, bool displayInfo, bool displayGraph, double high_level, double low_level, string controlCmd, string controlValue, List<byte> permission, bool critical = false)
        {
            this.numInput = numInput;
            this.name = name;
            this.unit = unit;
            this.Xmax = Xmax;
            this.Xmin = Xmin;
            this.Ymax = Ymax;
            this.Ymin = Ymin;
            this.displayInfo = displayInfo;
            this.displayGraph = displayGraph;
            this.high_level = high_level;
            this.low_level = low_level;
            this.controlCmd = controlCmd;
            this.controlValue = controlValue;
            this.permission = permission == null ? new List<byte>() : permission;
            this.critical = critical;
        }
    }

    public class UserDashBoard
    {
        //user
        public int sumDevices;
        public int sumActive;
        public int sumInActive;
        // DataLogger
        public int sumDataLogger;
        public int sumDataLoggerActive;
        public int sumDataLoggerInActive;
        //
        public int sumCello;
        public int sumCelloActive;
        public int sumCelloInActive;
    }
    public class AreaDashBoard
    {
        public int sumDevices;
        public int sumActive;
        public int sumInActive;
    }
    public class Event
    {
        public string id { get; set; }
        //  public double timeEvent { get; set; }
        public string id_event { get; set; }
        public List<inputEvent> inputEvents { get; set; }
        public Event(string id, string id_event, List<inputEvent> inputEvents)
        {
            this.id = id;
            // this.timeEvent = timeEvent;
            this.inputEvents = inputEvents;
            this.id_event = id_event;
        }
    }

    public class inputEvent
    {
        public string numInput { get; set; }
        public string input_event { get; set; }
        public inputEvent(string numInput, string input_event)
        {
            this.input_event = input_event;
            this.numInput = numInput;
        }
    }
    public class lazyload
    {
        public int totalElements { get; set; }
        public int totalPage { get; set; }
        public int elementsInPage { get; set; }
        public int page { get; set; }
        public string[] data { get; set; }
        public lazyload(int totalPage, int elementsInPage, int totalElements, string[] data, int page)
        {
            this.totalPage = totalPage;
            this.elementsInPage = elementsInPage;
            this.totalElements = totalElements;
            this.data = data;
            this.page = page;
        }
    }
    //public class readDetails
    //{
    //    public string Device_id { get; set; } //device_id
    //    public string area { get; set; }
    //    public bool Status { get; set; }
    //    public string Device_name { get; set; }
    //    public int type { get; set; }
    //    public double latitude { get; set; }
    //    public double longitude { get; set; }
    //    public double lastEdit { get; set; }
    //    public double lastReceived { get; set; }
    //    public double lastTimeSystem { get; set; }
    //    public double[] lastData { get; set; }
    //    public Dictionary<string, object> config { get; set; }
    //}
    public class raw_data
    {
        public string filename;
        public byte[] data;
    }
    public class statistics_respond
    {
        public int totalData;
        public int totalOnline;
        public int totalOffline;

    }

    public class Device_respond
    {
        public string Device_id { get; set; } //device_id
        public string area { get; set; }
        public bool Status { get; set; }
        public string Device_name { get; set; }
        public int type { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double lastEdit { get; set; }
        public double lastReceived { get; set; }
        public double lastTimeSystem { get; set; }
        public Dictionary<string, LastDataPoint> lastData { get; set; }
        public string version_running { get; set; }
        public Dictionary<string, configInput> config { get; set; }
        public string hw_config { get; set; }
        public string supportEngineer = "";
        public string owner = "";
        public string cameraId = "";
        public Dictionary<string, configInput> extraconfig = new Dictionary<string, configInput>();
    }
    public class Read_User_respond
    {
        public string username;
        public int total_device;
        public List<Device_respond> list_devices;
        public List<string> typeList;
        public List<string> listEngineers;

        //public string smtp_server;
        //public int smtp_port;
        //public string tokenkey;
        //public string smtpusername;
        //public string smtppassword;
        //public List<string> toEmail;

    }
    public class ReadDeviceInDay
    {
        public List<string> list_header { get; set; }
        public List<double[]> data { get; set; }
        public ReadDeviceInDay(List<string> list_header, List<double[]> data)
        {
            this.list_header = list_header;
            this.data = data;
        }
    }
    //public class emailinfo
    //{
    //    public string smtp_server;
    //    public int smtp_port;
    //  //  public string tokenkey;
    //    public string smtpusername;
    //    public string smtppassword;
    //    public Dictionary<string, List<string>> toEmail;
    //   // public string username;
    //}
    //public class getemail
    //{
    //    public string username;
    //    public string tokenkey;
    //}
    //public class setemail
    //{
    //    public string username;
    //    public string tokenkey;
    //    public List<string> toEmail;
    //}
    public class lossblock
    {
        public List<string> list_device;
        public double[] total_block;
    }
}
