// IQATS (C# port)
// DataFiles.cs
// File helpers from parser.scm and manip.scm (open-path, get-everything,
// make-saved-file, write-type, write-def, write-period, delete-last-chars).
// The original hard-codes c:\iqats\; here the directory is configurable.

namespace QGen;

public static class DataFiles
{
    /// <summary>Data files keep the DOS line endings they were written with.</summary>
    const string FileNewLine = "\r\n";

    /// <summary>Directory holding grammar.dat, manip.lst, iqats.log, ...</summary>
    public static string Directory { get; set; } = ".";

    public static string PathOf(string file) => Path.Combine(Directory, file);

    /// <summary>(open-path file) – opens a fresh reader over the data file.</summary>
    public static SentenceReader OpenPath(string file) => new(GetEverything(file));

    /// <summary>(get-everything file) – the whole file as a string (line endings normalised).</summary>
    public static string GetEverything(string file)
    {
        var path = PathOf(file);
        if (!File.Exists(path))
            throw new SchemeError($"Unable to open file \"{path}\" because: File does not exist.");
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    /// <summary>
    /// make-saved-file + writes: the original reopens the file, copies the old
    /// contents back and then writes after them, i.e. an append.
    /// </summary>
    public static void Append(string file, string text) =>
        File.AppendAllText(PathOf(file), text.Replace("\n", FileNewLine));

    /// <summary>(write-type type file) – newline then the type name.</summary>
    public static void WriteType(string type, string file) => Append(file, "\n" + type);

    /// <summary>(write-def word file) – newline then the written object.</summary>
    public static void WriteDef(object word, string file) => Append(file, "\n" + Sexp.Write(word));

    /// <summary>(write-period file) – closes an entry.</summary>
    public static void WritePeriod(string file) => Append(file, ".\n");

    /// <summary>
    /// (delete-last-chars file num) – rewrites the file without its last num
    /// characters. Use num = 2 to take the final ".\n" off manip.lst.
    /// </summary>
    public static void DeleteLastChars(string file, int num)
    {
        var contents = GetEverything(file);
        if (num > contents.Length)
            throw new SchemeError($"sublist: cannot delete {num} characters from {file}");
        contents = contents[..^num];
        File.WriteAllText(PathOf(file), contents.Replace("\n", FileNewLine));
        // the original then writes symbol-null, which prints as nothing
    }
}
