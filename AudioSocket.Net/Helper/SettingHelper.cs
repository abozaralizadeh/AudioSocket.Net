using System.Reflection;

namespace AudioSocket.Net.Helper
{
    public class SettingHelper
    {
        public SettingHelper()
        {
        }

        public static IConfiguration GetConfigurations()
        {
            var builder = new ConfigurationBuilder()
                      .SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables()
                      .AddUserSecrets(Assembly.GetExecutingAssembly(), true);


            return builder.Build();
        }
    }
}

