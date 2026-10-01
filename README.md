# QGen – IQATS for .NET

QGen is a C# port of **IQATS** (Intelligent Question and Answer Test Summarizer), written by Sam Hsiung in 1998 for MIT Scheme 7.4.2. It reads English sentences, parses them into phrase trees, and generates question/answer pairs from them. When it can't handle a sentence, you can teach it the matching question and answer, and it saves the new pattern for later sentences.

```
Input:     The Civil War occurred from 1861 to 1865
Question:  When did The Civil War occur ?
Answer:    From 1861 to 1865
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Building and running

```sh
dotnet build
dotnet run                                  # interactive session
dotnet run -- --startup <textfile>          # batch mode
dotnet run -- --data <dir>                  # use a different data directory
```

| Option | Description |
| --- | --- |
| `--data <dir>` | Directory holding the IQATS data files. Defaults to `Data/` next to the executable. |
| `--startup <file>` | Reads the first five sentences of a text file, fully parses them and prints the generated questions, with timings (port of `startup.scm`). |

With no `--startup`, QGen runs the interactive client (port of `talk.scm`).

## Interactive session

1. Enter a sentence without the final period. The quotes around it are optional.
2. QGen prints the parsed sentence, then the sentence after semantic grouping.
3. If the sentence matches a known pattern, QGen prints the generated questions and answers. It then asks whether you want to teach it another question for this sentence.
4. If nothing matches, QGen asks you for a suitable question and answer and learns the pattern from them.
5. Every sentence and its generated questions are appended to `iqats.log`.

Example parse of *"The Civil War occurred from 1861 to 1865"*:

```scheme
((object (noun-phrase (determiner ("The")) (proper-noun ("Civil")) (proper-noun ("War"))))
 (verb-time-phrase (verb-time ("occurred")))
 (time (preposition-time-phrase (preposition-proximity ("from")) (number ("1861"))
                                (preposition-helper ("to")) (number ("1865"))))
 (eos (".")))
```

## How it works

| Stage | What happens | Data files |
| --- | --- | --- |
| 1. Word categories | Dictionary words get grammatical categories. Then capitalised words are tagged as proper nouns and numerals as numbers. Remaining words become `unknown`. | `grammar.lst`, `grammar.dat` |
| 2. Phrase grouping | Runs of related words are grouped into phrases (noun phrase, time phrase, …). A small number of unknown words is allowed inside a run. | `identify.lst`, `identify.dat` |
| 3. Semantics | Detects people (two or more proper nouns with no determiner). Then groups phrases into `object`, `object-minor` and `time`. | `semantic.lst`, `semantic.dat` |
| 4. Questions | The sentence's top-level phrase pattern is compared against each question type. For each match, QGen reorders the phrases, adds question words, adjusts verb tense, and extracts the answer. | `manip.lst`, `manip.dat`, `manip.ord`, `apply.dat`, `question.dat`, `answer.dat` |

## Data files

All behaviour comes from the plain-text rule tables in `Data/`, so you can extend QGen by editing them, with no code changes:

- **`.lst` files** give the processing order: a single list of names ending in a period.
- **`.dat` files** hold entries separated by blank lines. Each entry starts with a key and ends with a period. For example, the `grammar.dat` entry below lists the words in the `determiner` category:

  ```
  determiner

  an the a every some.
  ```

QGen re-reads the data files on every lookup, so edits take effect immediately.

### Learning

Learning appends a new question type to `manip.lst` and adds matching entries to `apply.dat`, `question.dat`, `answer.dat`, `manip.dat` and `manip.ord`.

By default the program reads and writes the copy of `Data/` in the build output (`bin/…/Data`). The source `Data/` folder therefore stays untouched, and the build won't overwrite a newer learned copy. To make learned patterns update the files in the repository, run with `--data Data`.

## Project layout

| File | Contents |
| --- | --- |
| `Program.cs` | Command-line entry point |
| `Talk.cs` | Interactive client and batch mode (`talk.scm`, `startup.scm`) |
| `Parser.cs` | Word classification and phrase grouping (`parser.scm`) |
| `Manip.cs` | Semantics, question/answer generation and learning (`manip.scm`) |
| `SentenceReader.cs` | Splits text into words and sentences |
| `DataFiles.cs` | Data file access |
| `Sexp.cs` | Minimal Scheme data model (symbols, lists, `eq?`, `write`/`display`) |

The port stays deliberately close to the original Scheme code, quirks included, so that its output matches the original. Each C# method's doc comment names the Scheme procedure it replaces.

## Limitations

These limitations come from the original v0.8 alpha:

- Sentences in which the same phrase type appears more than once at the top level trigger a duplicate-type warning and may not be processed correctly.
- Tense conversion is purely rule-based (`-ed` endings), so irregular verbs are not handled.
- Coverage depends entirely on the words and patterns in the data files.

## Credits

The original IQATS is © 1998 Sam Hsiung. This repository contains the C# port.
