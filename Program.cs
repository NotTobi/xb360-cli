using System.CommandLine;
using System.CommandLine.Builder;
using xb360;

var rootCommand = new RootCommand("A xbdm based CLI to interact with your Xbox360 console");

// todo: provide config command to persist this host option
var hostOption = new Option<string>("--host", "The IP Address or hostname of the console");

rootCommand.AddGlobalOption(hostOption);

var rootContext = new RootContext(rootCommand, hostOption);

rootContext.AddInfoCommand();
rootContext.AddLogsCommand();
rootContext.AddLaunchCommand();
rootContext.AddDashboardCommand();
rootContext.AddLoadPluginCommand();
rootContext.AddUnloadPluginCommand();
rootContext.AddRestartCommand();
rootContext.AddShutdownCommand();
rootContext.AddUploadCommand();

await rootCommand.InvokeAsync(args);

// var builder = new CommandLineBuilder(rootCommand);

// builder
//     .UseDefaults()
//     .UseExceptionHandler((exception, context) =>
//     {
//         Console.WriteLine(exception.Message);
//     }, errorExitCode: 1);

// await builder.Command.InvokeAsync(args);