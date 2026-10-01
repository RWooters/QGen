// IQATS (C# port)
// SentenceReader.cs
// Port of the input-port / sentence reading half of parser.scm.
// The original reads through MIT Scheme's current input port; here each
// reader instance plays the role of a freshly opened port (open-path).

namespace QGen;

public sealed class SentenceReader
{
    const int Eof = -1;

    readonly string _text;
    int _pos;

    /// <param name="text">Port contents. CRLF is normalised to LF, as the Windows text-mode port did.</param>
    public SentenceReader(string text) => _text = text.Replace("\r\n", "\n");

    public int PeekChar() => _pos < _text.Length ? _text[_pos] : Eof;

    public int ReadChar() => _pos < _text.Length ? _text[_pos++] : Eof;

    /// <summary>(char-ready?) – false once the port is exhausted.</summary>
    public bool CharReady() => _pos < _text.Length;

    public bool AtEof => _pos >= _text.Length;

    // ---------------------------------------------------------------
    // word level predicates

    /// <summary>Checks for initials like G. Helmut so they are not confused with end of sentence.</summary>
    public static bool IsInitial(string word) =>
        word.Length == 2 && Parser.CharExistLast(word, '.');

    /// <summary>
    /// (end-of-sentence? word). Note the original consumes a character when the
    /// next one is neither a space nor a linefeed (the Tab test uses read-char).
    /// </summary>
    public bool EndOfSentence(string word)
    {
        if (word.Length == 0) return false;
        return (Parser.CharExistLast(word, '.') || Parser.CharExistLast(word, '!') || Parser.CharExistLast(word, '?'))
               && !IsInitial(word)
               && (PeekChar() == ' ' || PeekChar() == '\n' || ReadChar() == '\t');
    }

    /// <summary>(skip-char?) – word separators.</summary>
    public bool SkipChar()
    {
        int c = PeekChar();
        return c is ' ' or '\n' or '\t' or ',' or '"' or '(' or ')';
    }

    // ---------------------------------------------------------------
    // sentence level readers

    /// <summary>(get-word-symbol) – reads the next word as a string, case preserved.</summary>
    public string GetWordSymbol()
    {
        while (SkipChar()) ReadChar();
        if (AtEof) return ""; // original signals an error reading past eof

        var word = new System.Text.StringBuilder();
        word.Append((char)ReadChar());
        while (CharReady() && !SkipChar())
            word.Append((char)ReadChar());
        return word.ToString();
    }

    /// <summary>(get-next-sentence) – list of word strings, end of sentence mark removed.</summary>
    public List<object> GetNextSentence()
    {
        var sentence = new List<object> { GetWordSymbol() };
        if (AtEof && (string)sentence[0] == "") return [];

        while (true)
        {
            if (EndOfSentence((string)sentence[^1]))
                return EosOut(sentence);

            if (AtEof)
            {
                // The original would read past eof here; finish the sentence instead.
                if ((string)sentence[^1] == "") sentence.RemoveAt(sentence.Count - 1);
                if (sentence.Count > 0 && sentence[^1] is string last && last.Length > 0 && ".!?".Contains(last[^1]) && !IsInitial(last))
                    return EosOut(sentence);
                return sentence;
            }

            sentence.Add(GetWordSymbol());
        }
    }

    /// <summary>(get-next-sentence-period) – sentence followed by the (eos (".")) marker.</summary>
    public List<object> GetNextSentencePeriod()
    {
        var sentence = GetNextSentence();
        sentence.Add(Sexp.L(Sym.Eos, Sexp.L(".")));
        return sentence;
    }

    /// <summary>(get-all-sentence) – every sentence in the port appended into one list.</summary>
    public List<object> GetAllSentence() => GetAll(GetNextSentence);

    /// <summary>(get-all-sentence-period).</summary>
    public List<object> GetAllSentencePeriod() => GetAll(() =>
    {
        var s = GetNextSentence();
        if (s.Count > 0) s.Add(Sexp.L(Sym.Eos, Sexp.L(".")));
        return s;
    });

    List<object> GetAll(Func<List<object>> next)
    {
        var all = new List<object>();
        // like the original, one character is consumed before each sentence
        while (ReadChar() != Eof && PeekChar() != Eof)
            all.AddRange(next());
        return all;
    }

    /// <summary>(eos-out sentence) – takes the last character of the last word out.</summary>
    static List<object> EosOut(List<object> sentence)
    {
        var last = (string)sentence[^1];
        sentence[^1] = Parser.EowOut(last);
        return sentence;
    }
}
