// IQATS (C# port)
// Manip.cs
// Port of manip.scm (originally coded by Sam Hsiung for MIT Scheme 7.4.2).
// Builds questions and answers from parsed sentences, and learns new
// question patterns by writing them to the data files.

using static QGen.Parser;
using static QGen.Sexp;

namespace QGen;

public static class Manip
{
    // =================================================================
    // Question generation

    /// <summary>(sentence-for-question list type) – phrase markers and strings matching the question type.</summary>
    public static List<object> SentenceForQuestion(List<object> list, Symbol type) =>
        GetMatchSentence(CategoryDefsBody(type, "manip.dat"), list);

    /// <summary>(arrange-sentence list type) – orders the sentence's phrases according to manip.ord.</summary>
    public static List<object> ArrangeSentence(List<object> list, Symbol type)
    {
        var arranged = new List<object>();
        foreach (var order in CategoryDefsBody(type, "manip.ord"))
            arranged.Add(Car(GetMatchSentence(L(order), list)));
        return arranged;
    }

    /// <summary>(get-category-string-defs category file) – like get-category-defs but case is kept, as strings.</summary>
    public static List<object>? GetCategoryStringDefs(Symbol category, string file)
    {
        var port = DataFiles.OpenPath(file);
        while (true)
        {
            var sentence = port.GetNextSentence();
            if (sentence.Count > 0 && IsEqual(category.Name, sentence[0]))
                return sentence;
            if (!port.CharReady())
                return null;
        }
    }

    /// <summary>(detail-sentence sentence type) – adds introductory words from apply.dat and the "?".</summary>
    public static List<object> DetailSentence(List<object> sentence, Symbol type)
    {
        var intro = Cdr(GetCategoryStringDefs(type, "apply.dat")
                        ?? throw new SchemeError($"cdr: category {type} not found in apply.dat"));
        return Append(intro, [Append(sentence, ["?"])]);
    }

    /// <summary>(change-tense? type) – question.dat flag.</summary>
    public static bool ChangeTense(Symbol type) =>
        ((Symbol)Car(CategoryDefsBody(type, "question.dat"))).Name == "1";

    /// <summary>
    /// (make-? list type) – the question as a list of strings. Grammar is applied
    /// after the sentence is converted into a list of strings.
    /// </summary>
    public static List<object> MakeQuestion(List<object> list, Symbol type) =>
        ChangeTense(type)
            ? GetStringFromList(DetailSentence(ApplyGrammar(ArrangeSentence(list, type)), type))
            : GetStringFromList(DetailSentence(ArrangeSentence(list, type), type));

    // =================================================================
    // Grammar

    /// <summary>(last-chars string num) – the last num letters of a word.</summary>
    public static string LastChars(string word, int num) => word[(word.Length - num)..];

    /// <summary>
    /// (past-tense? string) – only procedural; exceptions such as 'feed'
    /// should be in the data files.
    /// </summary>
    public static bool PastTense(string word) => word.Length >= 2 && LastChars(word, 2) == "ed";

    /// <summary>(double-constanant-root-ending? s): "happened" -> false, "occurred" -> true.</summary>
    public static bool DoubleConsonantRootEnding(string word) => LastChars(word, 4)[0] == LastChars(word, 3)[0];

    /// <summary>(past->present string) – returns the word unchanged if it doesn't recognize the verb.</summary>
    public static string PastToPresent(string word)
    {
        if (!(PastTense(word) && word.Length > 3)) // if the word is < 4 the values below would sink below 0
            return word;
        if (word.Length == 4) return word[..^1];
        if (DoubleConsonantRootEnding(word)) return word[..^3];
        return word[..^2];
    }

    /// <summary>
    /// (past->present-sent sentence) – works on a list of phrases and already
    /// extracts the strings; only the first word of a verb phrase is kept.
    /// </summary>
    public static List<object> PastToPresentSent(List<object> sentence) =>
        ConnectList(sentence.Select(word => ParsedVerb(word)
            ? L(PastToPresent((string)Car(GetStringFromList(word))))
            : GetStringFromList(word)));

