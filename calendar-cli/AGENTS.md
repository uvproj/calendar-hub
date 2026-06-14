# Agent Guidelines for Calendar CLI

This project is built to be easily navigated and automated by agentic AI coders.

## Critical Instructions for Future Agents

1. **Keep Documentation Up-to-Date**:
   - Any time you add, modify, or remove CLI options, switches, subcommands, or behavior in [CalendarConsole.cs](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/CalendarConsole.cs), you **must** update the corresponding documentation in [CLI_USAGE.md](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/CLI_USAGE.md).
   - Ensure that all switches, parameter formats, and examples accurately reflect the codebase's parsing rules.

2. **Calendar Services Architecture**:
   - Do not perform file operations or third-party service calls directly in console command handlers. All calendar actions must be routed through the [ICalendarService](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/ICalendarService.cs) interface.
   - Filesystem-specific calendar actions should reside in [FileSystemCalendarService.cs](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/FileSystemCalendarService.cs).
   - Google Calendar API actions should reside in [GoogleCalendarService.cs](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/GoogleCalendarService.cs).

3. **Google Authentication & Cache Storage**:
   - Caching for Google API access and refresh tokens is automatically written in `%localappdata%\calendar-cli\google-tokens\`.
   - Google Event IDs are parsed/written as standard lowercase GUID strings (without hyphens), e.g. `guid.ToString("n").ToLowerInvariant()`, because Google Calendar only accepts a-v and 0-9 for event identifiers.

4. **Parsing Constraints**:
   - The custom command-line parser [CliArguments.cs](file:///c:/Users/yuvaraj.nagarajan/source/grepos/CalendarHub/calendar-cli/CliArguments.cs) requires that all option switches starting with `--` are followed by a value, **except** for registered boolean flags like `--default`.
