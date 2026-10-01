// IQATS (C# port)
// Parser.cs
// Port of parser.scm (originally coded by Sam Hsiung for MIT Scheme 7.4.2).
// Two levels of syntactic parsing implemented.
//
// A parsed word is a "phrase leaf"   (category ("word"))
// and a group of them a phrase        (category leaf leaf ...)
// e.g. (noun-phrase (determiner ("The")) (proper-noun ("Civil")) (proper-noun ("War")))

using System.Text.RegularExpressions;
using static QGen.Sexp;

namespace QGen;

/// <summary>
/// A procedure that classifies a word, replacing the original's
/// (proc word name?) convention where name? = #t returns the category name.
/// </summary>
public sealed record Classifier<T>(Symbol Name, Func<T, bool> Test);

public static partial class Parser
{
    // =================================================================
    // I. Utilities

    /// <summary>(last-obj-out list) – the list without its last object.</summary>
    public static List<object> LastObjOut(List<object> list) =>
        list.Count > 0 ? list.GetRange(0, list.Count - 1) : throw new SchemeError("cdr: empty list");

    /// <summary>(eow-out string) – takes the last character of a word out, i.e. a period.</summary>
    public static string EowOut(string word) =>
        word.Length > 0 ? word[..^1] : throw new SchemeError("cdr: empty string");

    /// <summary>(char-exist? word char).</summary>
    public static bool CharExist(string word, char c) => word.Contains(c);

    /// <summary>(char-exist-last? word char) – useful for finding end-of-sentence periods.</summary>
    public static bool CharExistLast(string word, char c) =>
        word.Length > 0 ? word[^1] == c : throw new SchemeError("last-pair: empty string");

    /// <summary>(exist-obj-list? obj list).</summary>
    public static bool ExistObjList(object obj, IEnumerable<object> list) => Memq(obj, list);

    // =================================================================
    // B. Information Retrieval – getting data from IQATS data files

    static Symbol ToSymbol(object word) => Symbol.Intern(((string)word).ToLowerInvariant());

    /// <summary>
    /// (get-category-defs category file) – finds the sentence in the data file
    /// whose first word is the category. Words are downcased and turned into
    /// symbols. Returns null (#f) if the category is not in the file.
    /// </summary>
    public static List<object>? GetCategoryDefs(Symbol category, string file)
    {
        var port = DataFiles.OpenPath(file);
        while (true)
        {
            var sentence = port.GetNextSentence().Select(w => (object)ToSymbol(w)).ToList();
            if (sentence.Count > 0 && Eq(category, sentence[0]))
                return sentence; // category definitions returned
            if (!port.CharReady())
                return null;
        }
    }

    /// <summary>(cdr (get-category-defs ...)) – errors like the original when the category is missing.</summary>
    public static List<object> CategoryDefsBody(Symbol category, string file) =>
        Cdr(GetCategoryDefs(category, file) ?? throw new SchemeError($"cdr: category {category} not found in {file}"));

    /// <summary>
    /// (get-all-data file) – used for getting orders in files. Everything should
    /// be stored in data files to make functionality easier for the user, and learning as well.
    /// </summary>
    public static List<Symbol> GetAllData(string file) =>
        DataFiles.OpenPath(file).GetAllSentence().Select(ToSymbol).ToList();

    /// <summary>(fetch-word? file category word).</summary>
    public static bool FetchWord(string file, Symbol category, Symbol word) =>
        Memq(word, CategoryDefsBody(category, file));

    /// <summary>(fetch-word-grammar category word) – grammar.dat dictionary lookup.</summary>
    public static bool FetchWordGrammar(Symbol category, Symbol word) =>
        FetchWord("grammar.dat", category, word);

    // =================================================================
    // II. Identifiers

