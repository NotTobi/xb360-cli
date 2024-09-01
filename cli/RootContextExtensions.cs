using System.CommandLine;

namespace xb360;

public static class RootContextExtensions
{
    public static void AddInfoCommand(this RootContext context)
    {
        var infoCommand = new Command("info", "Displays information about the console");

        infoCommand.SetHandler(Commands.DisplayInfoAsync, context.HostOption);

        context.RootCommand.AddCommand(infoCommand);
    }

    public static void AddLogsCommand(this RootContext context)
    {
        var logsCommand = new Command("logs", "Continuously polls logs from the console");

        var reconnectDelayOption = new Option<int>("--reconnectDelay",
            "The delay between reconnect attempts, if the console is no longer available");
        reconnectDelayOption.SetDefaultValueFactory(() => 1000);
        reconnectDelayOption.AddAlias("-d");

        logsCommand.AddOption(reconnectDelayOption);

        logsCommand.SetHandler(async ctx =>
        {
            var host = ctx.ParseResult.GetValueForOption(context.HostOption);
            var reconnectDelay = ctx.ParseResult.GetValueForOption(reconnectDelayOption);
            var token = ctx.GetCancellationToken();

            ctx.ExitCode = await Commands.PollLogsAsync(host!, reconnectDelay, token);
        });

        context.RootCommand.AddCommand(logsCommand);
    }

    public static void AddLaunchCommand(this RootContext context)
    {
        var launchCommand = new Command("launch", "Launches an executable on the console");

        var executablePathArgument = new Argument<string>("path", "The console path to the executable");

        launchCommand.AddArgument(executablePathArgument);

        launchCommand.SetHandler(Commands.LaunchExecutableAsync, context.HostOption, executablePathArgument);

        context.RootCommand.AddCommand(launchCommand);
    }

    public static void AddDashboardCommand(this RootContext context)
    {
        var dashboardCommand = new Command("dash", "Boots to the dashboard");

        dashboardCommand.SetHandler(Commands.BootToDashboardAsync, context.HostOption);

        context.RootCommand.AddCommand(dashboardCommand);
    }

    public static void AddModuleCommands(this RootContext context)
    {
        var moduleCommand = new Command("module", "Manages modules");

        // LOAD
        var loadCommand = new Command("load", "Loads a module");

        var pluginPath = new Argument<string>("path", "The path to the module");

        loadCommand.AddArgument(pluginPath);

        loadCommand.SetHandler(Commands.LoadPluginAsync, context.HostOption, pluginPath);

        moduleCommand.AddCommand(loadCommand);

        // UNLOAD
        var unloadCommand = new Command("unload", "Unloads a module");

        var pluginName = new Argument<string>("name", "The name of the loaded module");

        unloadCommand.AddArgument(pluginName);

        unloadCommand.SetHandler(Commands.UnloadPluginAsync, context.HostOption, pluginName);

        moduleCommand.AddCommand(unloadCommand);

        context.RootCommand.AddCommand(moduleCommand);
    }

    public static void AddRestartCommand(this RootContext context)
    {
        var restartCommand = new Command("restart", "Restarts the console");

        restartCommand.SetHandler(Commands.RestartConsoleAsync, context.HostOption);

        context.RootCommand.AddCommand(restartCommand);
    }

    public static void AddShutdownCommand(this RootContext context)
    {
        var shutdownCommand = new Command("shutdown", "Shuts down the console");

        shutdownCommand.SetHandler(Commands.ShutdownConsoleAsync, context.HostOption);

        context.RootCommand.AddCommand(shutdownCommand);
    }

    public static void AddUploadCommand(this RootContext context)
    {
        var uploadCommand = new Command("upload", "Uploads one or more files to the console");

        var srcArgument = new Argument<string>("src", "The path on the client");
        var destArgument = new Argument<string>("dest", "The path on the console");

        uploadCommand.AddArgument(srcArgument);
        uploadCommand.AddArgument(destArgument);

        uploadCommand.SetHandler(Commands.UploadToConsoleAsync, context.HostOption, srcArgument, destArgument);

        context.RootCommand.AddCommand(uploadCommand);
    }

    // todo: --host shouldn't be on this
    public static void AddConfigCommands(this RootContext context)
    {
        var configCommand = new Command("config", "Manages configuration options");

        // LIST
        var listCommand = new Command("list", "Lists all configured options");

        listCommand.SetHandler(Commands.ListConfig);

        configCommand.AddCommand(listCommand);

        // SET
        var setCommand = new Command("set", "Sets a configuration option");

        var keyArgument = new Argument<string>("key");
        keyArgument.FromAmong("host");

        var valueArgument = new Argument<string>("value");

        setCommand.AddArgument(keyArgument);
        setCommand.AddArgument(valueArgument);

        setCommand.SetHandler(Commands.SetConfig, keyArgument, valueArgument);

        configCommand.AddCommand(setCommand);

        // CLEAR
        var clearCommand = new Command("clear", "Clears all configured options");

        clearCommand.SetHandler(Commands.ClearConfig);

        configCommand.AddCommand(clearCommand);

        context.RootCommand.AddCommand(configCommand);
    }
}