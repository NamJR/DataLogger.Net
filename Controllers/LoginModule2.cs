using DataLogger_NetCore.Class;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;

namespace DataLogger_NetCore.API
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    public class LoginAPIController : ControllerBase
    {
        // POST: api/LoginAPI/UsersLogin
        [HttpPost("UsersLogin")]
        public IActionResult UsersLogin([FromBody] Class.TokenKey request)
        {
            try
            {
                // Kiểm tra xem user có tồn tại không
                if (Program.Users.ContainsKey(request.UserName))
                {
                    var user = Program.Users[request.UserName];

                    // Kiểm tra mật khẩu
                    if (!string.IsNullOrEmpty(user.password) && user.password == request.Password)
                    {
                        // Xóa các khu vực null
                        user.listAreas.RemoveAll(x => x == null);

                        // Tạo token key
                        string tokenkey = TokenGenerator.ToKeyUsers(request.UserName);
                        user.tokenkey = tokenkey;
                        user.lastLogin = Program.getTime().ToOADate();

                        // Cập nhật user
                        Program.User_replace(user);
                    
                        var response = new
                        {
                            user = new
                            {
                                user.username,
                                user.password, // Thêm các trường cần thiết
                            },
                            tokenkey = tokenkey,
                            tokenDetails = new TokenKey(tokenkey)
                        };

                        // Chuyển đổi đối tượng thành chuỗi JSON
                        string jsonResponse = Newtonsoft.Json.JsonConvert.SerializeObject(response);

                        // Mã hóa JSON bằng Base64
                        string encodedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonResponse));

                        // Trả về chuỗi JSON đã mã hóa
                        return Ok(encodedResponse);
                    }
                    else
                    {
                        return Ok(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"message\": \"Incorrect password\"}")));
                    }
                }
                else
                {
                    return Ok(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"message\": \"Account not exist\"}")));
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Program.saveLog($"API: /api/LoginAPI/UsersLogin -> {ex.Message}");

                // Trả về lỗi dưới dạng JSON đã mã hóa
                string errorJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { message = ex.Message });
                return Ok(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(errorJson)));
            }
        }


        // POST: api/LoginAPI/TokenLogin
        [HttpPost("TokenLogin")]
        public IActionResult TokenLogin([FromBody] Class.HttpRequest request)
        {
            try
            {
                // Kiểm tra token key
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                // Xác thực người dùng
                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");
                else
                {
                    // Cập nhật thông tin đăng nhập cuối
                    vTmpUser.lastLogin = Program.getTime().ToOADate();
                    Program.User_replace(vTmpUser);

                    // Trả về thông tin người dùng đăng nhập thành công
                    return Ok(vTmpUser);
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: api/LoginAPI/TokenLogin -> " + ex.Message);
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }
        [HttpPost("UsersLoginGG")]
        public IActionResult UsersLoginGG([FromBody] Class.HttpRequest request)
        {
            string tokenkey;
            try
            {
                // Xác thực token từ Google
                string username = GGSignIn.verifyToken(request.username);
                if (string.IsNullOrEmpty(username))
                    return Ok("Đăng nhập không thành công");

                // Kiểm tra xem người dùng đã tồn tại chưa
                if (Program.Users.ContainsKey(username))
                {
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    Program.Users[username].tokenkey = tokenkey;
                    Program.Users[username].lastLogin = Program.getTime().ToOADate();
                    Program.User_replace(Program.Users[username]);
                    return Ok(Program.Users[username]); // Đăng nhập thành công
                }
                else
                {
                    // Nếu người dùng mới, khởi tạo thông tin
                    List<string> lsDevices = new List<string>();
                    List<string> lsAreas = new List<string>();
                    double timenow = Program.getTime().ToOADate();
                    var newUser = new User(username, "", "", timenow, -1, "user", lsDevices, lsAreas);
                    Program.Users.Add(username, newUser);
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    newUser.tokenkey = tokenkey;
                    Program.User_Collection.InsertOne(newUser);
                    return Ok(newUser);
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi
                Program.saveLog("API: /api/LoginAPI/UsersLogin -> " + ex.Message);
                return Ok(ex.Message); // Trả về thông báo lỗi
            }
        }
        [HttpPost("UsersLoginMS")]
        public IActionResult UsersLoginMS([FromBody] Class.HttpRequest request)
        {
            string tokenkey;
            try
            {
                // Xác thực token từ Microsoft
                string username = GGSignIn.verifyMSToken(request.username);
                if (string.IsNullOrEmpty(username))
                    return Ok("Đăng nhập không thành công");

                // Kiểm tra xem người dùng đã tồn tại chưa
                if (Program.Users.ContainsKey(username))
                {
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    Program.Users[username].tokenkey = tokenkey;
                    Program.Users[username].lastLogin = Program.getTime().ToOADate();
                    Program.User_replace(Program.Users[username]);
                    return Ok(Program.Users[username]); // Đăng nhập thành công
                }
                else
                {
                    // Nếu người dùng mới, khởi tạo thông tin
                    List<string> lsDevices = new List<string>();
                    List<string> lsAreas = new List<string>();
                    double timenow = Program.getTime().ToOADate();
                    var newUser = new User(username, "", "", timenow, -1, "user", lsDevices, lsAreas);
                    Program.Users.Add(username, newUser);
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    newUser.tokenkey = tokenkey;
                    Program.User_Collection.InsertOne(newUser);
                    return Ok(newUser);
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi
                Program.saveLog("API: /api/LoginAPI/UsersLogin -> " + ex.Message);
                return Ok(ex.Message); // Trả về thông báo lỗi
            }
        }
        [HttpPost("UsersLoginApple")]
        public IActionResult UsersLoginApple([FromBody] Class.HttpRequest request)
        {
            string tokenkey;
            try
            {
                // Xác thực token từ Apple
                string username = GGSignIn.verifyAppleToken(request.username);
                if (string.IsNullOrEmpty(username))
                    return Ok("Đăng nhập không thành công");

                // Kiểm tra xem người dùng đã tồn tại chưa
                if (Program.Users.ContainsKey(username))
                {
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    Program.Users[username].tokenkey = tokenkey;
                    Program.Users[username].lastLogin = Program.getTime().ToOADate();
                    Program.User_replace(Program.Users[username]);
                    return Ok(Program.Users[username]); // Đăng nhập thành công
                }
                else
                {
                    // Nếu người dùng mới, khởi tạo thông tin
                    List<string> lsDevices = new List<string>();
                    List<string> lsAreas = new List<string>();
                    double timenow = Program.getTime().ToOADate();
                    var newUser = new User(username, "", "", timenow, -1, "user", lsDevices, lsAreas);
                    Program.Users.Add(username, newUser);
                    tokenkey = TokenGenerator.ToKeyUsers(username);
                    newUser.tokenkey = tokenkey;
                    Program.User_Collection.InsertOne(newUser);
                    return Ok(newUser);
                }
            }
            catch (Exception ex)
            {
                // Ghi lại log nếu có lỗi
                Program.saveLog("API: /api/LoginAPI/UsersLoginApple -> " + ex.Message);
                return Ok(ex.Message); // Trả về thông báo lỗi
            }
        }
        [HttpPost("Users")]
        public IActionResult GetAllUsers([FromBody] string tokenkey)
        {
            tokenkey = tokenkey.Trim('"'); // Xóa dấu nháy kép nếu có
            try
            {
                TokenKey vKey = new TokenKey(tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                Dictionary<string, object> usload = new Dictionary<string, object>(); // Users Load file

                if (vTmpUser.permission == "admin")
                {
                    // Lấy danh sách người dùng có quyền engineer
                    foreach (var user in Program.Users.Values)
                    {
                        if (user.permission == "engineer")
                            usload[user.username] = user;
                    }
                    return Ok(usload);
                }
                else if (vTmpUser.permission == "engineer")
                {
                    // Lấy danh sách người dùng theo danh sách của engineer
                    foreach (var username in vTmpUser.listUsers)
                    {
                        if (Program.Users.ContainsKey(username))
                            usload[username] = Program.Users[username];
                    }
                    return Ok(usload);
                }
                else
                {
                    return Ok("Permission denied");
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Program.saveLog("API: /api/LoginAPI/Users -> " + ex.Message);
                return Ok(ex.Message); // Trả về thông báo lỗi
            }
        }
        [HttpPost("AddUsers")]
        public IActionResult AddUsers([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                if (vTmpUser.permission == "admin")
                {
                    if (Program.Users.ContainsKey(request.username))
                    {
                        return Ok("Username đã tồn tại!");
                    }
                    else
                    {
                        List<string> lsDevices = new List<string>();
                        List<string> lsAreas = new List<string>();
                        double timenow = Program.getTime().ToOADate();

                        Program.Users.Add(request.username, new User(request.username, request.password, "", timenow, -1, request.permission, lsDevices, lsAreas));
                        Program.User_Collection.InsertOne(Program.Users[request.username]);

                        // Ghi log hành động tạo tài khoản
                        Program.actionLog($"{vTmpUser.username} create a new account: username={request.username}, password={request.password}");

                        if (request.permission == "engineer")
                            Program.listEngineers.Add(request.username);

                        return Ok("Thêm 1 user thành công");
                    }
                }
                else
                {
                    return Ok("Permission denied");
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Program.saveLog("API: /api/LoginAPI/AddUsers -> " + ex.Message);
                return Ok(ex.Message);
            }
        }
        [HttpPost("RootConfig")]
        public IActionResult RootConfig([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                if (vTmpUser.permission == "root")
                {
                    if (Program.Users.ContainsKey(request.username))
                    {
                        return Ok("Username đã tồn tại!");
                    }
                    else
                    {
                        List<string> lsDevices = new List<string>();
                        List<string> lsAreas = new List<string>();
                        double timenow = Program.getTime().ToOADate();
                        string permission = request.permission == "null" ? "user" : request.permission;

                        Program.Users.Add(request.username, new User(request.username, request.password, "", timenow, -1, permission, lsDevices, lsAreas));
                        Program.actionLog($"[RootConfig] CREATE username={request.username}, password={request.password} permission={request.permission}");
                        Program.User_Collection.InsertOne(Program.Users[request.username]);

                        return Ok("Thêm thành công");
                    }
                }
                else
                {
                    return Ok("Permission denied");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: /api/LoginAPI/RootConfig -> " + ex.Message);
                return Ok(ex.Message);
            }
        }
        [HttpPost("DeleteUsers")]
        public IActionResult DeleteUsers([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                if (vTmpUser.permission == "admin")
                {
                    if (Program.Users.ContainsKey(request.username) &&
                        Program.Users[request.username].permission != "root" &&
                        Program.Users[request.username].permission != "admin")
                    {
                        if (Program.Users[request.username].permission == "engineer")
                        {
                            Program.listEngineers.Remove(request.username);
                        }
                        Program.Users.Remove(request.username);
                        Program.actionLog($"[DELETE USER] {vTmpUser.username} remove an account: username={request.username}");
                        Program.User_delete(request.username);
                        return Ok("Xóa username thành công!");
                    }
                    else
                    {
                        return Ok("Username không tồn tại!");
                    }
                }
                else if (vTmpUser.permission == "root")
                {
                    if (Program.Users.ContainsKey(request.username) && Program.Users[request.username].permission != "root")
                    {
                        Program.Users.Remove(request.username);
                        Program.actionLog($"[ROOT DELETE USER] {vTmpUser.username} remove an account: username={request.username}");
                        Program.User_delete(request.username);
                        return Ok("Xóa username thành công!");
                    }
                    else
                    {
                        return Ok("Username không tồn tại!");
                    }
                }
                else
                {
                    return Ok("Permission denied");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: /api/LoginAPI/DeleteUsers -> " + ex.Message);
                return Ok(ex.Message);
            }
        }
        [HttpPost("UserChangePassword")]
        public IActionResult UserChangePassword([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                if (request.password != vTmpUser.password)
                    return Ok("Mật khẩu cũ sai!");
                else
                {
                    Program.Users[vTmpUser.username].password = request.newpassword;
                    Program.actionLog($"[USER_CHANGE_PASSWORD] {vTmpUser.username} change a password to {request.newpassword}");
                    Program.User_replace(Program.Users[vTmpUser.username]);
                    return Ok(Program.Users[vTmpUser.username]);
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: /api/LoginAPI/UserChangePassword -> " + ex.Message);
                return Ok(ex.Message);
            }
        }
        [HttpPost("AdminResetPassword")]
        public IActionResult AdminResetPassword([FromBody] Class.HttpRequest request)
        {
            try
            {
                TokenKey vKey = new TokenKey(request.tokenkey);
                if (!vKey.isOK)
                    return Ok("Incorrect input data");

                User vTmpUser = Program.Users.Values
                    .FirstOrDefault(p => p.username == vKey.UserName && p.password == vKey.Password);

                if (vTmpUser == null)
                    return Ok("Account not exist");

                if (vTmpUser.permission == "admin")
                {
                    if (Program.Users.ContainsKey(request.username))
                    {
                        Program.Users[request.username].password = request.newpassword;
                        Program.actionLog($"[ADMIN_RESET_PASSWORD] reset password: username={request.username}, newpassword={request.newpassword}");
                        Program.User_replace(Program.Users[request.username]);
                        return Ok("Đổi mật khẩu user thành công");
                    }
                    else
                    {
                        return Ok("Tài khoản username không tồn tại");
                    }
                }
                else
                {
                    return Ok("Bạn không có quyền thực hiện điều này");
                }
            }
            catch (Exception ex)
            {
                Program.saveLog("API: /api/LoginAPI/AdminResetPassword -> " + ex.Message);
                return Ok(ex.Message);
            }
        }
    }
}

