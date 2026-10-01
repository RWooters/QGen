// IQATS (C# port)
// Sexp.cs
// Minimal Scheme data model used by the port of parser.scm / manip.scm.
//
// Scheme value  ->  C# representation
//   symbol      ->  Symbol (interned, so eq? is reference equality)
//   string      ->  string
//   list        ->  List<object>
//   '() / #f    ->  empty list / null (MIT Scheme 7.4 treats #f and '() as the same object)

using System.Text;

namespace QGen;

/// <summary>Raised wherever the original Scheme code would have signalled an error.</summary>
public sealed class SchemeError(string message) : Exception(message);

/// <summary>Interned Scheme symbol.</summary>
public sealed class Symbol
{
    static readonly Dictionary<string, Symbol> Table = new(StringComparer.Ordinal);

    public string Name { get; }

    Symbol(string name) => Name = name;

    public static Symbol Intern(string name)
    {
        lock (Table)
        {
            if (!Table.TryGetValue(name, out var sym))
                Table[name] = sym = new Symbol(name);
            return sym;
        }
    }

    public override string ToString() => Name;
}

/// <summary>Frequently used symbols.</summary>
public static class Sym
{
    public static readonly Symbol Unknown = Symbol.Intern("unknown");
    public static readonly Symbol Eos = Symbol.Intern("eos");
    public static readonly Symbol Number = Symbol.Intern("number");
    public static readonly Symbol ProperNoun = Symbol.Intern("proper-noun");
    public static readonly Symbol Person = Symbol.Intern("person");
    public static readonly Symbol Determiner = Symbol.Intern("determiner");
    public static readonly Symbol NounPhrase = Symbol.Intern("noun-phrase");
    public static readonly Symbol Null = Symbol.Intern(""); // symbol-null
}

/// <summary>List primitives with the same semantics as their Scheme namesakes.</summary>
public static class Sexp
{
    public static List<object> L(params object[] items) => [.. items];

    public static object Car(object? x) =>
        x is List<object> { Count: > 0 } l ? l[0] : throw new SchemeError($"car: {Write(x)} is not a pair");

    public static List<object> Cdr(object? x) =>
        x is List<object> { Count: > 0 } l ? l.GetRange(1, l.Count - 1) : throw new SchemeError($"cdr: {Write(x)} is not a pair");

    public static object Cadr(object? x) => Car(Cdr(x));

    public static object Caddr(object? x) => Car(Cdr(Cdr(x)));

    /// <summary>(sublist list start end) – start inclusive, end exclusive.</summary>
    public static List<object> Sublist(List<object> list, int start, int end)
    {
        if (start < 0 || end > list.Count || start > end)
            throw new SchemeError($"sublist: range {start}..{end} out of bounds for list of length {list.Count}");
        return list.GetRange(start, end - start);
    }

    public static List<object> Append(params IEnumerable<object>[] lists)
    {
        var result = new List<object>();
        foreach (var l in lists) result.AddRange(l);
        return result;
    }

    /// <summary>(last-pair list) – a one element list holding the last object.</summary>
    public static List<object> LastPair(List<object> list) =>
        list.Count > 0 ? [list[^1]] : throw new SchemeError("last-pair: empty list");

    /// <summary>Scheme eq?: interned symbols compare by reference, as do lists and strings.</summary>
    public static bool Eq(object? a, object? b) => ReferenceEquals(a, b) || (a is null && b is List<object> { Count: 0 }) || (b is null && a is List<object> { Count: 0 });

    /// <summary>(not (not (memq obj list))) – memq forced to a boolean.</summary>
    public static bool Memq(object? obj, IEnumerable<object> list) => list.Any(x => Eq(obj, x));

    /// <summary>(memq obj list) returning the tail starting at obj, or an empty list (#f).</summary>
    public static List<object> MemqTail(object? obj, List<object> list)
    {
        int i = list.FindIndex(x => Eq(obj, x));
        return i < 0 ? [] : list.GetRange(i, list.Count - i);
    }

    /// <summary>Scheme equal?.</summary>
    public static bool IsEqual(object? a, object? b) => (a, b) switch
    {
        (string sa, string sb) => sa == sb,
        (List<object> la, List<object> lb) => la.Count == lb.Count && la.Zip(lb).All(p => IsEqual(p.First, p.Second)),
        (int ia, int ib) => ia == ib,
        _ => Eq(a, b),
    };

    /// <summary>Scheme truthiness, given that '() and #f are the same object in MIT Scheme 7.4.</summary>
    public static bool Truthy(object? x) => x switch
    {
        null => false,
        bool b => b,
        List<object> l => l.Count > 0,
        _ => true,
    };

    /// <summary>Output of Scheme's write procedure (strings quoted).</summary>
    public static string Write(object? x) => Print(x, quoteStrings: true);

    /// <summary>Output of Scheme's display procedure (strings unquoted).</summary>
    public static string Display(object? x) => Print(x, quoteStrings: false);

    static string Print(object? x, bool quoteStrings)
    {
        var sb = new StringBuilder();
        PrintTo(sb, x, quoteStrings);
        return sb.ToString();
    }

    static void PrintTo(StringBuilder sb, object? x, bool quoteStrings)
    {
        switch (x)
        {
            case null:
                sb.Append("()");
                break;
            case bool b:
                sb.Append(b ? "#t" : "#f");
                break;
            case string s when quoteStrings:
                sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                break;
            case List<object> l:
                sb.Append('(');
                for (int i = 0; i < l.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    PrintTo(sb, l[i], quoteStrings);
                }
                sb.Append(')');
                break;
            default:
                sb.Append(x);
                break;
        }
    }
}