    /// <summary>(cap-word-ref? word num) – is the num'th (1 based) character upper case?</summary>
    public static bool CapWordRef(string word, int num)
    {
        if (num < 1 || num > word.Length)
            throw new SchemeError($"car: character {num} of \"{word}\" does not exist");
        char c = word[num - 1];
        return c == char.ToUpperInvariant(c);
    }

    /// <summary>(cap-word? word) – analyzes first char.</summary>
    public static bool CapWord(string word) => CapWordRef(word, 1);

    /// <summary>(cap-word-all? word) – checks if all of word is in caps.</summary>
    public static bool CapWordAll(string word)
    {
        for (int i = 1; i <= word.Length; i++)
            if (!CapWordRef(word, i)) return false;
        return true;
    }

    // Scheme number syntax accepted by string->number (decimal, rational, exponent, radix prefixes).
    [GeneratedRegex(@"^(#[ei])?([+-]?(\d+/\d+|(\d+\.?\d*|\.\d+)([esfdl][+-]?\d+)?)|#[bodx][+-]?[0-9a-f]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeNumberRegex();

    /// <summary>(number? string #f) – true when string->number succeeds.</summary>
    public static bool IsNumber(string word) => SchemeNumberRegex().IsMatch(word);

    /// <summary>(proper-noun? word #f) – a capital word and not a number.</summary>
    public static bool IsProperNoun(string word) => CapWord(word) && !IsNumber(word);

    public static readonly Classifier<string> ProperNoun = new(Sym.ProperNoun, IsProperNoun);
    public static readonly Classifier<string> Number = new(Sym.Number, IsNumber);

    /// <summary>
    /// (unknown-word? word) – checks for a word not in the grammar.dat dictionary.
    /// Not used by the engine since conv-sent already filters all listed words.
    /// </summary>
    public static bool UnknownWord(string word)
    {
        var symbol = ToSymbol(word);
        return !GetAllData("grammar.lst").Any(cat => FetchWordGrammar(cat, symbol));
    }

    /// <summary>
    /// (obj-distance a b list) – distance of two objects in a list; negative
    /// if a is after b, which can be used to tell before/after.
    /// </summary>
    public static int ObjDistance(object a, object b, List<object> list) =>
        MemqTail(a, list).Count - MemqTail(b, list).Count;

    // =================================================================
    // V. Conversion
    // A. Mechanics

    /// <summary>(conv-word cat) – categorizes a word string if grammar.dat lists it under cat.</summary>
    public static Func<object, object> ConvWord(Symbol cat) => word =>
        word is string s && FetchWordGrammar(cat, ToSymbol(s)) ? L(cat, L(s)) : word;

    /// <summary>(conv-sent cat sentence).</summary>
    public static List<object> ConvSent(Symbol cat, List<object> sentence) =>
        sentence.Select(ConvWord(cat)).ToList();

    /// <summary>(conv-all sentence) – applies every category in grammar.lst.</summary>
    public static List<object> ConvAll(List<object> sentence)
    {
        foreach (var cat in GetAllData("grammar.lst"))
            sentence = ConvSent(cat, sentence);
        return sentence;
    }

    /// <summary>(conv-word-proc proc) – categorizes with a procedure rather than a data file.</summary>
    public static Func<object, object> ConvWordProc(Classifier<string> proc) => word =>
        word is string s && proc.Test(s) ? L(proc.Name, L(s)) : word;

    /// <summary>(conv-sent-proc proc sentence).</summary>
    public static List<object> ConvSentProc(Classifier<string> proc, List<object> sentence) =>
        sentence.Select(ConvWordProc(proc)).ToList();

    /// <summary>(conv-all-procs sentence) – proper nouns, then numbers.</summary>
    public static List<object> ConvAllProcs(List<object> sentence)
    {
        foreach (var proc in new[] { ProperNoun, Number })
            sentence = ConvSentProc(proc, sentence);
        return sentence;
    }

    /// <summary>(conv-sent-fully sentence) – first-level parsing.</summary>
    public static List<object> ConvSentFully(List<object> sentence) =>
        ConvAllProcs(ConvAll(sentence));

    // =================================================================
    // C. Analytical Conversion

    /// <summary>(get-type obj) – the category of a word object, or unknown for a bare string.</summary>
    public static object GetType(object obj) => obj is List<object> ? Car(obj) : Sym.Unknown;

    /// <summary>(conv-group sentence type start end) – wraps sentence[start..end) into (type ...).</summary>
    public static List<object> ConvGroup(List<object> sentence, Symbol type, int start, int end)
    {
        var list1 = Sublist(sentence, 0, start);
        var list2 = Sublist(sentence, start, end);
        var list3 = Sublist(sentence, end, sentence.Count);
        list2.Insert(0, type);
        return Append(list1, [list2], list3);
    }

    /// <summary>(eq-type? word-obj type-list) – checks if a word object's type exists in type-list.</summary>
    public static bool EqType(object wordObj, List<object> typeList) => Memq(GetType(wordObj), typeList);

    /// <summary>(eq-type-sentence? sentence type-list).</summary>
    public static bool EqTypeSentence(List<object> sentence, List<object> typeList) =>
        sentence.Any(w => EqType(w, typeList));

    /// <summary>
    /// (group-sentence sentence type flexibility require check-type)
    /// Groups runs of word objects whose types are in checkType into (type ...).
    /// The flexibility factor is the number of unknown word objects allowed in a
    /// run; require is how many of the leading checkType entries the run must
    /// contain one of before it is grouped.
    /// Ported literally: flexibility is decremented as unknowns are passed and the
    /// decremented value carries into the recursive call, and get-end's lookahead
    /// lags one word behind after skipping an unknown. Both affect the grouping.
    /// </summary>
    public static List<object> GroupSentence(List<object> sentence, Symbol type, int flexibility, int require, List<object> checkType)
    {
        // get-start: index of the first word object of a type in checkType,
        // or the last index when there is none
        static int GetStart(List<object> sentence, List<object> checkType)
        {
            if (sentence.Count == 0) throw new SchemeError("car: empty sentence in group-sentence");
            int i = 0;
            while (true)
            {
                if (EqType(sentence[i], checkType)) return i;
                if (i == sentence.Count - 1) return i;
                i++;
            }
        }

        // get-end: absolute index one past the run that starts at sentenceEd[0].
        // Flexibility should only apply to unknown word objects.
        int? GetEnd(List<object> sentenceEd, int fullLength)
        {
            int j = 0;                      // new-s is sentenceEd[j..]
            object wordObj = sentenceEd[0];
            while (true)
            {
                if (j >= sentenceEd.Count)
                    return null;
                if (!EqType(wordObj, checkType))
                {
                    if (flexibility == 0 || !Eq(GetType(wordObj), Sym.Unknown))
                        return fullLength - (sentenceEd.Count - j);
                    flexibility--;
                    wordObj = sentenceEd[j]; // (car new-s) before advancing – the lag
                    j++;
                }
                else if (j == sentenceEd.Count - 1)
                {
                    return fullLength + 1 - (sentenceEd.Count - j); // in case obj is last of the list
                }
                else
                {
                    wordObj = sentenceEd[j + 1];
                    j++;
                }
            }
        }

        int sValue = GetStart(sentence, checkType);

        // the last object is wrapped in a list so get-type does NOT return unknown for it in get-end
        sentence = Append(Sublist(sentence, 0, sentence.Count - 1), [LastPair(sentence)]);
        int eValue = GetEnd(Sublist(sentence, sValue, sentence.Count), sentence.Count)
                     ?? throw new SchemeError("sublist: #f is not the correct type (group-sentence)");
        sentence = Append(Sublist(sentence, 0, sentence.Count - 1), [Car(sentence[^1])]);

        // requires one of the first `require` types in checkType before the run is grouped
        if (EqTypeSentence(Sublist(sentence, sValue, eValue), Sublist(checkType, 0, require)))
            sentence = ConvGroup(sentence, type, sValue, eValue);

        eValue = sentence.Count - 1;
        if (sValue == eValue)
            return sentence;
        return Append(Sublist(sentence, 0, sValue + 1),
                      GroupSentence(Sublist(sentence, sValue + 1, sentence.Count), type, flexibility, require, checkType));
    }

    /// <summary>
    /// Applies group-sentence for each category of a list file, taking the
    /// category's flexibility, requirement and identifiers from the definition file.
    /// Shared by group-all-sentence and apply-semantics-sentence.
    /// </summary>
    internal static List<object> GroupAllFrom(List<object> sentence, string listFile, string defsFile)
    {
        foreach (var category in GetAllData(listFile))
        {
            var defs = GetCategoryDefs(category, defsFile)
                       ?? throw new SchemeError($"car: category {category} not found in {defsFile}");
            sentence = GroupSentence(sentence,
                                     (Symbol)Car(defs),                   // category or type
                                     SymbolToNumber(Cadr(defs)),          // flexibility #
                                     SymbolToNumber(Caddr(defs)),         // requirement #
                                     Sublist(defs, 3, defs.Count));       // identifiers
        }
        return sentence;
    }

    static int SymbolToNumber(object symbol) =>
        int.TryParse(((Symbol)symbol).Name, out int n) ? n : throw new SchemeError($"integer-zero?: {symbol} is not a number");

    /// <summary>(group-all-sentence sentence) – second-level parsing using identify.lst / identify.dat.</summary>
    public static List<object> GroupAllSentence(List<object> sentence) =>
        GroupAllFrom(sentence, "identify.lst", "identify.dat");

    /// <summary>(get-string-from-list list) – a deep map collecting all the strings in order.</summary>
    public static List<object> GetStringFromList(object list)
    {
        var result = new List<object>();
        void Walk(object x)
        {
            switch (x)
            {
                case string s: result.Add(s); break;
                case List<object> l: foreach (var item in l) Walk(item); break;
            }
        }
        Walk(list);
        return result;
    }

    /// <summary>
    /// (get-match-sentence category list) – every phrase (at any depth, in order)
    /// whose category is one of the given categories.
    /// </summary>
    public static List<object> GetMatchSentence(List<object> category, List<object> list)
    {
        var result = new List<object>();
        GetInfoSentence(category, list, 0, result);
        return result;
    }

    // (get-info-sentence category list) walking list[start..] like the cdr recursion
    static void GetInfoSentence(List<object> category, List<object> list, int start, List<object> result)
    {
        for (int i = start; i < list.Count; i++)
        {
            var item = list[i];
            if (Memq(item, category)) // proves that category is in the list to test
                result.Add(i == 0 ? list : list.GetRange(i, list.Count - i));
            else if (item is List<object> sub)
                GetInfoSentence(category, sub, 0, result);
        }
    }

    /// <summary>
    /// (get-cats-from-sentence category list) – like get-match-sentence, but when
    /// the list itself starts with a matching category that symbol is returned first.
    /// </summary>
    public static List<object> GetCatsFromSentence(List<object> category, List<object> list)
    {
        var result = new List<object>();
        if (list.Count == 0) return result;
        if (Memq(list[0], category))
            result.Add(list[0]);
        else if (list[0] is List<object> sub)
            GetInfoSentence(category, sub, 0, result);
        GetInfoSentence(category, list, 1, result);
        return result;
    }

    /// <summary>(classify-unknown list) – remaining bare strings become (unknown ("word")).</summary>
    public static List<object> ClassifyUnknown(List<object> list) =>
        list.Select(x => x is string s ? L(Sym.Unknown, L(s)) : x).ToList();

    /// <summary>(get-object-sentence category sentence) – first phrase of that category, or null.</summary>
    public static object? GetObjectSentence(Symbol category, List<object> sentence) =>
        sentence.FirstOrDefault(p => Eq(Car(p), category));
}
