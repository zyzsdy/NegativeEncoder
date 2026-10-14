using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace NegativeEncoder.SystemOptions;

public static class SystemOption
{
    public static Task<T> ReadOption<T>() where T : class, new()
    {
        var configName = typeof(T).Name;
        var configPath = Path.Combine(GetConfigBasePath(), $"{configName}.json");
        return ReadOptionInternalAsync(configPath, new T(), backupOnInvalid: true);
    }

    public static Task<List<T>> ReadListOption<T>() where T : class, new()
    {
        var configName = typeof(T).Name;
        var configPath = Path.Combine(GetConfigBasePath(), $"{configName}.list.json");
        return ReadOptionInternalAsync(configPath, new List<T>(), backupOnInvalid: true);
    }

    public static Task<List<T>> ReadListOption<T>(string filePath) where T : class, new()
    {
        return ReadOptionInternalAsync(filePath, new List<T>(), backupOnInvalid: false);
    }

    public static async Task SaveOption(object config)
    {
        string configPath, jsonString;

        lock (config)
        {
            var configName = config.GetType().Name;
            configPath = Path.Combine(GetConfigBasePath(), $"{configName}.json");

            var configBase = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configBase) && !Directory.Exists(configBase)) Directory.CreateDirectory(configBase);

            jsonString = JsonConvert.SerializeObject(config);
        }

        await File.WriteAllTextAsync(configPath, jsonString);
    }

    public static async Task SaveListOption<T>(List<T> configs)
    {
        var configName = typeof(T).Name;
        var configPath = Path.Combine(GetConfigBasePath(), $"{configName}.list.json");

        var configBase = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configBase) && !Directory.Exists(configBase)) Directory.CreateDirectory(configBase);

        var jsonString = JsonConvert.SerializeObject(configs);
        await File.WriteAllTextAsync(configPath, jsonString);
    }

    public static async Task SaveListOption<T>(List<T> configs, string filePath)
    {
        var fileBase = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileBase) && !Directory.Exists(fileBase)) Directory.CreateDirectory(fileBase);

        var jsonString = JsonConvert.SerializeObject(configs);
        await File.WriteAllTextAsync(filePath, jsonString);
    }

    private static string GetConfigBasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "NegativeEncoder", "Config");
    }

    private static async Task<TOption> ReadOptionInternalAsync<TOption>(string filePath, TOption defaultOption,
        bool backupOnInvalid)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return defaultOption;

        try
        {
            var configFileString = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(configFileString))
            {
                if (backupOnInvalid) BackupInvalidConfig(filePath);
                return defaultOption;
            }

            var jsonOption = JsonConvert.DeserializeObject<TOption>(configFileString);
            if (jsonOption == null)
            {
                if (backupOnInvalid) BackupInvalidConfig(filePath);
                return defaultOption;
            }

            return jsonOption;
        }
        catch (JsonException)
        {
            if (backupOnInvalid) BackupInvalidConfig(filePath);
            return defaultOption;
        }
        catch (Exception e)
        {
            if (backupOnInvalid) BackupInvalidConfig(filePath);
            MessageBox.Show(e.ToString(), e.Message);
            return defaultOption;
        }
    }

    private static void BackupInvalidConfig(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = $"{filePath}.bad.{timestamp}";
            var index = 1;
            while (File.Exists(backupPath))
            {
                backupPath = $"{filePath}.bad.{timestamp}.{index}";
                index++;
            }

            File.Copy(filePath, backupPath, true);
        }
        catch
        {
            // ignored
        }
    }
}
