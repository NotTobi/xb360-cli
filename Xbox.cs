using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace xb360;

public class Xbox : IAsyncDisposable
{
    private TcpClient? _client;

    public Stream NetworkStream => _client?.GetStream() ?? throw new Exception("client is null");

    public int SourcePort
    {
        get
        {
            if (_client is null)
            {
                throw new Exception("client is null");
            }

            if (_client.Client.LocalEndPoint is IPEndPoint ipEndpoint)
            {
                return ipEndpoint.Port;
            }

            throw new Exception("expected ipendpoint");
        }
    }

    public int RemotePort
    {
        get
        {
            if (_client is null)
            {
                throw new Exception("client is null");
            }

            if (_client.Client.RemoteEndPoint is IPEndPoint ipEndpoint)
            {
                return ipEndpoint.Port;
            }

            throw new Exception("expected ipendpoint");
        }
    }

    public async Task ConnectAsync(string ipOrHostname)
    {
        if (string.IsNullOrEmpty(ipOrHostname))
        {
            throw new Exception("Not IP address or hostname specified");
        }

        if (!IPAddress.TryParse(ipOrHostname, out var ip))
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(ipOrHostname);

                if (addresses.Length < 1)
                {
                    throw new Exception("no addresses found");
                }

                ip = addresses.First();
            }
            catch
            {
                throw new Exception("Failed to resolve hostname: " + ipOrHostname);
            }
        }

        _client = new TcpClient
        {
            SendTimeout = 5000,
            ReceiveTimeout = 2000,
            ReceiveBufferSize = 0x100000 * 3,
            SendBufferSize = 0x100000 * 3,
            NoDelay = true
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _client.ConnectAsync(ip, 730, cts.Token);

        var (status, _) = await ReadSingleLineAsync(cts.Token);

        if (status != 201)
        {
            throw new Exception("incorrect connection message");
        }
    }

    public async Task DisconnectAsync(bool sendBye = true)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            if (sendBye)
            {
                await SendCommandAsync("bye");
            }
        }
        catch (Exception exc)
        {
            LogDebug("failed to bye: " + exc.Message);
        }

        try
        {
            _client.Close();

            _client.Dispose();
        }
        finally
        {
            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
        => await DisconnectAsync();

    public async Task<string> GetCpuKeyAsync()
    {
        await SendCommandAsync("consolefeatures ver=2 type=10 params=\"A\\0\\A\\0\\\"");

        var (_, data) = await ReadSingleLineAsync();

        return data;
    }

    public async Task<string> GetKernelVersionAsync()
    {
        await SendCommandAsync("consolefeatures ver=2 type=13 params=\"A\\0\\A\\0\\\"");

        var (_, data) = await ReadSingleLineAsync();

        return data;
    }

    public async Task<string> GetConsoleTypeAsync()
    {
        await SendCommandAsync("consolefeatures ver=2 type=17 params=\"A\\0\\A\\0\\\"");

        var (_, data) = await ReadSingleLineAsync();

        return data;
    }

    public async Task BootToDashboardAsync()
    {
        await SendCommandAsync("magicboot");
    }

    public async Task RebootAsync()
    {
        await SendCommandAsync("magicboot  COLD");
    }

    public async Task ShutdownAsync()
    {
        await SendCommandAsync("consolefeatures ver=2 type=11 params=\"A\\0\\A\\0\\\"");
    }

    private async Task<bool> FileExists(string path)
    {
        try
        {
            await SendCommandAsync($"getfileattributes name=\"{path}\"");

            var (status, _) = await ReadSingleLineAsync();

            if (status == 402)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LaunchXexAsync(string path)
    {
        var xexPath = GetXboxPath(path);
        var directoryPath = GetDOSDirectoryName(xexPath);

        if (!await FileExists(xexPath))
        {
            throw new Exception($"File \"{xexPath}\" doesn't exist");
        }

        var command = $"magicboot title=\"{xexPath}\" directory=\"{directoryPath}\"";

        await SendCommandAsync(command);
    }

    // todo: handle directories
    public async Task UploadFileAsync(string src, string dest)
    {
        if (_client is null)
        {
            return;
        }

        var xboxPath = GetXboxPath(dest);

        if (!File.Exists(src))
        {
            throw new Exception("File doesn't exist: " + src);
        }

        var fileInfo = new FileInfo(src);
        var fileLength = fileInfo.Length;
        var fileLengthHex = string.Format("{0:x}", fileLength);

        await SendCommandAsync($"sendfile name=\"{xboxPath}\" length=0x{fileLengthHex}");

        var (status, _) = await ReadSingleLineAsync();

        if (status != (int)Status.READY_TO_ACCEPT_DATA)
        {
            throw new Exception("Wrong status");
        }

        var bufferSize = 0x10000;

        // todo: close?
        var fs = File.OpenRead(src);

        var offset = 0;
        while (fileLength > 0)
        {
            var length = bufferSize >= fileLength ? (int)fileLength : bufferSize;
            var buffer = new byte[length];

            fs.Read(buffer, offset, length);

            _client.Client.Send(buffer, offset, length, SocketFlags.None);

            fileLength -= length;
            offset += length;
        }
    }

    // private async Task XNotify(string text, XNotiyLogo logo = XNotiyLogo.FLASHING_XBOX_CONSOLE)
    // {
    //     var hexText = GetHexString(text);
    //     var command =
    //         $"consolefeatures ver=2 type=12 params=\"A\\0\\A\\2\\{String}/{text.Length}\\{hexText}\\{Int}\\{logo}\\\"";

    //     await SendCommandAsync(command);
    // }

    public async Task StartNotificationChannelAsync(int port)
    {
        var command = $"notify reconnectport={port} reverse";

        await SendCommandAsync(command);

        await ReadSingleLineAsync();
    }

    public async Task StopNotificationChannelAsync(int port)
    {
        var command = $"notifyat port={port} drop";

        await SendCommandAsync(command);

        await ReadSingleLineAsync();
    }

    public async Task StartDebuggerAsync(int port)
    {
        var command = $"debugger connect port=0x{port:x8} override user=xb360-cli";

        await SendCommandAsync(command);

        await ReadSingleLineAsync();
    }

    public async Task StopDebuggerAsync(int port)
    {
        var command = $"debugger disconnect port=0x{port:x8}";

        await SendCommandAsync(command);

        await ReadSingleLineAsync();
    }

    public async Task LoadModuleAsync(string modulePath)
    {
        var xboxPath = GetXboxPath(modulePath);

        if (!await FileExists(xboxPath))
        {
            throw new Exception($"File \"{xboxPath}\" doesn't exist");
        }

        var moduleName = GetDOSFileName(xboxPath);

        if (string.IsNullOrEmpty(moduleName))
        {
            throw new Exception("Invalid module path");
        }

        var moduleHandle = await GetModuleHandleAsync(moduleName);

        if (moduleHandle > 0)
        {
            throw new Exception($"Module \"{moduleName}\" is already loaded");
        }

        var xexLoadImageAddress = await GetFunctionAddressAsync("xboxkrnl.exe", 409);

        await CallMethodAsync<uint>(xexLoadImageAddress, xboxPath, 8, 0, 0);
    }

    public async Task UnloadModuleAsync(string moduleName)
    {
        var moduleHandle = await GetModuleHandleAsync(moduleName);

        if (moduleHandle < 1)
        {
            throw new Exception($"Module \"{moduleName}\" isn't loaded");
        }

        await SetMemoryAsync(moduleHandle + 64, new byte[] { 0, 1 });

        var xexUnloadImageAddress = await GetFunctionAddressAsync("xboxkrnl.exe", 417);

        await CallMethodAsync(xexUnloadImageAddress, typeof(void), moduleHandle);
    }

    private async Task<uint> GetModuleHandleAsync(string moduleName)
    {
        var xexGetModuleHandleAddress = await GetFunctionAddressAsync("xam.xex", 1102);

        return await CallMethodAsync<uint>(xexGetModuleHandleAddress, moduleName);
    }

    public async Task<T> GetMemoryAsync<T>(uint address, uint size)
    {
        var bytes = await ReadMemoryAsync(address, size);

        if (typeof(T) == typeof(string))
        {
            return (T)(object)Encoding.UTF8.GetString(bytes);
        }
        else 
        {
            throw new InvalidOperationException("Invalid type");
        }
    }

    private async Task<byte[]> ReadMemoryAsync(uint address, uint size)
    {
        await SendCommandAsync($"getmem addr={address} length={size}");

        var (_, lines) = await ReadMultipleLinesAsync();

        return Convert.FromHexString(lines[1]);
    }

    public async Task SetMemoryAsync(uint address, byte[] data)
    {
        var length = data.Length;
        var offset = 0;

        while (length > 0)
        {
            var bytesToSend = (length > 128) ? 128 : length;

            var sb = new StringBuilder($"setmem addr=0x{address:X} data=");

            for (var i = 0; i < bytesToSend; i++)
            {
                var payload = (int)data[offset + i];
                sb.Append(payload.ToString("X2"));
            }

            var command = sb.ToString();

            await SendCommandAsync(command);

            var (status, _) = await ReadSingleLineAsync();

            if (status != (int)Status.OK)
            {
                throw new Exception("couldn't write memory block");
            }

            address += (uint)bytesToSend;
            length -= bytesToSend;
            offset += bytesToSend;
        }
    }

    public async Task CallMethodAsync(uint address, params object[] arguments)
    {
        await CallMethodAsync(address, typeof(void), arguments);
    }

    public async Task<T> CallMethodAsync<T>(uint address, params object[] arguments)
    {
        var result = await CallMethodAsync(address, typeof(T), arguments);

        return (T)result;
    }

    public async Task SendCommandAsync(string command, bool clearBuffer = true)
    {
        if (_client is null)
        {
            return;
        }

        if (clearBuffer)
        {
            await ClearBufferAsync();
        }

        LogDebug("Command: " + command);

        var bytes = Encoding.ASCII.GetBytes(command + EOL);

        await _client.Client.SendAsync(bytes);
    }

    private async Task<object> CallMethodAsync(uint address, Type type, params object[] arguments)
    {
        var sb = new StringBuilder();

        var returnType = GetType(type);

        sb.Append($"consolefeatures ver=2 type={returnType} system as=0 ");
        sb.Append($"params=\"A\\{address:X}\\A\\{arguments.Length}\\");

        foreach (var arg in arguments)
        {
            if (arg is uint || arg is int || arg is byte)
            {
                sb.Append(Int);
                sb.Append('\\');
                sb.Append(arg);
                sb.Append('\\');
            }
            else if (arg is bool boolValue)
            {
                sb.Append(Int);
                sb.Append('/');
                sb.Append(Convert.ToInt32(boolValue));
                sb.Append('\\');

                continue;
            }
            else if (arg is string stringValue)
            {
                sb.Append(ByteArray);
                sb.Append('/');
                sb.Append(stringValue.Length);
                sb.Append('\\');
                sb.Append(GetHexString(stringValue));
                sb.Append('\\');
            }
            else
            {
                throw new Exception("Invalid arg type");
            }
        }

        sb.Append('"');

        var command = sb.ToString();

        IncreaseClientTimeouts();

        await SendCommandAsync(command);

        if (type != typeof(void))
        {
            var (_, data) = await ReadSingleLineAsync();

            var bufferAddressMarker = "buf_addr=";

            while (data.Contains(bufferAddressMarker))
            {
                await Task.Delay(250);

                var afterMarker = data.IndexOf(bufferAddressMarker) + bufferAddressMarker.Length;
                var bufferAddress = data[afterMarker..];

                var subcommand = $"consolefeatures {bufferAddressMarker}0x{bufferAddress}";

                await ClearBufferAsync();

                await SendCommandAsync(subcommand);

                (_, data) = await ReadSingleLineAsync();
            }

            ResetClientTimeouts();

            return CoerceResult(data, type);
        }
        else 
        {
            ResetClientTimeouts();

            return 0;
        }
    }

    private async Task<uint> GetFunctionAddressAsync(string moduleName, int ordinal)
    {
        var moduleNameHex = GetHexString(moduleName);
        var command =
            $"consolefeatures ver=2 type=9 params=\"A\\0\\A\\2\\{String}/{moduleName.Length}\\{moduleNameHex}\\{Int}\\{ordinal}\\\"";

        await SendCommandAsync(command);

        var (_, data) = await ReadSingleLineAsync();

        return uint.Parse(data, NumberStyles.HexNumber);
    }

    private static string GetHexString(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private const uint Void = 0u;
    private const uint Int = 1u;
    private const uint String = 2u;
    private const uint Float = 3u;
    private const uint Byte = 4u;
    private const uint IntArray = 5u;
    private const uint FloatArray = 6u;
    private const uint ByteArray = 7u;
    private const uint Uint64 = 8u;
    private const uint Uint64Array = 9u;

    private static object CoerceResult(string result, Type type)
    {
        if (type == typeof(string))
        {
            return result;
        }
        else if (type == typeof(short))
        {
            return short.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(ushort))
        {
            return ushort.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(int))
        {
            return int.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(uint))
        {
            return uint.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(long))
        {
            return ulong.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(ulong))
        {
            return ulong.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(float))
        {
            return float.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(double))
        {
            return double.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(byte))
        {
            return byte.Parse(result, NumberStyles.HexNumber);
        }
        else if (type == typeof(char) && result.Length > 0)
        {
            return result[0];
        }

        return 0;
    }

    private static uint GetType(Type t)
    {
        if (t == typeof(void))
        {
            return Void;
        }

        if (t == typeof(int) || t == typeof(uint)
            || t == typeof(short) || t == typeof(ushort))
        {
            return Int;
        }

        if (t == typeof(string) || t == typeof(char[]))
        {
            return String;
        }

        if (t == typeof(byte) || t == typeof(char))
        {
            return Byte;
        }

        if (t == typeof(float) || t == typeof(double))
        {
            return Float;
        }

        return Uint64;
    }

    private const string EOL = "\r\n";

    private async Task ClearBufferAsync()
    {
        if (_client is null)
        {
            return;
        }

        var stream = _client.GetStream();

        LogDebug("Clearing buffer...");

        while (stream.DataAvailable)
        {
            LogDebug("Stream has data - reading...");
            var buffer = new byte[256];

            await stream.ReadAsync(buffer);
        }
    }

    private async Task<(int Status, string Data)> ReadSingleLineAsync(
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new Exception("client is null");
        }

        var stream = _client.GetStream();

        using var reader = new StreamReader(stream, leaveOpen: true);

        var line = await reader.ReadLineAsync(cancellationToken)
            ?? throw new Exception("failed to read line");

        LogDebug("Response: " + line);

        return SplitStatusLine(line);
    }

    private async Task<(int Status, IReadOnlyList<string> Lines)> ReadMultipleLinesAsync()
    {
        if (_client is null)
        {
            throw new Exception("client is null");
        }

        IncreaseClientTimeouts();

        var stream = _client.GetStream();

        using var reader = new StreamReader(stream, leaveOpen: true);

        var line = await reader.ReadLineAsync() ?? throw new Exception("failed to read line");

        var (status, data) = SplitStatusLine(line);

        LogDebug("Response: " + line);

        var lines = new List<string> { data };

        if (status != (int)Status.MULTILINE_RESPONSE)
        {
            ResetClientTimeouts();
            return (status, lines);
        }

        do {
            line = await reader.ReadLineAsync() ?? throw new Exception("failed to read line");

            if (line is null) 
            {
                break;
            }

            LogDebug("Sub Response: " + line);

            if (line == ".") 
            {
                LogDebug("End of response");
                break;
            }

            lines.Add(line);
        }
        while (true);

        ResetClientTimeouts();

        return (status, lines);
    }

    private static (int Status, string Data) SplitStatusLine(string input)
    {
        var delimiterIndex = input.IndexOf("-");
        var dataStartIndex = delimiterIndex + 2;

        var status = int.Parse(input[..delimiterIndex]);
        var data = input[dataStartIndex..];

        return (status, data);
    }

    private static string GetXboxPath(string path)
    {
        if (path.StartsWith("/"))
        {
            path = string.Concat("HDD:\\", path.AsSpan(1));
        }

        if (path.Contains('/'))
        {
            path = path.Replace('/', '\\');
        }

        return path;
    }

    private static void LogDebug(string message)
    {
#if DEBUG
        Console.WriteLine(message);
#endif
    }

    private static string? GetDOSFileName(string path)
    {
        var lastSlash = path.LastIndexOf('\\');

        if (lastSlash < 0)
        {
            return path;
        }

        return path[(lastSlash + 1)..];
    }

    private static string? GetDOSDirectoryName(string path)
    {
        var lastSlash = path.LastIndexOf('\\');

        if (lastSlash < 0)
        {
            return null;
        }

        return path[..lastSlash];
    }

    private enum Status : int
    {
        OK = 200,
        MULTILINE_RESPONSE = 202,
        READY_TO_ACCEPT_DATA = 204,
    }

    private enum XNotiyLogo : int
    {
        XBOX_LOGO = 0,
        NEW_MESSAGE_LOGO = 1,
        FRIEND_REQUEST_LOGO = 2,
        NEW_MESSAGE = 3,
        FLASHING_XBOX_LOGO = 4,
        GAMERTAG_SENT_YOU_A_MESSAGE = 5,
        GAMERTAG_SINGED_OUT = 6,
        GAMERTAG_SIGNEDIN = 7,
        GAMERTAG_SIGNED_INTO_XBOX_LIVE = 8,
        GAMERTAG_SIGNED_IN_OFFLINE = 9,
        GAMERTAG_WANTS_TO_CHAT = 10,
        DISCONNECTED_FROM_XBOX_LIVE = 11,
        DOWNLOAD = 12,
        FLASHING_MUSIC_SYMBOL = 13,
        FLASHING_HAPPY_FACE = 14,
        FLASHING_FROWNING_FACE = 15,
        FLASHING_DOUBLE_SIDED_HAMMER = 16,
        GAMERTAG_WANTS_TO_CHAT_2 = 17,
        PLEASE_REINSERT_MEMORY_UNIT = 18,
        PLEASE_RECONNECT_CONTROLLERM = 19,
        GAMERTAG_HAS_JOINED_CHAT = 20,
        GAMERTAG_HAS_LEFT_CHAT = 21,
        GAME_INVITE_SENT = 22,
        FLASH_LOGO = 23,
        PAGE_SENT_TO = 24,
        FOUR_2 = 25,
        FOUR_3 = 26,
        ACHIEVEMENT_UNLOCKED = 27,
        FOUR_9 = 28,
        GAMERTAG_WANTS_TO_TALK_IN_VIDEO_KINECT = 29,
        VIDEO_CHAT_INVITE_SENT = 30,
        READY_TO_PLAY = 31,
        CANT_DOWNLOAD_X = 32,
        DOWNLOAD_STOPPED_FOR_X = 33,
        FLASHING_XBOX_CONSOLE = 34,
        X_SENT_YOU_A_GAME_MESSAGE = 35,
        DEVICE_FULL = 36,
        FOUR_7 = 37,
        FLASHING_CHAT_ICON = 38,
        ACHIEVEMENTS_UNLOCKED = 39,
        X_HAS_SENT_YOU_A_NUDGE = 40,
        MESSENGER_DISCONNECTED = 41,
        BLANK = 42,
        CANT_SIGN_IN_MESSENGER = 43,
        MISSED_MESSENGER_CONVERSATION = 44,
        FAMILY_TIMER_X_TIME_REMAINING = 45,
        DISCONNECTED_XBOX_LIVE_11_MINUTES_REMAINING = 46,
        KINECT_HEALTH_EFFECTS = 47,
        FOUR_5 = 48,
        GAMERTAG_WANTS_YOU_TO_JOIN_AN_XBOX_LIVE_PARTY = 49,
        PARTY_INVITE_SENT = 50,
        GAME_INVITE_SENT_TO_XBOX_LIVE_PARTY = 51,
        KICKED_FROM_XBOX_LIVE_PARTY = 52,
        NULLED = 53,
        DISCONNECTED_XBOX_LIVE_PARTY = 54,
        DOWNLOADED = 55,
        CANT_CONNECT_XBL_PARTY = 56,
        GAMERTAG_HAS_JOINED_XBL_PARTY = 57,
        GAMERTAG_HAS_LEFT_XBL_PARTY = 58,
        GAMER_PICTURE_UNLOCKED = 59,
        AVATAR_AWARD_UNLOCKED = 60,
        JOINED_XBL_PARTY = 61,
        PLEASE_REINSERT_USB_STORAGE_DEVICE = 62,
        PLAYER_MUTED = 63,
        PLAYER_UNMUTED = 64,
        FLASHING_CHAT_SYMBOL = 65,
        UPDATING = 76,
    }

    private void IncreaseClientTimeouts()
    {
        _client!.SendTimeout = 4000000;
        _client!.ReceiveTimeout = 4000000;
    }

    private void ResetClientTimeouts()
    {
        _client!.SendTimeout = 5000;
        _client!.ReceiveTimeout = 2000;
    }
}
