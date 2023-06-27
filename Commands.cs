namespace xb360;

public static class Commands
{
    public static async Task<int> DisplayInfoAsync(string host)
    {
        await using var xbox = await GetXboxAsync(host);

        var cpuKey = await xbox.GetCpuKeyAsync();
        var kernelVersion = await xbox.GetKernelVersionAsync();
        var consoleType = await xbox.GetConsoleTypeAsync();

        Console.WriteLine("Console type: " + consoleType);
        Console.WriteLine("Kernel version: " + kernelVersion);
        Console.WriteLine("CPU Key: " + cpuKey);

        return 0;
    }

    // todo: handle reconnecting (what is reconnect port for?)
    // todo: handle ctrl c interruption
    public static async Task<int> PollLogsAsync(string host, int reconnectDelay, CancellationToken cancellationToken)
    {
        var channel1 = await GetXboxAsync(host);

        var port = channel1.SourcePort;

        await channel1.StartNotificationChannelAsync(port);

        await using (var channel2 = await GetXboxAsync(host))
        {
            await channel2.StartDebuggerAsync(port);
        }

        var stream = channel1.NetworkStream;

        var sr = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await sr.ReadLineAsync(cancellationToken);

                if (string.IsNullOrEmpty(line) || !line.StartsWith("debugstr"))
                {
                    continue;
                }

                var marker = "string=";

                var index = line.IndexOf(marker);
                var message = line[(index + marker.Length)..];

                Console.WriteLine(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exc)
            {
                Console.WriteLine("Something went wrong: " + exc.Message);
            }
        }

        await using (var channel3 = await GetXboxAsync(host))
        {
            await channel3.StopDebuggerAsync(port);
        }

        await using (var channel4 = await GetXboxAsync(host))
        {
            await channel4.StopNotificationChannelAsync(port);
        }

        await channel1.DisconnectAsync(sendBye: false);

        return 0;
    }

    public static async Task<int> LaunchExecutableAsync(string host, string executablePath)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.LaunchXexAsync(executablePath);

        return 0;
    }

    public static async Task<int> BootToDashboardAsync(string host)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.BootToDashboardAsync();

        return 0;
    }

    public static async Task<int> LoadPluginAsync(string host, string pluginPath)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.LoadModuleAsync(pluginPath);

        return 0;
    }

    public static async Task<int> UnloadPluginAsync(string host, string pluginName)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.UnloadModuleAsync(pluginName);

        return 0;
    }

    public static async Task<int> RestartConsoleAsync(string host)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.RebootAsync();

        return 0;
    }

    public static async Task<int> ShutdownConsoleAsync(string host)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.ShutdownAsync();

        return 0;
    }

    public static async Task<int> UploadToConsoleAsync(string host, string src, string dest)
    {
        await using var xbox = await GetXboxAsync(host);

        await xbox.UploadFileAsync(src, dest);

        return 0;
    }

    private static async Task<Xbox> GetXboxAsync(string host)
    {
        var xbox = new Xbox();

        await xbox.ConnectAsync(host);

        return xbox;
    }

    private static readonly string _configFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xb360");

    public static void SetConfig(string key, string value)
    {
        var lines = Array.Empty<string>();

        if (File.Exists(_configFile))
        {
            try
            {

                lines = File.ReadAllLines(_configFile);
            }
            catch { }
        }

        var search = key + "=";
        var newConfigLine = search + value;
        var added = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith(search))
            {
                lines[i] = newConfigLine;
                added = true;
                break;
            }
        }

        if (!added)
        {
            var length = lines.Length;

            Array.Resize(ref lines, length + 1);

            lines[length] = newConfigLine;
        }

        File.WriteAllLines(_configFile, lines);
    }

    public static void ListConfig()
    {
        if (!File.Exists(_configFile))
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(_configFile).TrimEnd();

            Console.WriteLine(content);
        }
        catch { }
    }

    public static void ClearConfig()
    {
        if (!File.Exists(_configFile))
        {
            return;
        }

        try
        {
            File.Delete(_configFile);
        }
        catch { }
    }

    public static string GetConfigValueOrDefault(string key)
    {
        var lines = Array.Empty<string>();

        if (File.Exists(_configFile))
        {
            try
            {

                lines = File.ReadAllLines(_configFile);
            }
            catch { }
        }

        var search = key + "=";

        foreach (var line in lines)
        {
            if (!line.StartsWith(search))
            {
                continue;
            }

            return line[search.Length..];
        }

        return string.Empty;
    }
}
