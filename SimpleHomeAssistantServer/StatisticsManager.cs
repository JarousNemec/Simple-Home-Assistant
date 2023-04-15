﻿using System.Collections.Specialized;
using System.Configuration;
using System.Text.Json;
using System.Timers;
using SimpleHomeAssistantServer.Models;
using SimpleHomeAssistantServer.Workers;
using Timer = System.Timers.Timer;

namespace SimpleHomeAssistantServer;

public class StatisticsManager
{
    private MqttManager _mqttManager;
    private Dictionary<string, List<DevicePowerStateRecord>> _todayDeviceStatistics = null!;
    private NameValueCollection _config;
    private Timer _autoSave;
    private Timer _statisticsLogger;

    public StatisticsManager(MqttManager mqttManager)
    {
        _todayDeviceStatistics = new Dictionary<string, List<DevicePowerStateRecord>>();
        _config = ConfigurationManager.AppSettings;
        _mqttManager = mqttManager;
        Load();

        _autoSave = new Timer();
        _autoSave.AutoReset = true;
        _autoSave.Interval = 3000000;
        _autoSave.Elapsed += AutoSaveOnElapsed;
        // _autoSave.Start();

        _statisticsLogger = new Timer();
        _statisticsLogger.AutoReset = true;
        _statisticsLogger.Interval = 60000;
        _statisticsLogger.Elapsed += StatisticsLoggerOnElapsed;
        _statisticsLogger.Start();
    }

    private void StatisticsLoggerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        var topics = _mqttManager.DevicesRegister.Select(device => device.Topic).ToArray();
        var statisticsWorker = new Thread(new MqttDevicesPowerStatesWorker(topics, _todayDeviceStatistics).Run);
        statisticsWorker.Start();
    }

    private void AutoSaveOnElapsed(object? sender, ElapsedEventArgs e)
    {
        Save();
    }

    private void Load()
    {
        var todayStatisticsFileName = $"{_config.Get("StatisticsPath")}{DateTime.Today}.rec";
        if (File.Exists(todayStatisticsFileName))
        {
            var data = File.ReadAllText(todayStatisticsFileName);
            _todayDeviceStatistics =
                JsonSerializer.Deserialize<Dictionary<string, List<DevicePowerStateRecord>>>(data) ??
                new Dictionary<string, List<DevicePowerStateRecord>>();
        }
        else
        {
            _todayDeviceStatistics = new Dictionary<string, List<DevicePowerStateRecord>>();
        }
    }

    public void Save()
    {
        var data = JsonSerializer.Serialize(_todayDeviceStatistics);
        var todayStatisticsFileName = $"{_config.Get("StatisticsPath")}{DateTime.Today}.rec";
        File.WriteAllText(todayStatisticsFileName, data);
    }
}