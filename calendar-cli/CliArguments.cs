namespace CalendarCli;

internal sealed class CliArguments
{
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Positionals { get; }

    public bool HelpRequested { get; }

    private CliArguments(IReadOnlyList<string> positionals, bool helpRequested)
    {
        Positionals = positionals;
        HelpRequested = helpRequested;
    }

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        var positionals = new List<string>();
        var helpRequested = false;
        var parsed = new CliArguments(positionals, helpRequested: false);

        for (var index = 0; index < args.Count; index++)
        {
            var current = args[index];

            if (current is "--help" or "-h" or "help")
            {
                helpRequested = true;
                continue;
            }

            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(current);
                continue;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                if (current.Equals("--default", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.AddOption(current, "true");
                    continue;
                }

                throw new CliUsageException($"Missing value for option '{current}'.");
            }

            parsed.AddOption(current, args[index + 1]);
            index++;
        }

        return new CliArguments(positionals, helpRequested, parsed._options);
    }

    public string? GetSingleValue(params string[] names)
    {
        var values = GetValues(names);

        return values.Count switch
        {
            0 => null,
            1 => values[0],
            _ => throw new CliUsageException($"Option '{names[0]}' can only be supplied once.")
        };
    }

    public IReadOnlyList<string> GetValues(params string[] names)
    {
        var values = new List<string>();

        foreach (var name in names)
        {
            if (_options.TryGetValue(name, out var optionValues))
            {
                values.AddRange(optionValues);
            }
        }

        return values;
    }

    public void EnsureOnlyKnownOptions(params string[] allowedNames)
    {
        var allowed = new HashSet<string>(allowedNames, StringComparer.OrdinalIgnoreCase);
        var unknown = _options.Keys.Where(name => !allowed.Contains(name)).OrderBy(name => name).ToList();

        if (unknown.Count > 0)
        {
            throw new CliUsageException($"Unknown option(s): {string.Join(", ", unknown)}");
        }
    }

    private void AddOption(string name, string value)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            values = [];
            _options[name] = values;
        }

        values.Add(value);
    }

    private CliArguments(
        IReadOnlyList<string> positionals,
        bool helpRequested,
        Dictionary<string, List<string>> options)
        : this(positionals, helpRequested)
    {
        _options = new Dictionary<string, List<string>>(options, StringComparer.OrdinalIgnoreCase);
    }
}
