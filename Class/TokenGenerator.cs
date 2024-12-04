using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DataLogger_NetCore.Class
{
    public class TokenGenerator
    {
        public static string tockenkey_admin;
        public static string tocketkey_user;

        // Khóa bí mật JWT, bạn cần thay đổi giá trị này để bảo mật
        private const string SecretKey = "your-secret-key-change-this";
        private const string Issuer = "your-app-name"; // Tên ứng dụng phát hành
        private const string Audience = "your-app-users"; // Đối tượng sử dụng

        /// <summary>
        /// Tạo JWT cho admin
        /// </summary>
        public static string GenerateAdminToken(int adminId, string username)
        {
            return GenerateToken(adminId.ToString(), username, "Admin");
        }

        /// <summary>
        /// Tạo JWT cho người dùng
        /// </summary>
        public static string GenerateUserToken(string userId, string username)
        {
            return GenerateToken(userId, username, "User");
        }

        /// <summary>
        /// Phương thức chính để tạo JWT
        /// </summary>
        private static string GenerateToken(string userId, string username, string role)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId), // ID của user
                new Claim(JwtRegisteredClaimNames.UniqueName, username), // Tên đăng nhập
                new Claim(ClaimTypes.Role, role), // Vai trò (Admin/User)
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // ID ngẫu nhiên cho token
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1), // Hạn sử dụng của token
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Giải mã và kiểm tra JWT
        /// </summary>
        public static ClaimsPrincipal? ValidateToken(string token)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = key
                }, out SecurityToken validatedToken);

                return claimsPrincipal;
            }
            catch
            {
                // Token không hợp lệ
                return null;
            }
        }

        /* Bạn có thể sử dụng phương thức `ToKeyUsers` trong trường hợp cần sử dụng token cho người dùng */
        public static string ToKeyUsers(string id)
        {
            // Sử dụng mã hóa JWT thay vì TripleDES
            return GenerateUserToken(id, Program.Users[id].username);
        }
    }
}

/*using System;
using System.Security.Cryptography;
using System.Text;

namespace DataLogger_NetCore.Class
{
    public class TokenGenerator
    {
        public static string tockenkey_admin;
        public static string tocketkey_user;
        public static string Encrypt(string toEncrypt, bool useHashing)
        {
            if (toEncrypt == null)
                toEncrypt = "";
            byte[] keyArray;
            byte[] toEncryptArray = Encoding.UTF8.GetBytes(toEncrypt);
            if (useHashing)
            {
                var hashmd5 = new MD5CryptoServiceProvider();
                keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes("aithings.vn"));
            }
            else keyArray = Encoding.UTF8.GetBytes("aithings.vn");
            var tdes = new TripleDESCryptoServiceProvider
            {
                Key = keyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            ICryptoTransform cTransform = tdes.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Convert.ToBase64String(resultArray, 0, resultArray.Length);
        }
        public static string Decrypt(string toDecrypt, bool useHashing)
        {
            if (toDecrypt == null)
                toDecrypt = "";
            byte[] keyArray;
            byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);
            if (useHashing)
            {
                var hashmd5 = new MD5CryptoServiceProvider();
                keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes("aithings.vn"));
            }
            else keyArray = Encoding.UTF8.GetBytes("aithings.vn");
            var tdes = new TripleDESCryptoServiceProvider
            {
                Key = keyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            ICryptoTransform cTransform = tdes.CreateDecryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Encoding.UTF8.GetString(resultArray);
        }
        /*  public static string ToKeyAdmin(int id)
          {
              string vKey = "";
              vKey += Program.AdminLogin[id].username.Length.ToString().PadLeft(2, '0');
              vKey += Program.AdminLogin[id].password.Length.ToString().PadLeft(3, '0');
              string vData = string.Format("{0}{1}{2}{3}",
                  vKey,
                  Program.AdminLogin[id].username,
                  Program.AdminLogin[id].password,
                  Program.getTime().ToString("yyyyMMddHHmmss"));
              string tockenkey_admin = Encrypt(vData, true);
              return tockenkey_admin;
          } cut*/

/*      public static string ToKeyUsers(string id)
      {
          string vKey = "";
          vKey += Program.Users[id].username.Length.ToString().PadLeft(2, '0');
          vKey += Program.Users[id].password.Length.ToString().PadLeft(3, '0');
          string vData = string.Format("{0}{1}{2}{3}",
              vKey,
              Program.Users[id].username,
              Program.Users[id].password,
              Program.getTime().ToString("yyyyMMddHHmmss"));
          string tocketkey_user = Encrypt(vData, true);
          return tocketkey_user;
      }
  }
} */

