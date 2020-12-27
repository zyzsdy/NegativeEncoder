using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder.SystemOptions
{
    public static class SystemOption
    {
        public static async Task<T> ReadOption<T>() where T : class, new()
        {
            var defaultOption = new T();
            var configName = typeof(T).Name;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = System.IO.Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.json");

            //判断文件是否存在
            if (!System.IO.File.Exists(configPath))
            {
                return defaultOption;
            }

            var configFileString = await System.IO.File.ReadAllTextAsync(configPath);
            var jsonOption = JsonConvert.DeserializeObject<T>(configFileString);

            return jsonOption;
        }

        public static async Task SaveOption(object config)
        {
            var configName = config.GetType().Name;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = System.IO.Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.json");

            var configBase = System.IO.Path.GetDirectoryName(configPath);

            //判断目录是否存在
            if (!System.IO.Directory.Exists(configBase))
            {
                System.IO.Directory.CreateDirectory(configBase);
            }

            var jsonString = JsonConvert.SerializeObject(config);
            await System.IO.File.WriteAllTextAsync(configPath, jsonString);
        }
    }
}
