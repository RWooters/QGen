// IQATS (C# port)
// Talk.cs
// Port of talk.scm (the chatting client) and startup.scm (the test harness).

using System.Diagnostics;
using System.Text;
using static QGen.Manip;
using static QGen.Sexp;

namespace QGen;

public sealed class Talk(TextReader input, TextWriter output)
{
    const string Header1 = "IQATS- Intelligent Question and Answer Test Summerizer v0.8 alpha release\nContact author at sam@gundam.res.cmu.edu\n(c)1998 Sam Hsiung\n";
    const string Header2 = "To test IQATS enter a sentence, enclosed in quotes, without any periods.\nor end of sentence markers. Results are stored in iqats.log";
    const string Header3 = "Please input without any punctuation marks, question marks etc. and in quotes.";
    const string Example = "\"Chemistry is the study of the composition of substances\"";

    /// <summary>Signals that the input stream ended while a prompt was waiting.</summary>
    sealed class EndOfInput : Exception;

    public void Run()
    {
        output.WriteLine();
        output.Write(Header1);
        output.WriteLine();
        output.WriteLine();
        output.Write(Header2);
        output.WriteLine();
        output.WriteLine();
        output.Write(Example);
        try
        {
            TalkLoop();
        }
        catch (EndOfInput)
        {
            output.WriteLine();
        }
    }

    /// <summary>(analyze-convert sentence) – displays the first two parsing levels of a sentence.</summary>
    public void AnalyzeConvert(List<object> sentence)
    {
        sentence = Parser.GroupAllSentence(Parser.ClassifyUnknown(Parser.ConvSentFully(sentence)));
        output.WriteLine();
        output.Write(Display(sentence));
    }

    // (talk-loop)
    void TalkLoop()
    {
        while (true)
        {
            try
            {
                if (!ProcessSentence())
                    continue; // the original re-enters talk-loop when a duplicate warning is declined
            }
            catch (SchemeError e)
            {
                output.WriteLine();
                output.WriteLine($";{e.Message}");
            }

            if (!PromptYes("Enter another sentence? (y/n)"))
            {
                output.Write("Bye.");
                output.WriteLine();
                return;
            }
        }
    }

    /// <returns>false when the user chose not to continue with this sentence.</returns>
    bool ProcessSentence()
    {
        var sentence = StringToSentence(PromptString("Enter sentence in quotes"));
        sentence = Parse(sentence);
        output.Write(Display(sentence));
        output.WriteLine();
        sentence = ApplySemanticsSentence(sentence);
        output.Write(Display(sentence));
        output.WriteLine();

        if (DuplicateListing(CatListingBasic(sentence)))
        {
            if (!PromptYes("Warning: duplicate type found, IQATS may not be able to process this sentence\n correctly, ability to process this sentence will be included by the beta release. Continue? (y/n)"))
                return false;
        }
        else
        {
            output.WriteLine();
        }

        var question = SentenceToQuestion(sentence);
        if (question.Count == 0)
        {
            LearnInput(sentence);
        }
        else
        {
            DisplayQuestion(question, output);
            AskLearn(sentence);
        }

        output.WriteLine();
        output.Write("saving....");
        DataFiles.Append("iqats.log", "\n" + Write(sentence) + "\n" + Write(question) + "\n");
        return true;
    }

    // (learn-input sentence)
    void LearnInput(List<object> sentence)
    {
        output.Write(Header3);
        output.Write("IQATS cannot pattern-match the sentence just inputed.");
        output.Write("\nIf possible, to help IQATS learn, type in the appropriate question and answer in quotes,\n without the period or question mark.");
        PromptAndLearn(sentence);
    }

    // (ask-learn sentence)
    void AskLearn(List<object> sentence)
    {
        bool learn = PromptYes("Would you like IQATS to learn to generate other Q&A's?");
        output.Write(Header3);
        if (learn)
            PromptAndLearn(sentence);
    }

    void PromptAndLearn(List<object> sentence)
    {
        // the Scheme REPL evaluated the let bindings bottom-up, so Question is asked first
        var questionLearn = StringToSentence(PromptString("Question?"));
        var answerLearn = StringToSentence(PromptString("Answer?"));
        LearnQuestionAnswer(sentence, questionLearn, answerLearn, output);
    }

    // -----------------------------------------------------------------
    // prompt-for-expression

    string Prompt(string prompt)
    {
        output.Write($"\n{prompt}: ");
        output.Flush();
        return input.ReadLine() ?? throw new EndOfInput();
    }

    /// <summary>Reads a string datum. Surrounding quotes are optional here.</summary>
    string PromptString(string prompt)
    {
        var text = Prompt(prompt).Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            var sb = new StringBuilder();
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length - 1) i++;
                sb.Append(text[i]);
            }
            text = sb.ToString();
        }
        return text;
    }

    /// <summary>(eq? (prompt-for-expression ...) 'y) – symbols are case-folded in MIT Scheme.</summary>
    bool PromptYes(string prompt) =>
        PromptString(prompt).Equals("y", StringComparison.OrdinalIgnoreCase);

    // -----------------------------------------------------------------
    // startup.scm

    /// <summary>
    /// Port of startup.scm: reads the first five sentences of a text file,
    /// fully parses them and generates their questions.
    /// </summary>
    public void Startup(string textFile)
    {
        var start = Stopwatch.StartNew();
        output.Write("Loading IQATS- Intelligent Question-Answer Text Summerizer...... Time: ");
        output.Write(start.Elapsed.TotalSeconds);
        output.WriteLine();
        output.Write("Binaries finished loading, creating data structures for testing (this may take a while)..... Time: ");
        output.Write(start.Elapsed.TotalSeconds);

        var all = DataFiles.OpenPath(textFile).GetAllSentencePeriod();
        var port = DataFiles.OpenPath(textFile);
        var sentences = new List<List<object>>();
        for (int i = 0; i < 5; i++)
        {
            var sentence = port.GetNextSentencePeriod();
            if (sentence.Count == 1) break; // only the eos marker: no sentences left
            sentences.Add(sentence);
        }

        output.WriteLine();
        output.Write($"Parsing {string.Join(" ", Enumerable.Range(1, sentences.Count).Select(i => $"sent-{i}"))}....");
        var parsed = new List<List<object>>();
        foreach (var s in sentences)
        {
            try { parsed.Add(ParseAll(s)); }
            catch (SchemeError e) { parsed.Add([]); output.Write($"\n;{e.Message}"); }
        }
        output.Write(start.Elapsed.TotalSeconds);
        output.WriteLine();
        output.Write("Generating Questions...");
        output.WriteLine();
        for (int i = 0; i < parsed.Count; i++)
        {
            output.WriteLine($"sent-{i + 1}: {Write(parsed[i])}");
            try { output.WriteLine($"?-{i + 1}: {Write(parsed[i].Count == 0 ? [] : SentenceToQuestion(parsed[i]))}"); }
            catch (SchemeError e) { output.WriteLine($"?-{i + 1}: ;{e.Message}"); }
        }
        output.Write(start.Elapsed.TotalSeconds);
        output.WriteLine();
        output.Write($"Finished.... ({all.Count} words read in total)");
        output.WriteLine();
    }
}
