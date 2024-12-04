using DataLogger_NetCore.Class;
using DataLogger_NetCore.MQTT;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DataLogger_NetCore
{
    class Program
    {
        //
        public static ConcurrentDictionary<string, string> DeviceOwnership = new ConcurrentDictionary<string, string>();
        public static Dictionary<string, User> Users = new Dictionary<string, User>(); //Users Load file
        public static List<string> listEngineers = new List<string>();
        public static Dictionary<string, Device> Devices = new Dictionary<string, Device>(); //Devices Load file                             
        // event live API
        public static Dictionary<string, Queue<string>> userLog = new Dictionary<string, Queue<string>>(); // username -- eventlog -- event live
        // event load API (50 events)
        public static Dictionary<string, Queue<string>> userListLog = new Dictionary<string, Queue<string>>(); // username -- eventlog -- 50 events
        public static Dictionary<string, int> highWarningList = new Dictionary<string, int>();
        public static Dictionary<string, int> lowWarningList = new Dictionary<string, int>();

        public static IMongoCollection<User> User_Collection;
        public static IMongoCollection<Device> Device_Collection;
        public static IMongoCollection<DeviceTemplate> Template_Collection;
        public static IMongoCollection<ActivityLog> ActivityLog_Collection;

        //public static IMongoCollection<emailinfo> Email_Collection;

        // Save tokenkey in dictionary --> username                                                                                     

        public static string Admin_username;// = "admin@aquasoft";
        public static string Admin_password;// = "admin";
        public static int count = 0;
        public static int[] APIport;
        public static int check_time = 1000;
        public static IWebHost _webApp;
        // user log file
        // public static Dictionary<string, Dictionary<double, List<string>>> event_logs = new Dictionary<string, Dictionary<double, List<string>>>();
        //public static object id_user_log_lock = new object();
        public static string version = ""; // fix user log save file
        public static string MongoConnection;
        public static string Database;
        public static string hostname = "";
        public static string startupPath = Directory.GetCurrentDirectory();

        public static IMongoDatabase datalogger;
        public static HandleMQTT mqttHandle;

        static void Main()
        {
            //GetAquaboxDataAndPushToCollector();

            Console.Clear();
            readConfig();
            saveLog("version:\t" + version);

            double timenow = Program.getTime().ToOADate();

            saveLog("MongoDB:\t" + MongoConnection);
            Console.WriteLine("> MongoDB:\t" + MongoConnection);
            MongoClient dbClient = new MongoClient(MongoConnection);
            datalogger = dbClient.GetDatabase(Database);

            saveLog("Database:\t" + Database);
            User_Collection = datalogger.GetCollection<User>("User");
            Device_Collection = datalogger.GetCollection<Device>("Device");
            Template_Collection = datalogger.GetCollection<DeviceTemplate>("Template");
            if (!datalogger.ListCollectionNames().ToList().Contains("Activitylog"))
                datalogger.CreateCollection("Activitylog", new CreateCollectionOptions { TimeSeriesOptions = new TimeSeriesOptions("datetime") });
            ActivityLog_Collection = datalogger.GetCollection<ActivityLog>("Activitylog");

            var user_docmument = User_Collection.Find(new BsonDocument()).ToList();// PacketProcessor.readUsers();
            var device_docmument = Device_Collection.Find(new BsonDocument()).ToList();
            foreach (var item in user_docmument)
                Users.Add(item.username, item);
            foreach (var dv in device_docmument)
                if (!Devices.ContainsKey(dv.Device_id))
                {
                    if (dv.area == null) dv.area = "";
                    if (dv.supportEngineer == null) dv.supportEngineer = "";
                    if (dv.owner == null) dv.owner = "";
                    if (dv.extraconfig == null) dv.extraconfig = new Dictionary<string, configInput>();
                    Devices.Add(dv.Device_id, dv);
                    dv.notiUserList = new List<string>();
                }
            foreach (var item in Users.Values)
            {
                item.buildDeviceNotificationList();
                if (item.permission == "engineer") listEngineers.Add(item.username);
                if (item.temporaryDevices == null) item.temporaryDevices = new Dictionary<string, DateTimeOffset>();
            }
            //DeviceTemplate.loadTemplate();
            DeviceTemplate.loadDb();
            //  PacketProcessor.readDevicelog();
            //AdminLogin = PacketProcessor.readAdmin();
            if (!Users.ContainsKey("admin@aithings"))
            {
                Users.Add("admin@aithings", new User("admin@aithings", "admin", "", timenow, -1, "admin", new List<string>(), new List<string>()));
                User_Collection.InsertOne(Users["admin@aithings"]);
                //PacketProcessor.writeUsers(Users);
            }
            if (!AdminAccountExist(Users))
            {
                Users.Add(Admin_username, new User(Admin_username, Admin_password, "", timenow, -1, "admin", new List<string>(), new List<string>()));
                User_Collection.InsertOne(Users[Admin_username]);
                //PacketProcessor.writeUsers(Users);
            }
            if (APIport != null)
            {
                for (int i = 0; i < APIport.Length; i++)
                {
                    // hostname = (hostname == "") ? "*": hostname;
                    string baseAddress = "http://" + hostname + ":" + APIport[i].ToString() + "/";
                    _webApp = new WebHostBuilder()
                        .UseContentRoot(Directory.GetCurrentDirectory())
                        .UseKestrel()
                        .UseStartup<Startup>()
                        .UseUrls(baseAddress)
                        .Build();

                    _webApp.Start();
                    saveLog("API port:\t" + APIport[i]);
                    saveLog("Hostname:\t" + hostname);
                    //Console.WriteLine(Program.getTime().ToString("dd/MM/yyyy HH:mm:ss") + "]: API port:\t" + APIport[i]);
                    //Console.WriteLine(Program.getTime().ToString("dd/MM/yyyy HH:mm:ss") + "]: Hostname:\t" + hostname);
                }
            }
            {
                var builder = WebApplication.CreateBuilder();
                // L?y c?u hình JWT t? appsettings.json
                var jwtSettings = builder.Configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"];
                var issuer = jwtSettings["Issuer"];
                var audience = jwtSettings["Audience"];

                // Thêm xác th?c JWT
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                    };
                });
                // Add services to the container.

                builder.Services.AddControllers();

                // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                var app = builder.Build();


                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }
                app.UseRouting();
                app.UseCors("AllowAnyOrigin");
                app.UseAuthentication(); // Kích ho?t middleware xác th?c
                app.UseAuthorization();

                app.MapControllers();

                app.Run();

            }
            //DeviceTemplate.loadTemplate();
            mqttHandle = new HandleMQTT();
            mqttHandle.subscribe();

            while (true)
            {
                try
                {
                    checkStatus();
                    Thread.Sleep(10 * check_time);
                    if (!mqttHandle.mqttClient.IsConnected)
                        mqttHandle.subscribe();
                    //saveLog("test");

                }
                catch (Exception ex)
                {
                    saveLog("Check Status: -> " + ex.Message);
                }
            }
        }

        public static bool AdminAccountExist(Dictionary<string, User> Data)
        {
            if (Data.ContainsKey(Program.Admin_username))
                return true;
            else
                return false;
        }

        public static void readConfig()
        {
            try
            {
                string filepath = startupPath + @"/";
                if (File.Exists(@"" + filepath + "config.txt"))
                {
                    var s = File.ReadAllLines(filepath + "config.txt");
                    foreach (var str in s)
                    {
                        if (str.Length > 0)
                        {
                            string[] tmp = str.Split("\t");
                            switch (tmp[0])
                            {
                                case "version:":
                                    string version = tmp[1];
                                    Program.version = version;
                                    break;
                                case "API port:":
                                    //int[] APIport = new int[tmp.Length - 1];
                                    List<int> APIport = new List<int>();
                                    for (int i = 0; i < tmp.Length - 1; i++)
                                    {
                                        if (tmp[i + 1] != "")
                                            //APIport[i] = int.Parse(tmp[i + 1]);
                                            APIport.Add(int.Parse(tmp[i + 1]));
                                    }
                                    Program.APIport = APIport.ToArray();
                                    break;
                                case "Admin account:":
                                    if (tmp[1] != "" && tmp[2] != "")
                                    {
                                        string admin_user = tmp[1];
                                        string admin_pass = tmp[2];
                                        Program.Admin_username = admin_user;
                                        Program.Admin_password = admin_pass;
                                    }
                                    break;
                                case "Check status(s):":
                                    if (tmp[1] != "")
                                    {
                                        int check_time = int.Parse(tmp[1]);
                                        Program.check_time = check_time;
                                    }
                                    break;
                                case "MongoDB Connection:":
                                    if (tmp[1] != "")
                                    {
                                        string MongoConnection = tmp[1];
                                        Program.MongoConnection = MongoConnection;
                                    }
                                    break;
                                case "Project:":
                                    HandleMQTT.projectList = tmp[1].Split(',').ToList();
                                    break;
                                case "Database:":
                                    if (tmp[1] != "")
                                    {
                                        string Database = tmp[1];
                                        Program.Database = Database;
                                    }
                                    break;
                                case "Hostname:":
                                    if (tmp[1] != "")
                                    {
                                        string hostname = tmp[1];
                                        Program.hostname = hostname;
                                    }
                                    break;
                                case "MQTTUrl:":
                                    if (tmp[1] != "")
                                        HandleMQTT.url = tmp[1];
                                    break;
                                case "MQTTclientid:":
                                    if (tmp[1] != "")
                                        HandleMQTT.clientID = tmp[1];
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Program.saveLog("config file dont exist!");
                    Console.WriteLine("config file dont exist!");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog(ex.ToString());
            }
        }

        public static void checkStatus()
        {
            //   Console.WriteLine(Program.getTime().ToString()+"check");
            foreach (string key in Devices.Keys)
            {
                if (Devices[key].Status)// true state
                {
                    if (Program.getTime().ToOADate() - Devices[key].lastTimeSystem > 0.007) //disconnected = 10 mins = 0.007
                    {
                        Devices[key].Status = false;
                    }
                }

            }
        }

        public static void saveLog(string log)
        {
            try
            {
                File.AppendAllText("log.txt", "[" + Program.getTime().ToString("dd/MM/yyyy HH:mm:ss") + "]: " + log + "\r\n");
            }
            catch (Exception)
            {
                //Console.WriteLine("savelog ex : " + ex.Message);
            }
        }

        public static void actionLog(string log)
        {
            try
            {
                File.AppendAllText("actionlog.txt", "[" + Program.getTime().ToString("dd/MM/yyyy HH:mm:ss") + "]: " + log + "\r\n");
            }
            catch (Exception)
            {
                //Console.WriteLine("actionLog ex : " + ex.Message);
            }
        }

        public static void User_replace(User us)
        {
            var filter = Builders<User>.Filter.Eq("username", us.username);
            User_Collection.ReplaceOne(filter, us);
        }

        public static void User_delete(string us)
        {
            var filter = Builders<User>.Filter.Eq("username", us);
            User_Collection.DeleteOne(filter);
        }

        public static void Device_replace(Device dv)
        {
            var filter = Builders<Device>.Filter.Eq("Device_id", dv.Device_id);
            Device_Collection.ReplaceOne(filter, dv);
        }

        public static void Device_delete(string device_id)
        {
            var filter = Builders<Device>.Filter.Eq("Device_id", device_id);
            Device_Collection.DeleteOne(filter);
        }

        public static void WriteLine(string content)
        {
            Console.WriteLine("[" + Program.getTime().ToString("yyyy-MM-dd HH:mm:ss") + "]\t" + content);
        }

        public static DateTime getTime()
        {
            return DateTime.UtcNow;
        }
    }
}