    /// <summary>(connect-list list) – converts a list of lists into a list of their objects.</summary>
    public static List<object> ConnectList(IEnumerable<List<object>> lists) => lists.SelectMany(l => l).ToList();

    /// <summary>(symbol-length symbol).</summary>
    public static int SymbolLength(Symbol symbol) => symbol.Name.Length;

    /// <summary>(parsed-verb? word) – is the parsed phrase-leaf a verb?</summary>
    public static bool ParsedVerb(object word)
    {
        var name = ((Symbol)Car(word)).Name;
        return name.Length > 3 && name[..4] == "verb";
    }

    /// <summary>(apply-grammar sentence).</summary>
    public static List<object> ApplyGrammar(List<object> sentence) => PastToPresentSent(sentence);

    // =================================================================
    // Semantics

    /// <summary>
    /// (apply-semantics-sentence sentence) – this does not suggest that heavy
    /// 'semantic' parsing is done; objects, persons, time etc. is all that is needed.
    /// Same structure as group-all-sentence, using semantic.lst / semantic.dat.
    /// </summary>
    public static List<object> ApplySemanticsSentence(List<object> sentence) =>
        GroupAllFrom(ConvPhraseTreeProcsAll(sentence), "semantic.lst", "semantic.dat");

    /// <summary>(last-pair-out list).</summary>
    public static List<object> LastPairOut(List<object> list) =>
        list.Count > 0 ? Sublist(list, 0, list.Count - 1) : throw new SchemeError("sublist: empty list");

    /// <summary>(cat-listing-basic sentence) – top level phrase markers, eos marker taken out.</summary>
    public static List<object> CatListingBasic(List<object> sentence) =>
        LastPairOut(sentence).Select(Car).ToList();

    /// <summary>
    /// (cat-listing-full sentence) – lists linearly all the phrase markers in a
    /// phrase-tree. Like the original, the tree's last object is left out first.
    /// </summary>
    public static List<object> CatListingFull(List<object> sentence)
    {
        var cats = new List<object>();
        void Walk(object x)
        {
            switch (x)
            {
                case Symbol s: cats.Add(s); break;
                case List<object> l: foreach (var item in l) Walk(item); break;
            }
        }
        Walk(LastPairOut(sentence));
        return cats;
    }

    /// <summary>(parse-all sentence) – includes apply-semantics, which is not part of 'parse'.</summary>
    public static List<object> ParseAll(List<object> sentence) =>
        ApplySemanticsSentence(Parse(sentence));

    /// <summary>(parse sentence).</summary>
    public static List<object> Parse(List<object> sentence) =>
        GroupAllSentence(ClassifyUnknown(ConvSentFully(sentence)));

    /// <summary>(pattern-match? sentence type) – compares phrase markers of the sentence with manip.dat.</summary>
    public static bool PatternMatch(List<object> sentence, Symbol type) =>
        IsEqual(CatListingBasic(sentence), CategoryDefsBody(type, "manip.dat"));

    /// <summary>(sentence->question sentence) – a list of questions and their answers.</summary>
    public static List<object> SentenceToQuestion(List<object> sentence)
    {
        var question = new List<object>();
        var types = DataFiles.OpenPath("manip.lst").GetAllSentence().Select(w => Symbol.Intern((string)w));
        foreach (var type in types)
        {
            if (!PatternMatch(sentence, type)) continue;
            question.Add(MakeQuestion(sentence, type));
            question.Add(MakeAnswer(sentence, type)); // answer included
        }
        return question;
    }

    /// <summary>(display-? question) – one question or answer per line.</summary>
    public static void DisplayQuestion(List<object> question, TextWriter output)
    {
        foreach (var line in question)
            output.WriteLine(SentenceToString((List<object>)line));
    }

