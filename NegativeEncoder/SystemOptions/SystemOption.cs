using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace NegativeEncoder.SystemOptions;

public static class SystemOption
{
    public static async Task<T> ReadOption<T>() where T : class, new()
    {
        var defaultOption = new T();
        var configName = typeof(T).Name;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configPath = Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.json");

        //判断文件是否存在
        if (!File.Exists(configPath)) return defaultOption;

        //在LTSC2022上Async函数发生阻塞情况，暂时换同步
        var configFileString = await File.ReadAllTextAsync(configPath);
        var jsonOption = JsonConvert.DeserializeObject<T>(configFileString);

        return jsonOption;
    }

    public static async Task<List<T>> ReadListOption<T>() where T : class, new()
    {
        var defaultOption = new List<T>();
        var configName = typeof(T).Name;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configPath = Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.list.json");

        //判断文件是否存在
        if (!File.Exists(configPath)) return defaultOption;

        var configFileString = "";
        try
        {
            await using var configFileStream = new FileStream(configPath, FileMode.Open);
            using var sr = new StreamReader(configFileStream);
            configFileString = await sr.ReadToEndAsync();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.ToString(), e.Message);
        }

        if (string.IsNullOrEmpty(configFileString)) return defaultOption;

        var jsonOption = JsonConvert.DeserializeObject<List<T>>(configFileString);

        return jsonOption;
    }

    public static async Task<List<T>> ReadListOption<T>(string filePath) where T : class, new()
    {
        var defaultOption = new List<T>();

        //判断文件是否存在
        if (!File.Exists(filePath)) return defaultOption;

        var configFileString = "";
        try
        {
            await using var configFileStream = new FileStream(filePath, FileMode.Open);
            using var sr = new StreamReader(configFileStream);
            configFileString = await sr.ReadToEndAsync();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.ToString(), e.Message);
        }

        if (string.IsNullOrEmpty(configFileString)) return defaultOption;

        var jsonOption = JsonConvert.DeserializeObject<List<T>>(configFileString);

        return jsonOption;
    }

    public static async Task SaveOption(object config)
    {
        string configPath, jsonString;

        lock (config)
        {
            var configName = config.GetType().Name;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            configPath = Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.json");

            var configBase = Path.GetDirectoryName(configPath);

            //判断目录是否存在
            if (!Directory.Exists(configBase)) Directory.CreateDirectory(configBase!);

            jsonString = JsonConvert.SerializeObject(config);
        }

        await File.WriteAllTextAsync(configPath, jsonString);
    }

    public static async Task SaveListOption<T>(List<T> configs)
    {
        var configName = typeof(T).Name;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configPath = Path.Combine(appDataPath, $"NegativeEncoder/Config/{configName}.list.json");

        var configBase = Path.GetDirectoryName(configPath);

        //判断目录是否存在
        if (!Directory.Exists(configBase)) Directory.CreateDirectory(configBase!);

        var jsonString = JsonConvert.SerializeObject(configs);
        await File.WriteAllTextAsync(configPath, jsonString);
    }

    public static async Task SaveListOption<T>(List<T> configs, string filePath)
    {
        var fileBase = Path.GetDirectoryName(filePath);

        //判断目录是否存在
        if (!Directory.Exists(fileBase)) Directory.CreateDirectory(fileBase!);

        var jsonString = JsonConvert.SerializeObject(configs);
        await File.WriteAllTextAsync(filePath, jsonString);
    }
}