using System.Reflection.Metadata;
using System.Text;

namespace xbox;

public class MW2 
{
    private readonly Xbox xbox;

    public MW2(Xbox xbox)
    {
        this.xbox = xbox;
    }

    public async Task SendMessage(int clientIndex, string message) 
    {
        await SV_GameSendServerCommand(clientIndex, 0, $"c \"{message}\"");
    }

    public async Task Kick(int clientIndex, string reason = "")
    {
        if (clientIndex < 0 || clientIndex > 17)
        {
            throw new ArgumentOutOfRangeException(nameof(clientIndex));
        }

        // await SV_DropClient(clientIndex, reason);
        await SV_GameSendServerCommand(clientIndex, 0, $"t \"{reason}\"");
    }

    public async Task Derank(int clientIndex)
    {
        await SV_GameSendServerCommand(clientIndex, 0, "s activeaction \"scr_gameEnded;xblive_rankedmatch 1;onlinegame 1;xblive_privatematch 0;resetStats;defaultStatsInit\"");

        await Kick(clientIndex);
    }

    public async Task FreezeConsole(int clientIndex)
    {
        await SendMessage(clientIndex, "^1Bye Bye!");
        await SV_GameSendServerCommand(clientIndex, 0, "e \"^\x0001\"");
    }

    public async Task FreezeClasses(int clientIndex)
    {
        await SetClassName(clientIndex, 0, "^\x0001");
    }

    public async Task SetName(int clientIndex, string name) 
    {
        if (clientIndex < 0 || clientIndex > 17)
        {
            throw new ArgumentOutOfRangeException(nameof(clientIndex));
        }

        var nameAddress = client_session_s(clientIndex) + 0x3290;
        var bytes = Encoding.ASCII.GetBytes(name + "\0");

        var buffer = new byte[NameSize];
        Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

        await xbox.SetMemoryAsync(nameAddress, buffer);
    }

    public async Task<string> GetName(int clientIndex) 
    {
        var nameAddress = client_session_s(clientIndex) + 0x3290;

        return await xbox.GetMemoryAsync<string>(nameAddress, NameSize);
    }

    public async Task SetClassName(int clientIndex, int classIndex, string className)
    {
        if (className.Length > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(className), "Class name must be 20 characters or less.");
        }

        var classValue = 3003 /* custom classes */ + (classIndex * 64 /* size of class */) + 37 /* name */;
        var hexClassName = Convert.ToHexString(Encoding.ASCII.GetBytes(className)).ToUpper();

        await SV_GameSendServerCommand(clientIndex, 1, $"J {classValue} {hexClassName}");
    }

    public async Task SetPregameName(string name) 
    {
        if (name.Length > NameSize)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Name must be 31 characters or less.");
        }

        var bytes = Encoding.ASCII.GetBytes(name + '\0');

        var buffer = new byte[NameSize];
        Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

        await xbox.SetMemoryAsync(0x838BA824, buffer);
    }

    public async Task<string> GetPregameName() 
    {
        return await xbox.GetMemoryAsync<string>(0x838BA824, NameSize);
    }

    public async Task SetPregameClantag(string clantag)
    {
        if (clantag.Length > ClantagSize)
        {
            throw new ArgumentOutOfRangeException(nameof(clantag), "Clantag must be 4 characters or less.");
        }

        var bytes = Encoding.ASCII.GetBytes(clantag);

        var buffer = new byte[ClantagSize];
        Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

        await xbox.SetMemoryAsync(0x82687060, buffer);
    }

    public async Task<string> GetPregameClantag()
    {
        return await xbox.GetMemoryAsync<string>(0x82687060, ClantagSize);
    }

    public async Task StartGameFromLobby()
    {
        await Cbuf_AddText(0, "set party_connectToOthers 0;party_minplayers 1;party_minLobbyTime 1;party_pregameTimer 1;party_gameStartTimerLength 1;party_pregameStartTimerLength 1;party_minLobbyTime 1;party_timer 1");
        await Cbuf_AddText(0, "xpartygo");
    }

    private async Task SV_GameSendServerCommand(int clientIndex, int commandType, string command)
    {
        await xbox.CallMethodAsync(0x822548D8, clientIndex, commandType, command);
    }

    // TODO: This doesn't work properly
    private async Task SV_DropClient(int clientIndex, string reason, bool tellThem = true)
    {
        await xbox.CallMethodAsync(0x822523A8, clientIndex, reason, tellThem);
    }

    private async Task Cbuf_AddText(int arg1, string command)
    {
        await xbox.CallMethodAsync(0x82224990, arg1, command);
    }

    private uint client_session_s(int clientIndex)
    {
        return (uint)(0x830CBF80 + (clientIndex * 0x3700));
    }

    private const uint NameSize = 32;
    private const uint ClantagSize = 4;
}