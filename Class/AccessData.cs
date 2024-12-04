
using MongoDB.Bson.IO;
using OneSignalApi.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataLogger_NetCore.Class
{
    public class AccessData
    {
        public static bool isRaw;
        public static bool ChangeDeviceUser(string device_id, string device_name, double latitude, double longitude, string area, Dictionary<string, configInput> extraconfig)
        {
            double timenow = Program.getTime().ToOADate();
            if (Program.Devices.ContainsKey(device_id))
            {
                var device = Program.Devices[device_id];
                device.Device_name = device_name;
                device.latitude = latitude;
                device.longitude = longitude;
                device.lastEdit = timenow;
                device.area = area;
                var extraconf = new Dictionary<string, configInput>();
                foreach (var s in extraconfig)
                    extraconf[s.Key] = s.Value;
                device.extraconfig = extraconf;
                return true;
            }
            return false;
        }
        public static Dictionary<string, Device> ChangeDeviceAdmin(string device_id, string device_name, double latitude, double longitude, string area)
        {
            double timenow = Program.getTime().ToOADate();
            Dictionary<string, Device> objData = new Dictionary<string, Device>(Program.Devices);
            objData[device_id].Device_name = device_name;
            objData[device_id].latitude = latitude;
            objData[device_id].longitude = longitude;
            objData[device_id].lastEdit = timenow;
            objData[device_id].area = area;
            // PacketProcessor.writeDevice(objData);
            return objData;
        }
        public static Device_respond DV_res(User user, string device_id)
        {
            Device_respond dv = new Device_respond();
            var dev = Program.Devices[device_id];
            dv.Device_id = dev.Device_id;
            dv.Device_name = dev.Device_name;
            dv.lastData = dev.lastData;
            dv.lastEdit = dev.lastEdit;
            dv.lastReceived = dev.lastTimeSystem;
            dv.lastTimeSystem = dev.lastTimeSystem;
            dv.config = DeviceTemplate.getConfig(user, device_id);
            dv.area = dev.area == null ? "" : dev.area;
            dv.latitude = dev.latitude;
            dv.longitude = dev.longitude;
            dv.Status = dev.Status;
            dv.type = dev.type;
            dv.version_running = dev.version_running;
            dv.supportEngineer = dev.supportEngineer;
            dv.owner = dev.owner;
            dv.extraconfig = dev.extraconfig;
            dv.cameraId = dev.cameraId;
            return dv;
        }

        //datacal for a collection of multiple types of data
        public static Dictionary<string, object> DataCal(Dictionary<string, object> inputData, Dictionary<string, configInput> config, Dictionary<string, configInput> extraconfig)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            double a, b;
            if (inputData != null && config != null)
                foreach (string keyValue in inputData.Keys)
                    if (inputData[keyValue] is double input)
                    {
                        configInput conf = null;
                        if (extraconfig.ContainsKey(keyValue))
                            conf = extraconfig[keyValue];
                        else if (config.ContainsKey(keyValue))
                            conf = config[keyValue];

                        if (conf != null)
                        {
                            a = (conf.Ymax - conf.Ymin) / (conf.Xmax - conf.Xmin); //(Ymax - Ymin) / (Xmax - Xmin);
                            b = (conf.Xmax * conf.Ymin - conf.Ymax * conf.Xmin) / (conf.Xmax - conf.Xmin);
                            result[keyValue] = Math.Round((a * input + b) * 100) / 100;
                        }
                        else result[keyValue] = Math.Round(input * 100) / 100;
                    }
                    else result[keyValue] = inputData[keyValue];
            return result;
        }

        //datacal for timestamp collection of 1 type of data
        public static Dictionary<T, object> DataCal<T>(Dictionary<T, object> inputData, Dictionary<string, configInput> config, string keyValue, Dictionary<string, configInput> extraconfig)
        {
            //if (config == null || config.Count == 0)
            //{
            //    isRaw = true;
            //    return inputData;
            //}
            //isRaw = false;
            configInput conf = null;
            if (extraconfig.ContainsKey(keyValue))
                conf = extraconfig[keyValue];
            else if (config.ContainsKey(keyValue))
                conf = config[keyValue];


            if (conf != null)
            {
                Dictionary<T, object> result = new Dictionary<T, object>();
                double a, b;
                foreach (var t in inputData.Keys)
                {
                    if (inputData[t] is double input)
                    {
                        a = (conf.Ymax - conf.Ymin) / (conf.Xmax - conf.Xmin); //(Ymax - Ymin) / (Xmax - Xmin);
                        b = (conf.Xmax * conf.Ymin - conf.Ymax * conf.Xmin) / (conf.Xmax - conf.Xmin);
                        result[t] = Math.Round((a * input + b) * 100) / 100;
                    }
                    else result[t] = inputData[t];
                }
                return result;
            }
            else return inputData;

        }
    }
}

