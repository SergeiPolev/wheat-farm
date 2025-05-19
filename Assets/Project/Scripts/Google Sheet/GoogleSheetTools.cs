using System;
using Cathei.BakingSheet.Internal;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Cathei.BakingSheet.Unity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cathei.BakingSheet.Examples
{
    public static class GoogleSheetTools
    {
        // unit test google account credential
        private static readonly string GoogleCredential = @"{
  ""type"": ""service_account"",
  ""project_id"": ""every-day-games"",
  ""private_key_id"": ""21ac13f189b07776df746503091e2b76ffd371d8"",
  ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQCv+9RKxHjouGZ5\nPZf5iwA5EkrCjnSdU7a6XC18dkLaTNlOePNPkMrMe6tcHk1M83LUyV94IoXpEnqb\ndfI8KYHq2P5DCRRyGFxJhzaeIF3tX/HAcfwbXx8dDQQwS2mc7+auIShh8lclVotc\nu+VkXssDkjpD+WanZu6rrTa4004RThODnrJwt94+D/+mIhWljO4Nntx1oABiyP5X\ns2CWHmpHf/gfZoQErf9M0k+Rk++X5GB5MGfvCckBU6T8sQS/dYYRNtFpGXD9yqOj\nv6Cww3d4QqqlxP8VVOS5TROynsB3tD6MMk2NZJV4YYlp11TyPHDG7nV4aBJwasPb\nA8ZB+bmZAgMBAAECggEAFUqZh6Y3nqhYtYhvL6DSmRUOeHV1xGcRb4ChfDJuqiys\nCeN2RIUXFCBOPinoXkwB856db1J18xnqPY7KjN7Uug4gzQl3MMqjt72lLQJresGl\n5QulXcZnZENj41fsYjFgLmcSlK8WPg03dTPCcB9L3pW9eOXetpgsfhbJzRi7lDoz\nkHgxZQhvX2sF/tgKpP/mvU/6YeJJ/hf/z23bUqGbBdpQ5fEWxVZ5GjmVo80f8Mpe\n+ZGcYb5Fcvqm0UbaLMF/1us5W9IHVuYYaxo8iDPALrwOad05bGuE5XzAs/N+35ef\nHfRJilBZs3o3OszhfW5e5+luqDGIT2I89yy/fRXAkQKBgQDgKMZu3HNQeP/Jyoww\nNS/IdJpQVUs2c78USWGpV+708Li4nKqNH/iSA2EHeJCdMqlpk0m98bIaY3NPy38h\nYjPaj0p5DrfGDD91lCaCGcQ746nE6R3S/mN1w0VQGQeZlj+xqdwLn05TTMlVJQvr\nTwnVuVAyNCgFm2Hm6x9UvJTrlQKBgQDI+zhJ3iUX9f823Oh4Lg/cfnzCkNxEYl3U\ngclgswAa6/7rCm9blNLsLsiviAWO1qzYvo6exlZiaZGf8BVllqwp/iCj7zXAvUH5\n4SYgan88iIvyGvEAlC84PDNHud7drytNMkwzUS2JXiAsGYbsa5KM5w6iRASgaqpA\nj11mUrI09QKBgHWaDtk0wS7z/EaTBE96Z/JD8n248ffEa/gps5oTryNEc7UvRG87\n2b5JFYvE3iIK5USlaGfFuQoNKP8xJSaPjeLZkFnItfOqk1SNgFJ7UC+Xdob/Qo1i\nty2eX+vw5cLXR91e3zodvwsG2w3XnNQ8KE2/pmpgYKroZUmwC0T6lyqtAoGBAIQQ\nT9R6HHW6N5GdZ0RRQCrrEp/nAFYPLQjOn7zi4lbObBuWJ8ZN7Ks1srlk8AIEHl/u\nF1lNisXwCLjH0ceHUmnlix0tumyD8C56O8thL2pfb1YPTf3LYZvaMvgWstOInzOC\nsX+m//0b1JglzfrcVNgxm/QULdYbQPRbQExUWrVhAoGBAIH9zzsUIYC2FhUwP+RF\ntcwwrGHvZe+XnJmd8zwjHiOOO3Og5B/PZfus4RqfX3bYQTijazCa8HB3IqB/uplb\nEqcQexVHIy6B75aT7Wm4CvjBSDUjyh48pqUybdbqJcX4XaTfuwMsoRXIy+inMULT\nSi1h5CGvtEAcD0waIPJvFCvc\n-----END PRIVATE KEY-----\n"",
  ""client_email"": ""bakingsheet@every-day-games.iam.gserviceaccount.com"",
  ""client_id"": ""111270217976249178854"",
  ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
  ""token_uri"": ""https://oauth2.googleapis.com/token"",
  ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
  ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/bakingsheet%40every-day-games.iam.gserviceaccount.com"",
  ""universe_domain"": ""googleapis.com""
}";

        public class PrettyJsonConverter : JsonSheetConverter
        {
            public PrettyJsonConverter(string path, IFileSystem fileSystem = null) : base(path, fileSystem)
            { }

            public override JsonSerializerSettings GetSettings(Microsoft.Extensions.Logging.ILogger logError)
            {
                var settings = base.GetSettings(logError);

                settings.Formatting = Formatting.Indented;

                return settings;
            }
        }

#if UNITY_EDITOR
        [MenuItem("BakingSheet/Sample/Import From Google")]
#endif
        public static async Task<bool> ConvertFromGoogle(string gsheetAdress = "179ICLmwcLPiO1oCaPsEfPY0gSj4t9I6_xnacJ_1Su-E")
        {
            var jsonPath = PathConstants.JSONFilesPath;

            var googleConverter = new GoogleSheetConverter(gsheetAdress, GoogleCredential, TimeZoneInfo.Utc);

            var sheetContainer = new SheetContainer(UnityLogger.Default);

            await sheetContainer.Bake(googleConverter);

            // create json converter to path
            var jsonConverter = new PrettyJsonConverter(jsonPath);

            // save datasheet to streaming assets
            bool success = await sheetContainer.Store(jsonConverter);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            return success;
        }

        public static async Task<bool> ConvertFromGoogleBake(string address = "1twAv_USCJ4oxdMJ2j5yvQnR3xlJrG0GsfkWPGJoLICg")
        {
            var jsonPath = PathConstants.JSONFilesBakedPath;

            // SHEET ID. Here we'll change ID to change config for game
            var googleConverter = new GoogleSheetConverter(address, GoogleCredential, TimeZoneInfo.Utc);

            // Use as Unity Console logger
            //var logger = UnityLogger.Default;

            var sheetContainer = new SheetContainer(UnityLogger.Default);

            await sheetContainer.Bake(googleConverter);

            // create json converter to path
            var jsonConverter = new PrettyJsonConverter(jsonPath);

            // save datasheet to streaming assets

            var success = await sheetContainer.Store(jsonConverter);

            return success;
        }

        public static async Task<bool> ConvertFromGoogleMasterConfig()
        {
            var jsonPath = PathConstants.JSONMasterFilesPath;
            var googleConverter = new GoogleSheetConverter("1rQC8zl2GLteXTT1s7kNLVRV-oF_oTQJ40tnYpuIiN4s", GoogleCredential, TimeZoneInfo.Utc);
            var logger = UnityLogger.Default;
            var sheetContainer = new MasterConfigSheetContainer(logger);
            await sheetContainer.Bake(googleConverter);
            var jsonConverter = new PrettyJsonConverter(jsonPath);
            var success = await sheetContainer.Store(jsonConverter);

            return success;
        }
    }
}