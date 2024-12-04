using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DataLogger_NetCore.Class;
using MQTTnet.Client;
using MQTTnet;
using System.Threading.Tasks;
using System.Threading;
using DataLogger_NetCore.Notifications;
using SharpCompress.Common;
using Newtonsoft.Json;

namespace DataLogger_NetCore.MQTT
{
    public class HandleMQTT
    {
        public IMqttClient mqttClient;
        public bool connected = false;
        public static List<string> projectList = new List<string>();
        public static Dictionary<string, string> projectDevice = new Dictionary<string, string>();
        public static string url = "white-dev.aithings.vn:1883";
        public static string clientID = "backendWhite01";
        public HandleMQTT()
        {
        }

        Task handleMsg(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                //Console.WriteLine(e.ApplicationMessage.Topic + "\t" + e.ApplicationMessage.ConvertPayloadToString());
                var prefix = e.ApplicationMessage.Topic.Split('/');
                string id = prefix[1];
                lock (string.Intern(id))
                {
                    updateData(prefix[0], id, e.ApplicationMessage.ConvertPayloadToString());
                }
            }
            catch (Exception ex)
            {
                Program.saveLog(ex.ToString());
            }
            return Task.CompletedTask;
        }

        Task handleDisconnected(MqttClientDisconnectedEventArgs e)
        {
            connected = false;
            return Task.CompletedTask;
        }

