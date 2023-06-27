﻿using System.CommandLine;
using xb360;

var rootCommand = new RootCommand("A xbdm based CLI to interact with your Xbox360 console");

var hostOption = new Option<string>("--host", "The IP Address or hostname of the console");
hostOption.SetDefaultValueFactory(() => Commands.GetConfigValueOrDefault("host"));

rootCommand.AddGlobalOption(hostOption);

var rootContext = new RootContext(rootCommand, hostOption);

rootContext.AddInfoCommand();
rootContext.AddLogsCommand();
rootContext.AddLaunchCommand();
rootContext.AddDashboardCommand();
rootContext.AddModuleCommands();
rootContext.AddRestartCommand();
rootContext.AddShutdownCommand();
rootContext.AddUploadCommand();
rootContext.AddConfigCommands();

await rootCommand.InvokeAsync(args);

// todo: handle exceptions properly

// var builder = new CommandLineBuilder(rootCommand);

// builder
//     .UseDefaults()
//     .UseExceptionHandler((exception, context) =>
//     {
//         Console.WriteLine(exception.Message);
//     }, errorExitCode: 1);

// await builder.Command.InvokeAsync(args);