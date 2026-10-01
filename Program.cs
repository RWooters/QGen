// IQATS (C# port) – Intelligent Question and Answer Test Summarizer
// Entry point, replacing loading talk.scm / startup.scm in the MIT Scheme REPL.
//
//   QGen [--data <dir>]                    interactive chatting client (talk.scm)
//   QGen [--data <dir>] --startup <file>   parse the first five sentences of a text file (startup.scm)

using QGen;

string? dataDir = null;
string? startupFile = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--data" when i + 1 < args.Length:
            dataDir = args[++i];
            break;
        case "--startup" when i + 1 < args.Length:
            startupFile = args[++i];
            break;
        default:
            Console.Error.WriteLine("usage: QGen [--data <dir>] [--startup <text file>]");
            return 1;
    }
}

// The original used c:\iqats\. Default to ./Data next to the executable.
DataFiles.Directory = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
if (!File.Exists(DataFiles.PathOf("grammar.dat")))
{
    Console.Error.WriteLine($"IQATS data files not found in {Path.GetFullPath(DataFiles.Directory)}");
    return 1;
}

var talk = new Talk(Console.In, Console.Out);
if (startupFile is not null)
    talk.Startup(Path.GetFullPath(startupFile));
else
    talk.Run();
return 0;