    /// <summary>
    /// (person? phrase-word #f) – a noun phrase without a determiner made up only
    /// of two or more proper nouns, e.g. Andrew Jackson.
    /// </summary>
    public static bool IsPerson(List<object> phraseWord)
    {
        var phrases = CatListingFull(phraseWord);
        int properNounNum = GetMatchSentence(L(Sym.ProperNoun), phraseWord).Count;
        return !ExistObjList(Sym.Determiner, phrases)
               && Eq(Car(phraseWord), Sym.NounPhrase)
               && properNounNum > 1
               && properNounNum == GetStringFromList(phraseWord).Count;
    }

    public static readonly Classifier<List<object>> Person = new(Sym.Person, IsPerson);

    /// <summary>(conv-phrase-tree-procs proc sentence) – classifies already parsed phrases.</summary>
    public static List<object> ConvPhraseTreeProcs(Classifier<List<object>> proc, List<object> sentence) =>
        sentence.Select(word => proc.Test((List<object>)word) ? L(proc.Name, word) : word).ToList();

    /// <summary>(conv-phrase-tree-procs-all sentence).</summary>
    public static List<object> ConvPhraseTreeProcsAll(List<object> sentence)
    {
        foreach (var proc in new[] { Person })
            sentence = ConvPhraseTreeProcs(proc, sentence);
        return sentence;
    }

    /// <summary>(make-answer sentence type) – answer.dat phrases, first word capitalized.</summary>
    public static List<object> MakeAnswer(List<object> sentence, Symbol type) =>
        UpcaseSentence(GetStringFromList(GetMatchSentence(CategoryDefsBody(type, "answer.dat"), sentence)));

    /// <summary>(upcase-word word) – upcases first letter.</summary>
    public static string UpcaseWord(string word) =>
        word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : throw new SchemeError("car: empty word");

    /// <summary>(upcase-sentence sentence) – upcases first letter of first word.</summary>
    public static List<object> UpcaseSentence(List<object> sentence) =>
        Append([UpcaseWord((string)Car(sentence))], Cdr(sentence));

    /// <summary>(exist-cat? cat sentence).</summary>
    public static bool ExistCat(Symbol cat, List<object> sentence) =>
        GetMatchSentence(L(cat), sentence).Count > 0;

    // =================================================================
    // Learning

    /// <summary>(learn-question-dat parsed-sentence parsed-question name).</summary>
    public static void LearnQuestionDat(List<object> parsedSentence, List<object> parsedQuestion, string name)
    {
        const string file = "question.dat";
        DataFiles.WriteType(name, file);
        DataFiles.WriteDef(NeedChangeTense(parsedSentence, parsedQuestion) ? 1 : 0, file);
        DataFiles.WriteDef(Sym.Null, file);
        DataFiles.WritePeriod(file);
    }

    /// <summary>(learn-cat-listing-data-file parsed name file).</summary>
    public static void LearnCatListingDataFile(List<object> parsed, string name, string file)
    {
        var catListing = CatListingBasic(parsed);
        DataFiles.WriteType(name, file);
        WriteListDefs(catListing, file);
        DataFiles.WritePeriod(file);
    }

    /// <summary>
    /// (learn-question-answer parsed-sentence question answer) – question and
    /// answer are unparsed sentences (from string->sentence).
    /// </summary>
    public static void LearnQuestionAnswer(List<object> parsedSentence, List<object> question, List<object> answer, TextWriter output)
    {
        var name = LearnMakeType(LastPairOut(question)); // take period out
        output.Write("Learning initiated....");
        LearnManipLst(name);
        output.Write("manip.lst....");
        question = LearnApplyDat(LastPairOut(question), name); // question w/o question-words
        output.Write("apply.dat...parsing question...");
        question = ParseAll(Append(question, [L(Sym.Eos, L("."))]));
        LearnQuestionDat(parsedSentence, question, name);
        output.Write("question.dat....parsing answer....");
        answer = ParseAll(answer);
        LearnCatListingDataFile(answer, name, "answer.dat");
        output.Write("answer.dat....");
        LearnCatListingDataFile(parsedSentence, name, "manip.dat");
        output.Write("manip.dat....");
        LearnCatListingDataFile(question, name, "manip.ord");
        output.Write("manip.ord.... Learning successful");
    }

