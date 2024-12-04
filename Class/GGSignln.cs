using Google.Apis.Auth;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLogger_NetCore.Class
{
    public static class GGSignIn
    {
        static string appIdGG = "794275052549-nbk4rj7m65rmfvgnpuhsijmir8hm5d6b.apps.googleusercontent.com";
        static GoogleJsonWebSignature.ValidationSettings GGValidationSettings = new GoogleJsonWebSignature.ValidationSettings()
        {
            Audience = new List<string>() { appIdGG }
        };
        public static string verifyToken(string tokenId)
        {
            try
            {
                Program.saveLog("GG signing in " + tokenId);
                GoogleJsonWebSignature.Payload payload = GoogleJsonWebSignature.ValidateAsync(tokenId, GGValidationSettings).Result;
                var email = payload.Email;
                if (email != null)
                {
                    Program.saveLog("GG token check ok " + email);
                    return email;
                }
                else return "";
            }
            catch (Exception e)
            {
                Program.saveLog(e.ToString());
                return "";
            }
        }

        static string appIdMS = "62b29b8b-1dda-4e0e-9fdf-d52ec63137e0";
        public static string verifyMSToken(string tokenId)
        {

            try
            {
                Program.saveLog("MS signing in " + tokenId);
                var token = new JwtSecurityToken(jwtEncodedString: tokenId);
                if (token.Payload.Aud.First() != appIdMS) return "";
                var now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (token.Payload.NotBefore > now || token.Payload.Expiration < now) return "";
                object email = "";
                token.Payload.TryGetValue("preferred_username", out email);
                if (email != null)
                {
                    Program.saveLog("MS token check ok " + email);
                    return (string)email;
                }
                else return "";
            }
            catch (Exception e)
            {
                Program.saveLog(e.ToString());
                return "";
            }
        }

        public static string verifyAppleToken(string tokenId)
        {

            try
            {
                Program.saveLog("Apple signing in " + tokenId);
                var token = new JwtSecurityToken(jwtEncodedString: tokenId);
                if (token.Payload.Aud.First() != "com.aithings.white") return "";
                if (!token.Payload.Iss.Contains("https://appleid.apple.com")) return "";
                var now = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (token.Payload.NotBefore > now || token.Payload.Expiration < now) return "";
                object email = "";
                token.Payload.TryGetValue("email", out email);
                if (email != null)
                {
                    Program.saveLog("Apple token check ok " + email);
                    return (string)email;
                }
                else return "";
            }
            catch (Exception e)
            {
                Program.saveLog(e.ToString());
                return "";
            }
        }
    }
}
