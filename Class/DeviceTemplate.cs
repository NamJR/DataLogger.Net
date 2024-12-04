using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
//using Nancy;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DataLogger_NetCore.Class
{
    [BsonIgnoreExtraElements]
    public class DeviceTemplate
    {
        public static Dictionary<int, DeviceTemplate> listTemplate = new Dictionary<int, DeviceTemplate>();
        public static Dictionary<int, Dictionary<string, Dictionary<string, configInput>>> userTypeParamTemplate = new Dictionary<int, Dictionary<string, Dictionary<string, configInput>>>();
        [BsonId]
        public int typecode;
        public string typename;
        public Dictionary<string, configInput> paramTemplate;
        public string otalink1, otalink2;

        [JsonConstructor]
        public DeviceTemplate(int typecode, string typename, Dictionary<string, configInput> paramTemplate, string otalink1, string otalink2)
        {
            this.typecode = typecode;
            this.typename = typename;
            this.paramTemplate = paramTemplate;
            this.otalink1 = otalink1;
            this.otalink2 = otalink2;
        }

        public static void loadDb()
        {
            var templates = Program.Template_Collection.Find(new BsonDocument()).ToList();
            if (templates.Count == 0)
            {
                var newTemplate = new DeviceTemplate(0, "No Type", new Dictionary<string, configInput>(), "", "");
                Program.Template_Collection.ReplaceOne(x => x.typecode == newTemplate.typecode, newTemplate, new ReplaceOptions() { IsUpsert = true });
                templates.Add(newTemplate);
            }
            foreach (var it in templates)
            {
                listTemplate[it.typecode] = it;
                buildUserTypeParamTemplate(it.typecode);
            }
        }

        public static bool updateTemplate(DeviceTemplate t)
        {
            try
            {
                var result = Program.Template_Collection.ReplaceOne(x => x.typecode == t.typecode, t, new ReplaceOptions() { IsUpsert = true });
                listTemplate[t.typecode] = t;
                buildUserTypeParamTemplate(t.typecode);
                if (result.IsAcknowledged && result.ModifiedCount > 0) return true;
            }
            catch (Exception e)
            {
                Program.saveLog(e.ToString());
            }
            return false;
        }

        public static DeviceTemplate findTemplate(int typecode)
        {

            var t = listTemplate.Values.FirstOrDefault(x => x.typecode == typecode);
            return t;
        }
        public static bool deleteTemplate(int typecode)
        {
            try
            {
                var t = listTemplate.Values.FirstOrDefault(x => x.typecode == typecode);
                if (t != null)
                {
                    var result = Program.Template_Collection.DeleteOne(x => x.typecode == t.typecode);
                    listTemplate.Remove(t.typecode);
                    foreach (var device in Program.Devices.Values.ToArray())
                    {
                        if (device.type == typecode)
                        {
                            setType(device, 0);
                            Program.Device_replace(device);
                        }
                    }
                    if (result.IsAcknowledged && result.DeletedCount > 0) return true;
                }
            }
            catch (Exception e)
            {
                Program.saveLog(e.ToString());
            }
            return false;
        }

        public static void loadTemplate()
        {
            var listTmp = new Dictionary<int, DeviceTemplate>();
            if (File.Exists("template.csv"))
            {
                var codeList = File.ReadAllLines("template.csv");
                foreach (var str in codeList)
                {
                    try
                    {
                        var strSplit = str.Split(',');
                        if (strSplit.Length <= 1) continue;
                        var template = new DeviceTemplate(int.Parse(strSplit[0]), strSplit[1], new Dictionary<string, configInput>(), "", "");
                        if (File.Exists(template.typecode + "_template.csv"))
                        {
                            var configList = File.ReadAllLines(template.typecode + "_template.csv");
                            foreach (var configStr in configList)
                            {
                                var c = configStr.Split(',');
                                if (c.Length <= 1) continue;
                                string controlCmd = "";
                                string controlValue = "";
                                if (c.Length > 9)
                                {
                                    controlCmd = c[9];
                                    controlValue = c[10];
                                }
                                configInput conf = new configInput(c[0], c[1], c[2], c[3] == "" ? 1.0 : double.Parse(c[3]), c[4] == "" ? 0.0 : double.Parse(c[4]),
                                    c[5] == "" ? 1.0 : double.Parse(c[5]), c[6] == "" ? 0.0 : double.Parse(c[6]), c[7].ToLowerInvariant() == "true" ? true : false,
                                    c[8].ToLowerInvariant() == "true" ? true : false, 0, 0, controlCmd, controlValue, new List<byte>() { 2, 2, 2 });
                                template.paramTemplate[conf.numInput] = conf;
                            }
                        }
                        updateTemplate(template);
                        listTmp[template.typecode] = template;
                    }
                    catch (Exception e)
                    {
                        Program.saveLog(e.ToString());
                        continue;
                    }
                }
            }
            listTemplate = listTmp;
        }

        public static void setType(Device d, int type)
        {
            if (listTemplate.ContainsKey(type))
                d.type = type;
        }

        public static string getName(int type)
        {
            return listTemplate.ContainsKey(type) ? listTemplate[type].typename : "UNKNOWN";
        }

        private static void buildUserTypeParamTemplate(int typecode)
        {
            var template = listTemplate[typecode];
            if (!userTypeParamTemplate.ContainsKey(typecode))
                userTypeParamTemplate[typecode] = new Dictionary<string, Dictionary<string, configInput>>();
            foreach (var userRol in new string[] { "admin", "engineer", "user" })
            {
                var conf = new Dictionary<string, configInput>();
                foreach (var input in template.paramTemplate.Values)
                    if (permissionCheck(userRol, input.permission, "READ"))
                        conf.Add(input.numInput, input);
                userTypeParamTemplate[typecode][userRol] = conf;
            }
        }

        // Hàm gọi parameter config template
        public static Dictionary<string, configInput> getTemplate(int typecode)
        {
            var template = listTemplate[typecode];
            var conf = new Dictionary<string, configInput>();
            foreach (var input in template.paramTemplate.Values)
            {
                conf.Add(input.numInput, input);
            }
            return conf;
        }
        // Không dùng được getConfig
        public static Dictionary<string, configInput> getConfig(User user, string deviceid)
        {
            var conf = new Dictionary<string, configInput>();
            if (!Program.Devices.ContainsKey(deviceid)) return conf;
            var device = Program.Devices[deviceid];
            if (!user.listDevices.Contains(deviceid)) return conf;
            if (!listTemplate.ContainsKey(device.type)) return conf;
            if (!userTypeParamTemplate[device.type].ContainsKey(user.permission)) return conf;
            return userTypeParamTemplate[device.type][user.permission];
        }

        public static Dictionary<string, configInput> getConfig(string deviceid)
        {
            var conf = new Dictionary<string, configInput>();
            if (!Program.Devices.ContainsKey(deviceid)) return conf;
            var device = Program.Devices[deviceid];
            if (!listTemplate.ContainsKey(device.type)) return conf;

            return listTemplate[device.type].paramTemplate;
        }

        public static bool permissionCheck(User user, string deviceid, string numInput, string permission = "READ")
        {
            if (!Program.Devices.ContainsKey(deviceid)) return false;
            var device = Program.Devices[deviceid];
            if (!user.listDevices.Contains(deviceid)) return false;
            if (!listTemplate.ContainsKey(device.type)) return false;
            var template = listTemplate[device.type];
            if (!template.paramTemplate.ContainsKey(numInput)) return false;
            var config = template.paramTemplate[numInput];
            return permissionCheck(user.permission, config.permission, permission);
        }

        public static bool permissionCheck(string username, Device device, string numInput, string permission = "READ")
        {
            if (!Program.Users.ContainsKey(username)) return false;
            if (!listTemplate.ContainsKey(device.type)) return false;
            var template = listTemplate[device.type];
            if (!template.paramTemplate.ContainsKey(numInput)) return false;
            var config = template.paramTemplate[numInput];
            return permissionCheck(Program.Users[username].permission, config.permission, permission);
        }

        private static bool permissionCheck(string role, List<byte> permission, string permissionType)
        {
            if (role == "engineer") return true;
            if (permissionType == "READ" && permission[2] >= 1) return true;
            if (permissionType == "WRITE" && permission[2] >= 2) return true;
            return false;
        }
    }
}
