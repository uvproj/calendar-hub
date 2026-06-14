using System.Globalization;
using System.Net.Mail;

namespace CalendarCli;

internal static class CalendarConsole
{
    private static readonly ServiceStore ServiceStore = new();

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            PrintRootUsage();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "events" => await HandleEventsAsync(args[1..]),
            "services" => await HandleServicesAsync(args[1..]),
            _ => ExitWithUsage($"Unknown command '{args[0]}'.", PrintRootUsage)
        };
    }

    private static async Task<ICalendarService> GetCalendarServiceAsync(string? serviceName)
    {
        var services = ServiceStore.LoadAll();
        Service? service = null;

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            service = services.FirstOrDefault(s => s.Name.Equals(serviceName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (service is null)
            {
                throw new CliUsageException($"Service '{serviceName}' was not found.");
            }
        }
        else
        {
            service = services.FirstOrDefault(s => s.IsDefault);
            if (service is null)
            {
                var fallbackPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "calendar-cli",
                    "events.json");
                return new FileSystemCalendarService(fallbackPath);
            }
        }

        return service.Type switch
        {
            ServiceType.FileSystem => new FileSystemCalendarService(service.FilePath ?? throw new CliUsageException("File path is missing for FileSystem service.")),
            ServiceType.Google => new GoogleCalendarService(service.SecretsJsonPath ?? throw new CliUsageException("Secrets JSON path is missing for Google service.")),
            _ => throw new CliUsageException($"Unsupported service type '{service.Type}'.")
        };
    }

    private static async Task<int> HandleEventsAsync(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            PrintEventsUsage();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "add" => await HandleAddAsync(args[1..]),
            "list" => await HandleListAsync(args[1..]),
            "delete" => await HandleDeleteAsync(args[1..]),
            _ => ExitWithUsage($"Unknown events command '{args[0]}'.", PrintEventsUsage)
        };
    }

    private static async Task<int> HandleAddAsync(string[] args)
    {
        CliArguments parsed;

        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--name", "--when", "--date-time", "--description", "--location", "--invitee", "--service");
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintAddUsage);
        }

        if (parsed.HelpRequested)
        {
            PrintAddUsage();
            return 0;
        }

        if (parsed.Positionals.Count > 0)
        {
            return ExitWithUsage("Unexpected positional arguments were supplied.", PrintAddUsage);
        }

        var name = parsed.GetSingleValue("--name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return ExitWithUsage("The --name option is required.", PrintAddUsage);
        }

        var dateTimeText = parsed.GetSingleValue("--when", "--date-time");
        if (string.IsNullOrWhiteSpace(dateTimeText))
        {
            return ExitWithUsage("The --when option is required.", PrintAddUsage);
        }

        if (!DateTimeOffset.TryParse(
                dateTimeText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var startsAt))
        {
            return ExitWithUsage(
                "The event date/time could not be parsed. Use an ISO-like value such as 2026-06-18T14:30.",
                PrintAddUsage);
        }

        List<string> invitees;

        try
        {
            invitees = parsed
                .GetValues("--invitee")
                .Select(NormalizeEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintAddUsage);
        }

        var serviceName = parsed.GetSingleValue("--service");
        ICalendarService calendarService;
        try
        {
            calendarService = await GetCalendarServiceAsync(serviceName);
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintAddUsage);
        }

        var calendarEvent = new CalendarEvent(
            string.Empty,
            name.Trim(),
            TrimToNull(parsed.GetSingleValue("--description")),
            TrimToNull(parsed.GetSingleValue("--location")),
            startsAt,
            invitees);

        try
        {
            await calendarService.AddAsync(calendarEvent);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding event: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Added event '{calendarEvent.Name}'.");
        Console.WriteLine($"Id: {calendarEvent.Id}");
        Console.WriteLine($"When: {FormatDateTime(calendarEvent.StartsAt)}");

        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            Console.WriteLine($"Location: {calendarEvent.Location}");
        }

        if (calendarEvent.Invitees.Count > 0)
        {
            Console.WriteLine($"Invitees: {string.Join(", ", calendarEvent.Invitees)}");
        }

        return 0;
    }

    private static async Task<int> HandleListAsync(string[] args)
    {
        CliArguments parsed;

        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--month", "--service");
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintListUsage);
        }

        if (parsed.HelpRequested)
        {
            PrintListUsage();
            return 0;
        }

        if (parsed.Positionals.Count > 0)
        {
            return ExitWithUsage("Unexpected positional arguments were supplied.", PrintListUsage);
        }

        DateTimeOffset windowStart;
        DateTimeOffset windowEnd;
        var monthText = parsed.GetSingleValue("--month");

        if (!string.IsNullOrWhiteSpace(monthText))
        {
            if (!DateTime.TryParseExact(monthText, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            {
                return ExitWithUsage("The --month option must use the format yyyy-MM.", PrintListUsage);
            }

            var localMonthStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);
            windowStart = new DateTimeOffset(localMonthStart);
            windowEnd = windowStart.AddMonths(1);
        }
        else
        {
            windowStart = DateTimeOffset.Now;
            windowEnd = windowStart.AddMonths(1);
        }

        var serviceName = parsed.GetSingleValue("--service");
        ICalendarService calendarService;
        try
        {
            calendarService = await GetCalendarServiceAsync(serviceName);
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintListUsage);
        }

        IReadOnlyList<CalendarEvent> events;
        try
        {
            events = await calendarService.ListBetweenAsync(windowStart, windowEnd);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error listing events: {ex.Message}");
            return 1;
        }

        if (events.Count == 0)
        {
            Console.WriteLine("No events found for the requested time window.");
            return 0;
        }

        Console.WriteLine($"Events from {windowStart:yyyy-MM-dd} to {windowEnd.AddTicks(-1):yyyy-MM-dd}:");

        foreach (var calendarEvent in events)
        {
            Console.WriteLine();
            Console.WriteLine($"[{calendarEvent.Id}] {calendarEvent.Name}");
            Console.WriteLine($"  When: {FormatDateTime(calendarEvent.StartsAt)}");

            if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
            {
                Console.WriteLine($"  Location: {calendarEvent.Location}");
            }

            if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
            {
                Console.WriteLine($"  Description: {calendarEvent.Description}");
            }

            if (calendarEvent.Invitees.Count > 0)
            {
                Console.WriteLine($"  Invitees: {string.Join(", ", calendarEvent.Invitees)}");
            }
        }

        return 0;
    }

    private static async Task<int> HandleDeleteAsync(string[] args)
    {
        CliArguments parsed;

        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--id", "--service");
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintDeleteUsage);
        }

        if (parsed.HelpRequested)
        {
            PrintDeleteUsage();
            return 0;
        }

        if (parsed.Positionals.Count > 0)
        {
            return ExitWithUsage("Unexpected positional arguments were supplied.", PrintDeleteUsage);
        }

        var idText = parsed.GetSingleValue("--id");
        if (!Guid.TryParse(idText, out var eventId))
        {
            return ExitWithUsage("The --id option is required and must be a valid GUID.", PrintDeleteUsage);
        }

        var serviceName = parsed.GetSingleValue("--service");
        ICalendarService calendarService;
        try
        {
            calendarService = await GetCalendarServiceAsync(serviceName);
        }
        catch (CliUsageException ex)
        {
            return ExitWithUsage(ex.Message, PrintDeleteUsage);
        }

        try
        {
            var (succeeded, deletedEvent) = await calendarService.DeleteAsync(eventId);
            if (!succeeded)
            {
                Console.Error.WriteLine($"No event was found for id '{eventId}'.");
                return 1;
            }

            Console.WriteLine($"Deleted event '{deletedEvent!.Name}' ({deletedEvent.Id}).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting event: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleServicesAsync(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            PrintServicesUsage();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "add" => await HandleServiceAddAsync(args[1..]),
            "list" => await HandleServiceListAsync(args[1..]),
            "remove" => await HandleServiceRemoveAsync(args[1..]),
            "set-default" => await HandleServiceSetDefaultAsync(args[1..]),
            _ => ExitWithUsage($"Unknown services command '{args[0]}'.", PrintServicesUsage)
        };
    }

    private static Task<int> HandleServiceAddAsync(string[] args)
    {
        CliArguments parsed;
        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--name", "--type", "--file-path", "--secrets-path", "--default");
        }
        catch (CliUsageException ex)
        {
            return Task.FromResult(ExitWithUsage(ex.Message, PrintServiceAddUsage));
        }

        if (parsed.HelpRequested)
        {
            PrintServiceAddUsage();
            return Task.FromResult(0);
        }

        if (parsed.Positionals.Count > 0)
        {
            return Task.FromResult(ExitWithUsage("Unexpected positional arguments were supplied.", PrintServiceAddUsage));
        }

        var name = parsed.GetSingleValue("--name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ExitWithUsage("The --name option is required.", PrintServiceAddUsage));
        }

        name = name.Trim();

        var typeText = parsed.GetSingleValue("--type");
        if (string.IsNullOrWhiteSpace(typeText))
        {
            return Task.FromResult(ExitWithUsage("The --type option is required.", PrintServiceAddUsage));
        }

        if (!Enum.TryParse<ServiceType>(typeText, true, out var type))
        {
            return Task.FromResult(ExitWithUsage($"Invalid service type '{typeText}'. Allowed values are FileSystem, Google.", PrintServiceAddUsage));
        }

        string? filePath = null;
        string? secretsPath = null;

        if (type == ServiceType.FileSystem)
        {
            filePath = parsed.GetSingleValue("--file-path");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(ExitWithUsage("The --file-path option is required for FileSystem service type.", PrintServiceAddUsage));
            }
        }
        else if (type == ServiceType.Google)
        {
            secretsPath = parsed.GetSingleValue("--secrets-path");
            if (string.IsNullOrWhiteSpace(secretsPath))
            {
                return Task.FromResult(ExitWithUsage("The --secrets-path option is required for Google service type.", PrintServiceAddUsage));
            }
        }

        var makeDefaultValue = parsed.GetSingleValue("--default");
        bool makeDefault = makeDefaultValue != null && makeDefaultValue.Equals("true", StringComparison.OrdinalIgnoreCase);

        var services = ServiceStore.LoadAll();
        if (services.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"Service '{name}' already exists.");
            return Task.FromResult(1);
        }

        if (services.Count == 0)
        {
            makeDefault = true;
        }

        if (makeDefault)
        {
            foreach (var s in services)
            {
                s.IsDefault = false;
            }
        }

        var newService = new Service
        {
            Name = name,
            Type = type,
            IsDefault = makeDefault,
            FilePath = filePath,
            SecretsJsonPath = secretsPath
        };

        services.Add(newService);
        ServiceStore.SaveAll(services);

        Console.WriteLine($"Added service '{name}' ({type})" + (makeDefault ? " as default." : "."));
        return Task.FromResult(0);
    }

    private static Task<int> HandleServiceListAsync(string[] args)
    {
        CliArguments parsed;
        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions();
        }
        catch (CliUsageException ex)
        {
            return Task.FromResult(ExitWithUsage(ex.Message, PrintServiceListUsage));
        }

        if (parsed.HelpRequested)
        {
            PrintServiceListUsage();
            return Task.FromResult(0);
        }

        if (parsed.Positionals.Count > 0)
        {
            return Task.FromResult(ExitWithUsage("Unexpected positional arguments were supplied.", PrintServiceListUsage));
        }

        var services = ServiceStore.LoadAll();
        if (services.Count == 0)
        {
            Console.WriteLine("No services registered.");
            return Task.FromResult(0);
        }

        Console.WriteLine("Registered services:");
        foreach (var service in services)
        {
            var defaultSuffix = service.IsDefault ? " (default)" : "";
            var configDetails = service.Type switch
            {
                ServiceType.FileSystem => $"file: {service.FilePath}",
                ServiceType.Google => $"secrets: {service.SecretsJsonPath}",
                _ => string.Empty
            };
            Console.WriteLine($"  - {service.Name}{defaultSuffix} [Type: {service.Type}, {configDetails}]");
        }

        return Task.FromResult(0);
    }

    private static Task<int> HandleServiceRemoveAsync(string[] args)
    {
        CliArguments parsed;
        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--name");
        }
        catch (CliUsageException ex)
        {
            return Task.FromResult(ExitWithUsage(ex.Message, PrintServiceRemoveUsage));
        }

        if (parsed.HelpRequested)
        {
            PrintServiceRemoveUsage();
            return Task.FromResult(0);
        }

        if (parsed.Positionals.Count > 0)
        {
            return Task.FromResult(ExitWithUsage("Unexpected positional arguments were supplied.", PrintServiceRemoveUsage));
        }

        var name = parsed.GetSingleValue("--name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ExitWithUsage("The --name option is required.", PrintServiceRemoveUsage));
        }

        name = name.Trim();

        var services = ServiceStore.LoadAll();
        var existing = services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Console.Error.WriteLine($"Service '{name}' not found.");
            return Task.FromResult(1);
        }

        bool removedWasDefault = existing.IsDefault;
        services.Remove(existing);

        if (removedWasDefault && services.Count > 0)
        {
            services[0].IsDefault = true;
            Console.WriteLine($"Removed default service '{existing.Name}'. '{services[0].Name}' is now the default service.");
        }
        else
        {
            Console.WriteLine($"Removed service '{existing.Name}'.");
        }

        ServiceStore.SaveAll(services);
        return Task.FromResult(0);
    }

    private static Task<int> HandleServiceSetDefaultAsync(string[] args)
    {
        CliArguments parsed;
        try
        {
            parsed = CliArguments.Parse(args);
            parsed.EnsureOnlyKnownOptions("--name");
        }
        catch (CliUsageException ex)
        {
            return Task.FromResult(ExitWithUsage(ex.Message, PrintServiceSetDefaultUsage));
        }

        if (parsed.HelpRequested)
        {
            PrintServiceSetDefaultUsage();
            return Task.FromResult(0);
        }

        if (parsed.Positionals.Count > 0)
        {
            return Task.FromResult(ExitWithUsage("Unexpected positional arguments were supplied.", PrintServiceSetDefaultUsage));
        }

        var name = parsed.GetSingleValue("--name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ExitWithUsage("The --name option is required.", PrintServiceSetDefaultUsage));
        }

        name = name.Trim();

        var services = ServiceStore.LoadAll();
        var target = services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            Console.Error.WriteLine($"Service '{name}' not found.");
            return Task.FromResult(1);
        }

        foreach (var s in services)
        {
            s.IsDefault = false;
        }

        target.IsDefault = true;
        ServiceStore.SaveAll(services);

        Console.WriteLine($"Set service '{target.Name}' as the default service.");
        return Task.FromResult(0);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new CliUsageException("Invitee email values cannot be empty.");
        }

        try
        {
            return new MailAddress(email.Trim()).Address;
        }
        catch (FormatException)
        {
            throw new CliUsageException($"'{email}' is not a valid email address.");
        }
    }

    private static string FormatDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHelpToken(string value) =>
        value is "--help" or "-h" or "help";

    private static int ExitWithUsage(string message, Action printUsage)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
        printUsage();
        return 1;
    }

    private static void PrintRootUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar events <add|list|delete> [options]");
        Console.WriteLine("  calendar services <add|list|remove|set-default> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  calendar events add            Add a new event");
        Console.WriteLine("  calendar events list           List events in the next month or a specific month");
        Console.WriteLine("  calendar events delete         Delete an event by id");
        Console.WriteLine("  calendar services add          Add a new service");
        Console.WriteLine("  calendar services list         List all registered services");
        Console.WriteLine("  calendar services remove       Remove a service by name");
        Console.WriteLine("  calendar services set-default  Set a service as default");
    }

    private static void PrintEventsUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar events add [options]");
        Console.WriteLine("  calendar events list [options]");
        Console.WriteLine("  calendar events delete [options]");
    }

    private static void PrintAddUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar events add --name <text> --when <date-time> [--description <text>] [--location <text>] [--invitee <email> ...] [--service <name>]");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  calendar events add --name \"Sprint review\" --description \"Review open work\" --location \"Room 2\" --when \"2026-06-18T14:30\" --invitee alex@example.com --service MyGoogle");
    }

    private static void PrintListUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar events list [--month yyyy-MM] [--service <name>]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  calendar events list");
        Console.WriteLine("  calendar events list --month 2026-06 --service MyFile");
    }

    private static void PrintDeleteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar events delete --id <event-id> [--service <name>]");
    }

    private static void PrintServicesUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar services add --name <name> --type <FileSystem|Google> [--file-path <path>] [--secrets-path <path>] [--default]");
        Console.WriteLine("  calendar services list");
        Console.WriteLine("  calendar services remove --name <name>");
        Console.WriteLine("  calendar services set-default --name <name>");
    }

    private static void PrintServiceAddUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar services add --name <text> --type <FileSystem|Google> [--file-path <path>] [--secrets-path <path>] [--default]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  calendar services add --name MyFile --type FileSystem --file-path C:\\Users\\yuvaraj.nagarajan\\mycal.json");
        Console.WriteLine("  calendar services add --name MyGoogle --type Google --secrets-path C:\\path\\to\\secrets.json --default");
    }

    private static void PrintServiceListUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar services list");
    }

    private static void PrintServiceRemoveUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar services remove --name <text>");
    }

    private static void PrintServiceSetDefaultUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  calendar services set-default --name <text>");
    }
}