        public async void subscribe()
        {
            try
            {
                var mqttFactory = new MqttFactory();

                mqttClient = mqttFactory.CreateMqttClient();

                var mqtturl = url.Split(':');
                var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(mqtturl[0], int.Parse(mqtturl[1]))
                    .WithClientId(clientID)
                    .WithCleanSession(false)
                    .WithCredentials(clientID, "6731e9150e8089ae")
                    .Build();

                // Setup message handling before connecting so that queued messages
                // are also handled properly. When there is no event handler attached all
                // received messages get lost.
                mqttClient.ApplicationMessageReceivedAsync += handleMsg;

                await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                if (projectList.Count == 0) projectList.Add("white");
                foreach (var item in projectList)
                {
                    var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        f =>
                        {
                            f.WithTopic(item + "/+/data");
                        })
                    .Build();

                    await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                    Console.WriteLine("MQTT subscription ok: " + item);
                }
            }
            catch (Exception)
            { }
        }

        void updateData(string project, string id, string mqttData)
        {
            var dataString = mqttData.Split('\t');
            string timestamp = dataString[0];
            string device_id = id.ToString();
            projectDevice[id] = project;
            if (Program.Devices.ContainsKey(device_id))
            {
                Program.Devices[device_id].lastTimeSystem = Program.getTime().ToOADate();
                Program.Devices[device_id].Status = true;
            }
            DateTime temp;
            DateTime.TryParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out temp);
            if (temp.Year < 2023 || (temp - DateTime.UtcNow).TotalDays > 1)
            {
                sendCommand(device_id, "c:time:" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                return;
            }

            Dictionary<string, string> codedata = new Dictionary<string, string>();
            Dictionary<string, object> numdata = new Dictionary<string, object>();
            Dictionary<string, string> strdata = new Dictionary<string, string>();
            for (int i = 1; i < dataString.Length; i++)
            {
                var str = dataString[i];
                var s = str.Split(':');
                if (s.Length == 2)
                {
                    codedata[s[0]] = s[1];
                    double tmpval;
                    if (double.TryParse(s[1], out tmpval))
                    {
                        numdata[s[0]] = tmpval;
                    }
                    else if (s[0].StartsWith("UUID"))
                        strdata[s[0]] = s[1];
                }
            }

            DeviceData.getDevice(id).insertData(temp, codedata);
            double receivedtime = temp.ToOADate();

            if (Program.Devices.ContainsKey(device_id))
            {
                var device = Program.Devices[device_id];

                if (numdata.ContainsKey("TYPE") && Convert.ToDouble(numdata["TYPE"]) != device.type)
                    Program.mqttHandle.sendCommand(device.Device_id, "c:type:" + device.type); //resync device type if necessary

                if (device.lastReceived - DateTime.UtcNow.ToOADate() > 1 || device.lastReceived <= receivedtime)
                {
                    device.lastReceived = receivedtime;
                    device.lastTimeSystem = Program.getTime().ToOADate();
                    if (device.Status == false)
                        device.Status = true;

                    var config = DeviceTemplate.getConfig(device.Device_id);
                    var lastvalues = AccessData.DataCal(numdata, config, device.extraconfig);
                    foreach (var entry in lastvalues)
                    {
                        var inputCode = entry.Key;
                        if (!device.lastData.ContainsKey(inputCode))
                            device.lastData[inputCode] = new LastDataPoint();
                        device.lastData[inputCode].value = entry.Value;
                        device.lastData[inputCode].timestamp = temp;

                        if (!inputCode.StartsWith("UUID") && config.ContainsKey(inputCode))
                        {
                            var val = Convert.ToDouble(entry.Value);
                            if (config[inputCode].high_level != 0)
                            {
                                var highwarningstatus = Program.highWarningList.ContainsKey(device_id + ":" + inputCode);
                                if (!highwarningstatus && val >= config[inputCode].high_level)
                                {
                                    string grade = config[inputCode].critical ? "CRITICAL" : "WARNING";
                                    Program.highWarningList.Add(device_id + ":" + inputCode, 1);
                                    ActivityLog.insertData(DateTime.UtcNow, "System", device_id, grade + ": " + config[inputCode].name
                                        + " value: " + val + " exceeds HIGH warning level.", config[inputCode].critical ? 2 : 1);
                                    OneSignalService.createNotification(device, grade + ": device: " + device.Device_name + " channel: " + config[inputCode].name
                                        + " value: " + val + " exceeds HIGH warning level.", device.filterNotiList(inputCode));
                                }
                                else if (highwarningstatus && val < config[inputCode].high_level)
                                    Program.highWarningList.Remove(device_id + ":" + inputCode);
                            }
                            if (config[inputCode].low_level != 0)
                            {
                                var lowwarningstatus = Program.lowWarningList.ContainsKey(device_id + ":" + inputCode);
                                if (!lowwarningstatus && val <= config[inputCode].low_level)
                                {
                                    string grade = config[inputCode].critical ? "CRITICAL" : "WARNING";
                                    Program.lowWarningList.Add(device_id + ":" + inputCode, 1);
                                    ActivityLog.insertData(DateTime.UtcNow, "System", device_id, grade + ": " + config[inputCode].name
                                        + " value: " + val + " exceeds LOW warning level.", config[inputCode].critical ? 2 : 1);
                                    OneSignalService.createNotification(device, grade + ": device: " + device.Device_name + " channel: " + config[inputCode].name
                                        + " value: " + val + " exceeds LOW warning level.", device.filterNotiList(inputCode));
                                }
                                else if (lowwarningstatus && val > config[inputCode].low_level)
                                    Program.lowWarningList.Remove(device_id + ":" + inputCode);
                            }
                        }
                    }
                    foreach (var entry in strdata)
                    {
                        if (!device.lastData.ContainsKey(entry.Key)) device.lastData[entry.Key] = new LastDataPoint();
                        device.lastData[entry.Key].timestamp = temp;
                        device.lastData[entry.Key].value = entry.Value;
                    }
                    Program.Device_replace(Program.Devices[id.ToString()]);
                }
            }
            else
            {
                Dictionary<string, LastDataPoint> lastData = new Dictionary<string, LastDataPoint>();
                foreach (var entry in codedata)
                {
                    lastData[entry.Key] = new LastDataPoint();
                    lastData[entry.Key].value = entry.Value;
                    lastData[entry.Key].timestamp = temp;
                }
                Program.Devices.Add(device_id, new Device(device_id, true, device_id, 21.029472, 105.785448, -1, receivedtime, lastData, 0, "", Program.getTime().ToOADate()));//VSI location
                Program.Devices[device_id].version_running = "0";
                Program.Users[Program.Admin_username].listDevices.Add(device_id);
                Program.Device_Collection.InsertOne(Program.Devices[id.ToString()]);
            }

            //OneSignalService.createNotification(Program.Devices[device_id], mqttData);
        }

        public bool sendCommand(string device_id, string command)
        {
            if (projectDevice.ContainsKey(device_id))
            {
                return mqttClient.PublishStringAsync(projectDevice[device_id] + "/" + device_id + "/control", command).Result.IsSuccess;
            }
            return false;
        }
    }
}
