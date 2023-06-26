using System.CommandLine;

namespace xb360;

public record RootContext(RootCommand RootCommand, Option<string> HostOption);
