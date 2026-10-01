# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

QGen is a C# (.NET 10 console app) port of IQATS ("Intelligent Question and Answer Test Summarizer", Sam Hsiung, 1998), originally written in MIT Scheme 7.4.2 (`parser.scm`, `manip.scm`, `talk.scm`, `startup.scm`). It parses English sentences into phrase trees and generates question/answer pairs from them using rule tables in `Data/`. It can also learn new question patterns by appending to those tables.

## Commands

```sh
dotnet build                                      # build (QGen.slnx / QGen.csproj)
dotnet run                                        # interactive chat client (port of talk.scm)
dotnet run -- --startup <textfile>                # parse the first 5 sentences of a file and generate questions (port of startup.scm)
dotnet run -- --data Data                         # use the repo's Data/ directly instead of the copy in bin/
```

There are no tests or lint configuration in the repo.

**Data directory gotcha:** by default the program reads `Data/` next to the executable (`bin/.../Data`), which the build copies with `PreserveNewest`. Learning and logging write to that copy, so the source `Data/` stays unchanged and a learned (newer) copy in `bin/` is not overwritten by the build. Passing `--data Data` makes the program write to the tracked files in the repo.

## Port fidelity

The code is a deliberately literal port. Each C# method's doc comment names the Scheme procedure it replaces (e.g. `(group-sentence ...)`). Several odd behaviours are intentional and change the output. Don't "fix" them unless asked:
- `Parser.GroupSentence`: flexibility is decremented and carried into the recursive call, and `GetEnd`'s lookahead lags one word behind after it skips an unknown.
- `SentenceReader.EndOfSentence` consumes a character when the next char isn't a space or newline. `GetAll` consumes one char before each sentence.
- `Talk.PromptAndLearn` asks for the Question before the Answer, matching the bottom-up `let` evaluation in the REPL.
- `SchemeError` is thrown wherever the Scheme code would have signalled an error. Its messages imitate the Scheme ones (`car: ...`, `sublist: ...`). The talk loop catches it and prints `;message`.

### Scheme data model (`Sexp.cs`)
- Symbols are interned `Symbol` objects, so `eq?` is reference equality (`Sexp.Eq`). Words from data files are lowercased and interned (`Parser.ToSymbol`).
- Lists are `List<object>`. `null` and an empty list both mean `'()`/`#f`, as in MIT Scheme 7.4. Use `Sexp.Truthy`/`Eq`/`IsEqual`, not `==`.
- Sentence words are `string`. A parsed leaf is `(category ("Word"))` and a phrase is `(category leaf leaf ...)`. A parsed sentence ends with the marker `(eos ("."))`.
- `Sexp.Write` / `Sexp.Display` print values the way Scheme does. `iqats.log` and the learned entries use this format.

## Pipeline

`Manip.Parse` → `Manip.ApplySemanticsSentence` → `Manip.SentenceToQuestion`:

1. **Read**: `SentenceReader` / `Manip.StringToSentence` split text into word strings.
2. **Level 1, word categories** (`Parser.ConvSentFully`): for each category in `grammar.lst`, words listed under it in `grammar.dat` become `(cat ("w"))`. Then the procedural classifiers run: proper noun (capitalised and not a number), then number. Remaining strings become `unknown` (`ClassifyUnknown`).
3. **Level 2, phrase grouping** (`Parser.GroupAllSentence`): for each category in `identify.lst`, `identify.dat` gives `name flexibility require identifiers...`. `GroupSentence` wraps runs of matching types, allowing up to `flexibility` unknowns. A run must contain one of the first `require` identifiers.
4. **Semantics** (`ApplySemanticsSentence`): the `Person` classifier runs first (a noun phrase of two or more proper nouns with no determiner). Then the same grouping runs with `semantic.lst` / `semantic.dat` (object, object-minor, time).
5. **Question generation**: for each type in `manip.lst`, the sentence matches when its top-level phrase markers (`CatListingBasic`) equal the type's `manip.dat` entry. The question is built from:
   - phrases in `manip.ord` order;
   - intro words prepended from `apply.dat` (case kept);
   - a `?` appended;
   - past tense changed to present when the type's `question.dat` flag is `1`.

   The answer is the phrases listed in `answer.dat`, with the first letter capitalised.

**Learning** (`Manip.LearnQuestionAnswer`) adds a new type. Its name comes from the first two words of the question, plus random digits until it is unique. The new type is appended to `apply.dat`, `question.dat`, `answer.dat`, `manip.dat` and `manip.ord`. For `manip.lst`, the final `.\n` is removed first and re-added after the new name. Every processed sentence is appended to `iqats.log`.

## Data file format

- `.dat` files hold entries separated by blank lines. Each entry is a "sentence" that ends with a period. The first word is the key, and the rest are values separated by whitespace or newlines. `GetCategoryDefs` finds the first entry whose first word matches the key.
- `.lst` files are one period-terminated sentence that gives the processing order.
- Data files are re-read from disk on every lookup (no caching), so edits take effect immediately.
- Writes use CRLF line endings (`DataFiles.FileNewLine`). Reads normalise them to LF.
