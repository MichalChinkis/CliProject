using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
createRspCommand.SetHandler(async () =>
{
    // קלט מהמשתמש עבור כל אפשרות
    Console.WriteLine("Enter languages (comma-separated or 'all'):");
    var language = Console.ReadLine()?.Trim();
    if (language == "") language = "csharp";

    Console.WriteLine("Enter output file name (optional, default 'bundle.txt'):");
    var output = Console.ReadLine()?.Trim();

    Console.WriteLine("Include note with source code paths? (true/false):");
    var note = Console.ReadLine()?.Trim();

    Console.WriteLine("Remove empty lines? (true/false):");
    var removeEmptyLines = Console.ReadLine()?.Trim();

    Console.WriteLine("Enter author name (optional):");
    var author = Console.ReadLine()?.Trim();

    // בניית הפקודה המלאה
    var rspContent = $"--language \"{language}\"\n";
    if (!string.IsNullOrEmpty(output)) rspContent += $"--output \"{output}\"\n";
    if (!string.IsNullOrEmpty(note)) rspContent += $"--note {note}\n";
    if (!string.IsNullOrEmpty(removeEmptyLines)) rspContent += $"--remove-empty-lines {removeEmptyLines}\n";
    if (!string.IsNullOrEmpty(author)) rspContent += $"--author \"{author}\"";

    // כתיבת הפקודה לקובץ .rsp
    var rspFileName = "bundle.rsp";
    await File.WriteAllTextAsync(rspFileName, rspContent);

    Console.WriteLine($"Response file '{rspFileName}' created successfully!");
});

var bundleCommand = new Command("bundle", "Bundle code files into a single file")
{
    new Option<string>("--language", "Comma-separated list of programming languages (or 'all')") { IsRequired = false },
    new Option<string>("--output", "Optional name of the output file"),
    new Option<bool>("--note", "Include a note with the source code paths in the bundle"),
    new Option<bool>("--remove-empty-lines", "Remove empty lines from files"),
    new Option<string>("--sort", "Sort files by 'name' or 'extension'") { IsRequired = false },
    new Option<string>("--author", "Author name to include at the top of the bundle file") { IsRequired = false }
};

bundleCommand.SetHandler(async (InvocationContext context) =>
{
    var language = context.ParseResult.GetValueForOption(bundleCommand.Options.First(o => o.Name == "language")) ?? "csharp";
    var fileName = context.ParseResult.GetValueForOption(bundleCommand.Options.First(o => o.Name == "output")) ?? "bundle.txt";
    var author = context.ParseResult.GetValueForOption(bundleCommand.Options.First(o => o.Name == "author")) ?? "";
    var includeNote = context.ParseResult.GetValueForOption<bool>((Option<bool>)bundleCommand.Options.First(o => o.Name == "note"));
    var removeEmptyLines = context.ParseResult.GetValueForOption<bool>((Option<bool>)bundleCommand.Options.First(o => o.Name == "remove-empty-lines"));
    var sort = context.ParseResult.GetValueForOption(bundleCommand.Options.First(o => o.Name == "sort")) ?? "name";

    if (string.IsNullOrEmpty(language.ToString()))
    {
        Console.WriteLine("The --language option is required.");
        return;
    }

    // בחירת קבצים
    string[] selectedFiles;
    if (language.ToString().ToLower() == "all")
    {
        selectedFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.TopDirectoryOnly);
    }
    else
    {
        var languages = language.ToString().Split(',').Select(l => l.Trim().ToLower()).ToArray();
        var languageExtensions = GetExtensionsForLanguages(languages);
        selectedFiles = languageExtensions.SelectMany(ext => Directory.GetFiles(Directory.GetCurrentDirectory(), $"*.{ext}")).ToArray();
    }

    await CreateBundle(selectedFiles, fileName.ToString(), includeNote, sort.ToString(), removeEmptyLines, author.ToString());
});

var rootCommand = new RootCommand();
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);

// פונקציה לאיחוד קבצים
async Task CreateBundle(string[] files, string fileName, bool includeNote, string sort, bool removeEmptyLines, string author)
{
    var bundleFile = fileName.EndsWith(".txt") ? fileName : fileName + ".txt";

    files = sort.ToLower() == "extension"
        ? files.OrderBy(f => Path.GetExtension(f)).ToArray()
        : files.OrderBy(f => Path.GetFileName(f)).ToArray();

    using (var writer = new StreamWriter(bundleFile, false))
    {
        if (!string.IsNullOrEmpty(author))
        {
            writer.WriteLine($"// Author: {author}");
        }
        foreach (var file in files)
        {
            if (includeNote)
            {
                writer.WriteLine($"// Source: {Path.GetFullPath(file)}");
            }
            writer.WriteLine($"// --- Start of {Path.GetFileName(file)} ---");
            var content = await File.ReadAllTextAsync(file);
            if (removeEmptyLines) content = RemoveEmptyLines(content);
            await writer.WriteLineAsync(content);
            writer.WriteLine($"// --- End of {Path.GetFileName(file)} ---\n");
        }
    }

    Console.WriteLine($"Files bundled into {bundleFile}");
}

// פונקציה שמחזירה את סיומות הקבצים המתאימים לשפות שנבחרו
string[] GetExtensionsForLanguages(string[] languages)
{
    var languageExtensions = new System.Collections.Generic.Dictionary<string, string[]>
    {
        { "csharp", new[] { "cs" } },
        { "javascript", new[] { "js" } },
        { "python", new[] { "py" } },
        { "java", new[] { "java" } }
    };

    return languages.SelectMany(lang => languageExtensions.ContainsKey(lang) ? languageExtensions[lang] : new string[0]).ToArray();
}

// פונקציה להסרת שורות ריקות
string RemoveEmptyLines(string content)
{
    return string.Join(Environment.NewLine, content.Split(Environment.NewLine).Where(line => !string.IsNullOrWhiteSpace(line)));
}
