using DataLogger_NetCore.Class;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using System.Dynamic;
using Newtonsoft.Json;
using MongoDB.Driver;

namespace DataLogger_NetCore.API
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    public class DataAPIController : ControllerBase
    {
        // GET: api/DataAPI/ping
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            dynamic rs = new ExpandoObject();
            rs.status = "Running";
            rs.version = Constants.api_version;
            rs.whatnews = Constants.whatnews;

            return Ok(rs);
        }
        // POST: api/DataAPI/ReadDeviceDataByDayInput
        [HttpPost("ReadDeviceDataByDayInput")]
        public IActionResult ReadDeviceDataByDayInput([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Validate token
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    return Ok("Incorrect input data"); // Token không hợp lệ
                }

                // Kiểm tra user tồn tại
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    return Ok("Account not exist"); // Tài khoản không tồn tại
                }

                // Tìm thiết bị dựa trên device_id
                var device = Program.Devices[request.device_id];
                List<string> listInput;

                // Kiểm tra quyền truy cập
                if (!string.IsNullOrEmpty(request.numInput))
                {
                    if (!DeviceTemplate.permissionCheck(vTmpUser, request.device_id, request.numInput))
                    {
                        return Ok("Permission Denied"); // Không đủ quyền truy cập
                    }
                    listInput = new List<string> { request.numInput };
                }
                else
                {
                    listInput = DeviceTemplate.getConfig(vTmpUser, request.device_id).Keys.ToList();
                }

                // Thiết lập thời gian bắt đầu và kết thúc
                DateTime fromDate = new DateTime(request.year, request.month, request.day, 0, 0, 0);
                DateTime toDate = new DateTime(request.year, request.month, request.day, 23, 59, 59);

                if (request.hour != 0)
                {
                    fromDate = fromDate.AddHours(request.hour);
                    toDate = toDate.AddHours(request.hour);
                }

                // Lấy dữ liệu thiết bị
                var devicedata = DeviceData.getDevice(request.device_id);
                var data_recv = devicedata.searchData(fromDate, toDate);

                Dictionary<double, object> res = new Dictionary<double, object>();
                double val = 0.0;

                // Xử lý dữ liệu
                foreach (var datedata in data_recv)
                {
                    if (datedata.codedata.ContainsKey(request.numInput))
                    {
                        if (request.numInput.StartsWith("UUID"))
                        {
                            res[datedata.datetime.ToOADate()] = datedata.codedata[request.numInput];
                        }
                        else if (double.TryParse(datedata.codedata[request.numInput], out val))
                        {
                            res[datedata.datetime.ToOADate()] = val;
                        }
                    }
                }

                // Tính toán dữ liệu nếu không phải là UUID
                if (!request.numInput.StartsWith("UUID"))
                {
                    res = AccessData.DataCal(res, DeviceTemplate.getConfig(request.device_id), request.numInput, device.extraconfig);
                }

                // Trả về dữ liệu dưới dạng JSON
                return Ok(res);
            }
            catch (Exception ex)
            {
                // Log lỗi và trả về phản hồi thất bại
                Program.saveLog($"API: api/DataAPI/ReadDeviceDataByDayInput -> {ex}");
                return StatusCode(417, ex.Message); // HTTP 417 - Expectation Failed
            }
        }
        // POST: api/DataAPI/ReadDeviceDataByDay
        [HttpPost("ReadDeviceDataByDay")]
        public IActionResult ReadDeviceDataByDay([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Kiểm tra token key
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                // Xác thực người dùng
                User vTmpUser = Program.Users.Values
                    .Where(p => p.username == vKey.UserName && p.password == vKey.Password)
                    .FirstOrDefault();

                if (vTmpUser == null)
                    return Ok("Account not exist");

                // Lấy thông tin thiết bị và cấu hình thiết bị
                var device = Program.Devices[request.device_id];
                var listInput = DeviceTemplate.getConfig(vTmpUser, request.device_id);
                Dictionary<string, Dictionary<double, object>> res = new Dictionary<string, Dictionary<double, object>>();
                Dictionary<string, bool> calList = new Dictionary<string, bool>();

                // Nếu có danh sách đầu vào
                if (listInput.Count > 0)
                {
                    foreach (var item in listInput.Keys)
                        res[item] = new Dictionary<double, object>();

                    // Thiết lập thời gian từ và thời gian đến
                    DateTime fromDate = new DateTime(request.year, request.month, request.day, 0, 0, 0);
                    DateTime toDate = new DateTime(request.year, request.month, request.day, 23, 59, 59);

                    // Nếu yêu cầu có giờ cụ thể
                    if (request.hour != 0)
                    {
                        fromDate = fromDate.AddHours(request.hour);
                        toDate = toDate.AddHours(request.hour);
                    }

                    // Lấy dữ liệu thiết bị
                    var devicedata = DeviceData.getDevice(request.device_id);
                    var data_recv = devicedata.searchData(fromDate, toDate);
                    double val = 0.0;

                    // Xử lý dữ liệu nhận được
                    foreach (var datedata in data_recv)
                    {
                        double oaDateTime = datedata.datetime.ToOADate();
                        foreach (var numInput in res.Keys)
                        {
                            if (datedata.codedata.ContainsKey(numInput))
                            {
                                if (numInput.StartsWith("UUID"))
                                    res[numInput][oaDateTime] = datedata.codedata[numInput];
                                else if (double.TryParse(datedata.codedata[numInput], out val))
                                    res[numInput][oaDateTime] = val;
                            }
                        }
                    }

                    // Tính toán dữ liệu nếu cần thiết
                    foreach (var numInput in res.Keys)
                    {
                        if (!numInput.StartsWith("UUID"))
                        {
                            res[numInput] = AccessData.DataCal(res[numInput],
                                DeviceTemplate.getConfig(request.device_id),
                                numInput,
                                device.extraconfig);
                        }
                    }
                }

                // Trả về kết quả dưới dạng JSON
                return Ok(res);
            }
            catch (Exception ex)
            {
                Program.saveLog("API: api/DataAPI/ReadDeviceDataByDay -> " + ex.ToString());
                return StatusCode(417, ex.Message); // ExpectationFailed
            }
        }
        [HttpPost("DeleteDevice")]
        public IActionResult DeleteDevice([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm người dùng dựa trên thông tin token
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                {
                    return Ok("Tài khoản không tồn tại");
                }

                if (vTmpUser.permission == "admin")
                {
                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        // Xóa thiết bị khỏi người dùng
                        vTmpUser.listDevices.Remove(request.device_id);

                        if (Program.Devices.ContainsKey(request.device_id))
                        {
                            Program.Devices[request.device_id].notiUserList.Remove(vTmpUser.username);
                        }

                        // Cập nhật danh sách khu vực sau khi xóa thiết bị
                        List<string> lsAreas = new List<string>();
                        foreach (var key in vTmpUser.listDevices)
                        {
                            if (!lsAreas.Contains(Program.Devices[key].area))
                            {
                                lsAreas.Add(Program.Devices[key].area);
                            }
                        }

                        vTmpUser.listAreas = lsAreas;
                        Program.User_replace(Program.Users[vTmpUser.username]);

                        // Ghi lại log thao tác xóa thiết bị
                        ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "DeleteDevice");

                        return Ok(Program.Users[vTmpUser.username]);
                    }
                    else
                    {
                        return Ok("Thiết bị không tồn tại");
                    }
                }
                else
                {
                    return Ok("Không có quyền truy cập");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: api/DataAPI/DeleteDevice -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("AdminAddDevice")]
        public IActionResult AdminAddDevice([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm người dùng dựa trên token
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                {
                    return Ok("Tài khoản không tồn tại");
                }

                if (vTmpUser.permission == "admin" || vTmpUser.permission == "engineer")
                {
                    // Kiểm tra xem thiết bị đã tồn tại trong hệ thống chưa
                    if (!Program.Devices.ContainsKey(request.device_id))
                    {
                        Device newDevice = new Device(
                            request.device_id,
                            false,
                            request.device_id,
                            21.029472,
                            105.785448,
                            -1,
                            -1,
                            new Dictionary<string, LastDataPoint>(),
                            0,
                            "",
                            Program.getTime().ToOADate()
                        );
                        Program.Devices.Add(newDevice.Device_id, newDevice);
                        Program.Device_Collection.InsertOne(Program.Devices[request.device_id]);
                    }

                    // Kiểm tra xem username có tồn tại không
                    if (Program.Users.ContainsKey(request.username))
                    {
                        // Kiểm tra xem thiết bị đã được thêm cho người dùng này chưa
                        if (Program.Users[request.username].listDevices.Contains(request.device_id))
                        {
                            return Ok("Thiết bị đã tồn tại cho người dùng");
                        }
                        else
                        {
                            // Thêm thiết bị cho người dùng
                            Program.Devices[request.device_id].area = request.area;
                            Program.Users[request.username].listDevices.Add(request.device_id);
                            Program.Devices[request.device_id].addNotiList(request.username);

                            // Nếu thiết bị chưa có trong danh sách thiết bị của admin/engineer, thêm vào
                            if (!vTmpUser.listDevices.Contains(request.device_id))
                            {
                                vTmpUser.listDevices.Add(request.device_id);
                            }

                            // Cập nhật danh sách khu vực của người dùng
                            List<string> userAreas = new List<string>();
                            foreach (var deviceId in Program.Users[request.username].listDevices)
                            {
                                if (!userAreas.Contains(Program.Devices[deviceId].area))
                                {
                                    userAreas.Add(Program.Devices[deviceId].area);
                                }
                            }
                            Program.Users[request.username].listAreas = userAreas;

                            // Cập nhật danh sách khu vực của admin/engineer
                            List<string> adminAreas = new List<string>();
                            foreach (var deviceId in vTmpUser.listDevices)
                            {
                                if (!adminAreas.Contains(Program.Devices[deviceId].area))
                                {
                                    adminAreas.Add(Program.Devices[deviceId].area);
                                }
                            }
                            vTmpUser.listAreas = adminAreas;

                            // Tạo bản sao dữ liệu người dùng để gửi trả
                            Dictionary<string, object> usersLoad = new Dictionary<string, object>();
                            foreach (var key in Program.Users.Keys)
                            {
                                usersLoad[key] = Program.Users[key];
                            }

                            // Cập nhật thiết bị và người dùng trong hệ thống
                            Program.Device_replace(Program.Devices[request.device_id]);
                            Program.User_replace(vTmpUser);
                            Program.User_replace(Program.Users[request.username]);

                            // Ghi lại log thao tác
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Thêm thiết bị cho người dùng " + request.username);

                            return Ok(usersLoad);
                        }
                    }
                    else
                    {
                        return Ok("Tài khoản username không tồn tại");
                    }
                }
                else
                {
                    return Ok("Không có quyền truy cập");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: api/DataAPI/AdminAddDevice -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("AdminDeleteDevice")]
        public IActionResult AdminDeleteDevice([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm người dùng dựa trên token
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                {
                    return Ok("Tài khoản không tồn tại");
                }

                if (vTmpUser.permission == "admin")
                {
                    // Kiểm tra username có tồn tại trong hệ thống không
                    if (Program.Users.ContainsKey(request.username))
                    {
                        // Kiểm tra xem thiết bị có trong danh sách thiết bị của người dùng không
                        if (Program.Users[request.username].listDevices.Contains(request.device_id))
                        {
                            // Xóa thiết bị khỏi danh sách thiết bị của người dùng
                            Program.Users[request.username].listDevices.Remove(request.device_id);

                            // Cập nhật danh sách khu vực của người dùng
                            List<string> userAreas = new List<string>();
                            foreach (var key in Program.Users[request.username].listDevices)
                            {
                                if (!userAreas.Contains(Program.Devices[key].area))
                                {
                                    userAreas.Add(Program.Devices[key].area);
                                }
                            }
                            Program.Users[request.username].listAreas = userAreas;

                            // Cập nhật danh sách khu vực của admin
                            List<string> adminAreas = new List<string>();
                            foreach (var key in vTmpUser.listDevices)
                            {
                                if (!adminAreas.Contains(Program.Devices[key].area))
                                {
                                    adminAreas.Add(Program.Devices[key].area);
                                }
                            }
                            vTmpUser.listAreas = adminAreas;

                            // Tạo bản sao dữ liệu người dùng để gửi trả
                            Dictionary<string, object> usersLoad = new Dictionary<string, object>();
                            foreach (var key in Program.Users.Keys)
                            {
                                if (Program.Users[key].permission != "root")
                                {
                                    usersLoad[key] = Program.Users[key];
                                }
                            }

                            // Cập nhật thiết bị và người dùng trong hệ thống
                            Program.Device_replace(Program.Devices[request.device_id]);
                            Program.User_replace(vTmpUser);
                            Program.User_replace(Program.Users[request.username]);

                            // Xóa người dùng khỏi danh sách thông báo của thiết bị
                            Program.Devices[request.device_id].notiUserList.Remove(request.username);

                            // Ghi lại log thao tác
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Xóa thiết bị khỏi người dùng " + request.username);

                            return Ok(usersLoad);
                        }
                        else
                        {
                            return Ok("Thiết bị không tồn tại trong danh sách của người dùng");
                        }
                    }
                    else
                    {
                        return Ok("Tài khoản username không tồn tại");
                    }
                }
                else
                {
                    return Ok("Không có quyền truy cập");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: api/DataAPI/AdminDeleteDevice -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("ReadDeviceDetails")]
        public IActionResult ReadDeviceDetails([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Kiểm tra người dùng có quyền truy cập thiết bị không
                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        // Nếu thiết bị tồn tại trong hệ thống
                        if (Program.Devices.ContainsKey(request.device_id))
                        {
                            // Trả về thông tin chi tiết của thiết bị
                            Device_respond dv = AccessData.DV_res(vTmpUser, request.device_id);
                            return Ok(JsonConvert.SerializeObject(dv));
                        }
                        else
                        {
                            // Nếu thiết bị chưa tồn tại trong cơ sở dữ liệu
                            return Ok("Thiết bị chưa tồn tại trong cơ sở dữ liệu");
                        }
                    }
                    else
                    {
                        // Nếu thiết bị không thuộc quyền quản lý của người dùng
                        return Ok("Thiết bị không tồn tại");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/ReadDeviceDetails -> " + ex.Message);
                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("DeviceChangeAdmin")]
        public IActionResult DeviceChangeAdmin([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Kiểm tra người dùng có quyền truy cập thiết bị không
                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        // Thay đổi tên và vị trí của thiết bị
                        Program.Devices = AccessData.ChangeDeviceAdmin(request.device_id, request.newdevicename, request.latitude, request.longitude, request.area);

                        // Cập nhật danh sách khu vực cho người dùng
                        List<string> lsAreas = new List<string>();
                        foreach (var key in vTmpUser.listDevices)
                        {
                            if (!lsAreas.Contains(Program.Devices[key].area))
                                lsAreas.Add(Program.Devices[key].area);
                        }
                        vTmpUser.listAreas = lsAreas;

                        // Cập nhật thông tin thiết bị và người dùng
                        Program.Device_replace(Program.Devices[request.device_id]);
                        Program.User_replace(vTmpUser);

                        // Ghi log hoạt động
                        ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Device configuration");

                        // Trả về thông tin thiết bị sau khi cập nhật
                        return Ok(Program.Devices[request.device_id]);
                    }
                    else
                    {
                        // Nếu thiết bị không thuộc quyền quản lý của người dùng
                        return Ok("Thiết bị không tồn tại");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/DeviceChangeAdmin -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("DeviceChangeUser")]
        public IActionResult DeviceChangeUser([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Kiểm tra người dùng có quyền truy cập thiết bị không
                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        // Lấy tên thiết bị mới hoặc giữ nguyên tên hiện tại
                        var newname = request.newdevicename ?? request.device_name ?? Program.Devices[request.device_id].Device_name;

                        // Thay đổi thông tin thiết bị người dùng
                        bool res = AccessData.ChangeDeviceUser(request.device_id, newname, request.latitude, request.longitude, request.area, request.config);

                        if (res)
                        {
                            // Cập nhật danh sách khu vực cho admin
                            List<string> lsAreas_ad = new List<string>();
                            foreach (var key in Program.Users[Program.Admin_username].listDevices)
                            {
                                if (!lsAreas_ad.Contains(Program.Devices[key].area))
                                    lsAreas_ad.Add(Program.Devices[key].area);
                            }
                            Program.Users[Program.Admin_username].listAreas = lsAreas_ad;

                            // Cập nhật danh sách khu vực cho người dùng
                            List<string> lsAreas = new List<string>();
                            foreach (var key in vTmpUser.listDevices)
                            {
                                if (!lsAreas.Contains(Program.Devices[key].area))
                                    lsAreas.Add(Program.Devices[key].area);
                            }
                            vTmpUser.listAreas = lsAreas;

                            // Cập nhật thông tin thiết bị và người dùng
                            Program.Device_replace(Program.Devices[request.device_id]);
                            Program.User_replace(Program.Users[Program.Admin_username]);
                            Program.User_replace(vTmpUser);

                            // Ghi log hoạt động
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Device configuration");

                            // Trả về thông tin thiết bị sau khi cập nhật
                            return Ok(Program.Devices[request.device_id]);
                        }
                        else
                        {
                            return Ok("Cài đặt thiết bị không thành công");
                        }
                    }
                    else
                    {
                        return Ok("Thiết bị không tồn tại");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/DeviceChangeUser -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("DeviceInputPriority")]
        public IActionResult DeviceInputPriority([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Kiểm tra người dùng có quyền truy cập thiết bị không
                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        // Lấy thông tin thiết bị
                        var device = Program.Devices[request.device_id];

                        // Duyệt qua từng inputPriority trong yêu cầu
                        foreach (var input in request.inputPriority)
                        {
                            // Nếu lastData chưa chứa inputKey, khởi tạo giá trị mới
                            if (!device.lastData.ContainsKey(input.Key))
                                device.lastData[input.Key] = new LastDataPoint();

                            // Cập nhật giá trị priority cho từng input
                            device.lastData[input.Key].priority = input.Value;
                        }

                        // Trả về thông tin lastData đã cập nhật
                        return Ok(device.lastData);
                    }
                    else
                    {
                        return Ok("Thiết bị không tồn tại");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/DeviceInputPriority -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("ViewListAreas")]
        public IActionResult ViewListAreas([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Khởi tạo danh sách khu vực
                    List<string> lsAreas = new List<string>();

                    // Duyệt qua các thiết bị của người dùng để lấy danh sách khu vực
                    foreach (var key in Program.Users[vTmpUser.username].listDevices)
                    {
                        string area = Program.Devices[key].area;
                        if (!lsAreas.Contains(area))
                            lsAreas.Add(area);
                    }

                    // Cập nhật danh sách khu vực cho người dùng
                    Program.Users[vTmpUser.username].listAreas = lsAreas;

                    // Trả về danh sách khu vực
                    return Ok(lsAreas);
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/ViewListAreas -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("ReadDeviceUser")]
        public IActionResult ReadDeviceUser([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    // Nếu token không hợp lệ, trả về lỗi dữ liệu đầu vào
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    // Nếu không tìm thấy người dùng, trả về lỗi tài khoản không tồn tại
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    Read_User_respond read_user = new Read_User_respond
                    {
                        username = vTmpUser.username,
                        list_devices = new List<Device_respond>()
                    };

                    // Kiểm tra quyền của người dùng
                    if (vTmpUser.permission == "engineer")
                    {
                        // Xóa thiết bị tạm thời nếu quá thời gian quy định
                        if (vTmpUser.temporaryDevices != null && vTmpUser.temporaryDevices.Count > 0)
                        {
                            foreach (var entry in vTmpUser.temporaryDevices.ToArray())
                            {
                                if (DateTimeOffset.UtcNow > entry.Value)
                                {
                                    vTmpUser.listDevices.RemoveAll(x => x == entry.Key);
                                    ActivityLog.insertData(DateTime.UtcNow, "System", entry.Key, "Remove Temporary engineer: " + vTmpUser.username);
                                    vTmpUser.temporaryDevices.Remove(entry.Key);
                                }
                            }
                        }
                    }

                    // Lấy danh sách thiết bị của người dùng
                    foreach (string id in vTmpUser.listDevices)
                    {
                        if (Program.Devices.ContainsKey(id))
                        {
                            Device_respond dv = AccessData.DV_res(vTmpUser, id);
                            read_user.list_devices.Add(dv);
                        }
                    }

                    // Cập nhật thông tin cho đối tượng trả về
                    read_user.total_device = read_user.list_devices.Count;
                    read_user.typeList = DeviceTemplate.listTemplate.Values.Select(x => x.typecode + "," + x.typename).ToList();
                    read_user.listEngineers = Program.listEngineers;

                    // Trả về thông tin thiết bị của người dùng
                    return Ok(read_user);
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/ReadDeviceUser -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("ControlDevice")]
        public IActionResult ControlDevice([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                {
                    return Ok("Dữ liệu đầu vào không chính xác");
                }

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                {
                    return Ok("Tài khoản không tồn tại");
                }
                else
                {
                    // Kiểm tra quyền của người dùng
                    if (!DeviceTemplate.permissionCheck(vTmpUser, request.device_id, request.numInput, "WRITE"))
                    {
                        return Ok("Quyền truy cập bị từ chối");
                    }

                    if (vTmpUser.listDevices.Contains(request.device_id))
                    {
                        var config = DeviceTemplate.getConfig(request.device_id);
                        if (!string.IsNullOrEmpty(config[request.numInput].controlCmd))
                        {
                            var cmd = string.Format(config[request.numInput].controlCmd, request.value);
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Device control param " + request.numInput + " value " + request.value);

                            // Gửi lệnh điều khiển qua MQTT
                            bool res = Program.mqttHandle.sendCommand(request.device_id, cmd);

                            return res ? Ok("Lệnh đã được gửi thành công") : Ok("Gửi lệnh thất bại");
                        }
                        else
                        {
                            return Ok("Lệnh không hợp lệ");
                        }
                    }
                    else
                    {
                        return Ok("Thiết bị không tồn tại");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/ControlDevice -> " + ex.Message);

                // Trả về mã lỗi với chi tiết ngoại lệ
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("CreateDevice")]
        public IActionResult CreateDevice([FromBody] Class.HttpRequest request)
        {
            string adm = Program.Admin_username;
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                bool isNew = false;
                // Kiểm tra xem thiết bị đã tồn tại chưa
                if (!Program.Devices.ContainsKey(request.device_id))
                {
                    isNew = true;
                    Device newdv = new Device(request.device_id, false, request.device_name, request.latitude, request.longitude, 0, 0,
                        new Dictionary<string, LastDataPoint>(), 0, request.area, 0);
                    Program.Devices.Add(newdv.Device_id, newdv);
                }
                var device = Program.Devices[request.device_id];

                // Kiểm tra quyền sở hữu thiết bị
                if (!isNew && Program.DeviceOwnership.ContainsKey(request.device_id) && Program.DeviceOwnership[request.device_id] != vTmpUser.username)
                {
                    return Ok("success"); // không có thay đổi, chỉ qua để cấu hình wifi
                }

                // Cập nhật thông tin thiết bị
                device.Device_name = request.device_name;
                device.latitude = request.latitude;
                device.longitude = request.longitude;
                if (request.camId != null) device.cameraId = request.camId;
                DeviceTemplate.setType(device, request.device_type);
                device.area = request.area ?? "";

                if (request.area != null && !vTmpUser.listAreas.Contains(request.area))
                    vTmpUser.listAreas.Add(request.area);

                Program.DeviceOwnership[request.device_id] = vTmpUser.username;

                // Thêm thiết bị vào danh sách thiết bị của người dùng nếu chưa có
                if (!vTmpUser.listDevices.Contains(request.device_id))
                {
                    vTmpUser.listDevices.Add(request.device_id);
                    Program.Devices[request.device_id].addNotiList(request.username);
                }

                // Cập nhật chủ sở hữu và trạng thái của thiết bị
                device.owner = vTmpUser.username;
                device.Status = false;

                // Lưu thiết bị vào cơ sở dữ liệu
                if (isNew)
                    Program.Device_Collection.InsertOne(device);
                else
                    Program.Device_replace(device);

                Program.User_replace(vTmpUser);

                // Ghi lại log hoạt động
                ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Device creation from mobile app");
                return Ok("success");
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/CreateDevice -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("FactoryReset")]
        public IActionResult FactoryReset([FromBody] Class.HttpRequest request)
        {
            string adm = Program.Admin_username;
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                var deviceid = request.device_id;

                // Kiểm tra xem thiết bị có tồn tại không
                if (!Program.Devices.ContainsKey(deviceid))
                    return Ok("Thiết bị không tìm thấy");

                // Kiểm tra quyền sở hữu thiết bị
                if (Program.DeviceOwnership.ContainsKey(deviceid) &&
                    Program.DeviceOwnership[deviceid] != vTmpUser.username &&
                    vTmpUser.permission != "admin")
                {
                    return Ok("Quyền bị từ chối");
                }

                // Thực hiện reset thiết bị
                if (Program.DeviceOwnership.Remove(deviceid, out string ownerUsername))
                {
                    var owner = vTmpUser.permission == "admin" ? Program.Users[ownerUsername] : vTmpUser;
                    owner.listDevices.Remove(deviceid);
                    Program.Devices[deviceid].removeNotiList(owner.username);
                    Program.Devices[deviceid].owner = "";

                    // Ghi lại log hoạt động
                    ActivityLog.insertData(DateTime.UtcNow, owner.username, request.device_id, "Device factory reset from mobile app");
                    Program.Device_replace(Program.Devices[request.device_id]);
                    return Ok("success");
                }
                else
                {
                    return Ok("failed");
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/FactoryReset -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("UpdateDevice")]
        public IActionResult UpdateDevice([FromBody] Class.HttpRequest request)
        {
            string adm = Program.Admin_username;
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Kiểm tra xem người dùng có quyền cập nhật thiết bị không
                if (vTmpUser.listDevices.Contains(request.device_id) && Program.Devices.ContainsKey(request.device_id))
                {
                    Device olddv = Program.Devices[request.device_id];
                    olddv.Device_name = request.device_name;

                    if (request.area != null)
                        olddv.area = request.area;

                    olddv.latitude = request.latitude;
                    olddv.longitude = request.longitude;

                    if (request.camId != null)
                        olddv.cameraId = request.camId;

                    if (olddv.type != request.device_type)
                    {
                        DeviceTemplate.setType(olddv, request.device_type);
                        Program.mqttHandle.sendCommand(request.device_id, "c:type:" + request.device_type);
                    }

                    if (!string.IsNullOrEmpty(request.wifissid))
                    {
                        Program.mqttHandle.sendCommand(request.device_id, "wifi:" +
                            request.wifissid.Trim() + "," + request.wifipass.Trim());
                        olddv.Status = false;
                    }

                    // Cập nhật thiết bị trong cơ sở dữ liệu
                    var filter = Builders<Device>.Filter.Eq(x => x.Device_id, request.device_id);
                    Program.Device_Collection.ReplaceOne(filter, olddv);

                    // Ghi lại log hoạt động
                    ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Device update from mobile app");

                    var tmp = AccessData.DV_res(vTmpUser, request.device_id);
                    return Ok(tmp); // Trả về thông tin thiết bị đã cập nhật
                }
                else
                {
                    return Ok("Thiết bị không tìm thấy");
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/UpdateDevice -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("CheckDeviceStatus")]
        public IActionResult CheckDeviceStatus([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Kiểm tra xem thiết bị có tồn tại không
                if (!Program.Devices.ContainsKey(request.device_id) || !vTmpUser.listDevices.Contains(request.device_id))
                    return Ok("Thiết bị không tồn tại");

                // Trả về trạng thái thiết bị
                if (request.value == 0)
                    return Ok(Program.Devices[request.device_id].Status ? "online" : "offline");
                else if (request.value == 1)
                    return Ok(JsonConvert.SerializeObject(Program.Devices[request.device_id].lastData));
                else
                    return Ok("Yêu cầu không xác định");
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/CheckDeviceStatus -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("ReadTemplate")]
        public IActionResult ReadTemplate([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Kiểm tra quyền truy cập
                if (vTmpUser.permission == "admin")
                    return Ok(JsonConvert.SerializeObject(DeviceTemplate.listTemplate));
                else
                    return Ok("Quyền truy cập bị từ chối");
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/ReadTemplate -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("UpdateTemplate")]
        public IActionResult UpdateTemplate([FromBody] CustomRequest<DeviceTemplate> request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Kiểm tra quyền truy cập
                if (vTmpUser.permission == "admin")
                {
                    ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, "N/A", $"Cập nhật hoặc tạo mẫu {request.data.typename}");

                    // Cập nhật mẫu và trả về kết quả
                    if (DeviceTemplate.updateTemplate(request.data))
                        return Ok("Thành công");
                    else
                        return Ok("Thất bại");
                }
                else
                {
                    return Ok("Quyền truy cập bị từ chối");
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/UpdateTemplate -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("DeleteTemplate")]
        public IActionResult DeleteTemplate([FromBody] CustomRequest<int> request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Kiểm tra quyền truy cập
                if (vTmpUser.permission == "admin")
                {
                    ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, "N/A", $"Xóa mẫu {request.data}");

                    // Xóa mẫu và trả về kết quả
                    if (DeviceTemplate.deleteTemplate(request.data))
                        return Ok("Thành công");
                    else
                        return Ok("Thất bại");
                }
                else
                {
                    return Ok("Quyền truy cập bị từ chối");
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/DeleteTemplate -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("QueryLog")]
        public IActionResult QueryLog([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                // Tạo khoảng thời gian truy vấn nhật ký
                DateTime fromDate = new DateTime(request.year, request.month, request.day, 0, 0, 0);
                DateTime toDate = new DateTime(request.year, request.month, request.day, 23, 59, 59);
                if (request.hour != 0)
                {
                    fromDate = fromDate.AddHours(request.hour);
                    toDate = toDate.AddHours(request.hour);
                }

                // Tìm kiếm dữ liệu nhật ký theo khoảng thời gian
                var res = ActivityLog.searchData(fromDate, toDate, vTmpUser);
                return Ok(JsonConvert.SerializeObject(res)); // Trả về kết quả dưới dạng JSON
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/QueryLog -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
        [HttpPost("AddEngineer")]
        public IActionResult AddEngineer([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Khởi tạo TokenKey từ tokenkey trong request
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Dữ liệu đầu vào không chính xác");

                // Tìm kiếm người dùng dựa trên username và password từ TokenKey
                User vTmpUser = Program.Users.Values
                                        .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);
                if (vTmpUser == null)
                    return Ok("Tài khoản không tồn tại");

                string msg = "Thêm kỹ sư thất bại";

                // Kiểm tra thiết bị
                if (Program.Devices.ContainsKey(request.device_id) && vTmpUser.listDevices.Contains(request.device_id))
                {
                    var device = Program.Devices[request.device_id];

                    if (vTmpUser.permission == "user")
                    {
                        // Xóa kỹ sư hỗ trợ trước đó nếu có
                        if (!string.IsNullOrEmpty(device.supportEngineer) && Program.Users.ContainsKey(device.supportEngineer))
                        {
                            Program.Users[device.supportEngineer].listDevices.Remove(device.Device_id);
                            Program.User_replace(Program.Users[device.supportEngineer]);
                        }

                        // Thêm kỹ sư mới
                        if (Program.Users.ContainsKey(request.username) && Program.Users[request.username].permission == "engineer")
                        {
                            device.supportEngineer = request.username;
                            Program.Users[request.username].listDevices.Add(device.Device_id);
                            Program.User_replace(Program.Users[request.username]);
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Thêm Kỹ sư hỗ trợ: " + request.username);
                            msg = "Thành công";
                        }
                        else if (string.IsNullOrEmpty(request.username))
                        {
                            device.supportEngineer = "";
                            ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Xóa Kỹ sư hỗ trợ");
                            msg = "Thành công";
                        }

                        Program.Device_replace(device);
                    }
                    else if (vTmpUser.permission == "engineer" && device.supportEngineer == vTmpUser.username && request.username != vTmpUser.username)
                    {
                        Program.Users[request.username].listDevices.Add(request.device_id);
                        Program.Users[request.username].temporaryDevices[request.device_id] = DateTimeOffset.UtcNow.AddHours(request.value);
                        Program.User_replace(Program.Users[request.username]);
                        msg = "Thành công";
                        ActivityLog.insertData(DateTime.UtcNow, vTmpUser.username, request.device_id, "Thêm Kỹ sư tạm thời: " + request.username);
                    }
                }

                return Ok(msg); // Trả về thông báo
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi xảy ra
                Program.saveLog("API: api/DataAPI/AddEngineer -> " + ex.Message);
                return StatusCode(417, ex.Message);
            }
        }
    }
}