    /// <summary>(write-list-defs list-defs file).</summary>
    public static void WriteListDefs(List<object> listDefs, string file)
    {
        foreach (var def in listDefs)
            DataFiles.WriteDef(def, file);
    }

    /// <summary>
    /// (need-change-tense? parsed-sentence parsed-question) – is the sentence
    /// past tense while the question isn't? Checking that a parsed verb exists in
    /// the sentence must be done before running this function.
    /// </summary>
    public static bool NeedChangeTense(List<object> parsedSentence, List<object> parsedQuestion)
    {
        if (!PastTense((string)Car(GetStringFromList(parsedSentence.Where(ParsedVerb).ToList()))))
            return false;
        var questionVerbs = parsedQuestion.Where(ParsedVerb).ToList();
        var verbWords = questionVerbs.Count == 0
            ? GetMatchSentence(L(Sym.Unknown), parsedQuestion) // can't recognize verbs -> unknowns will do
            : questionVerbs;
        return !PastTense((string)Car(GetStringFromList(verbWords)));
    }

    static readonly List<object> W5H1 =
        [.. new[] { "who", "what", "when", "where", "why", "how", "does", "did" }.Select(Symbol.Intern)];

    /// <summary>(question-word? word) – the 4 w's and the 1 h and 'did'!</summary>
    public static bool QuestionWord(object word) =>
        ExistObjList(Symbol.Intern(((string)word).ToLowerInvariant()), W5H1);

    /// <summary>
    /// (learn-apply-dat sentence name) – writes the question words to apply.dat
    /// and returns the sentence without them.
    /// </summary>
    public static List<object> LearnApplyDat(List<object> sentence, string name)
    {
        const string file = "apply.dat";
        var questionWords = sentence.Where(QuestionWord).ToList();
        sentence = sentence.Where(w => !QuestionWord(w)).ToList();
        DataFiles.WriteType(name, file);
        DataFiles.WriteDef(Symbol.Intern(SentenceToString(questionWords)), file);
        DataFiles.WritePeriod(file);
        return sentence;
    }

    /// <summary>
    /// (learn-make-type sentence) – generates a name for new data file entries
    /// from the first two words of the question, adding random digits until it is unique.
    /// </summary>
    public static string LearnMakeType(List<object> sentence)
    {
        var questionWords = Sublist(sentence, 0, 2);
        while (true)
        {
            var name = SentenceToStringCustom(questionWords, "-").ToLowerInvariant();
            var existing = DataFiles.OpenPath("manip.lst").GetAllSentence();
            if (!existing.Any(n => IsEqual(n, name)))
                return name;
            questionWords = Append(questionWords, [Random.Shared.Next(10).ToString()]);
        }
    }

    /// <summary>(learn-manip-lst name) – appends the new type to manip.lst before its final period.</summary>
    public static void LearnManipLst(string name)
    {
        const string file = "manip.lst";
        DataFiles.DeleteLastChars(file, 2);
        DataFiles.WriteType(name, file);
        DataFiles.WritePeriod(file);
    }

    // =================================================================
    // Strings

    /// <summary>(sentence->string sentence).</summary>
    public static string SentenceToString(List<object> sentence) => SentenceToStringCustom(sentence, " ");

    /// <summary>(sentence->string-custom sentence custom).</summary>
    public static string SentenceToStringCustom(List<object> sentence, string custom) =>
        sentence.Count > 0 ? string.Join(custom, sentence.Cast<string>()) : throw new SchemeError("car: empty sentence");

    /// <summary>
    /// (string->sentence string) – reads a string the same way sentences are read
    /// from files. The original round-trips through the file c:\iqats\temp;
    /// the identical text is read here from memory.
    /// </summary>
    public static List<object> StringToSentence(string text) =>
        new SentenceReader($"\n{text}.\n\n{text}.\n").GetNextSentencePeriod();

    /// <summary>(duplicate-listing? listing) – does any phrase marker appear more than once?</summary>
    public static bool DuplicateListing(List<object> listing) =>
        listing.Any(type => listing.Count(t => Eq(t, type)) > 1);
}
