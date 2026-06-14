# Calendar CLI

Calendar CLI is a small command-line tool for managing calendar events from the terminal. It supports adding, listing, and deleting events, and it can work against either a local JSON-backed calendar or a configured Google Calendar service.

The CLI is implemented in the `calendar-cli` project and uses a service-based design so calendar operations flow through a shared abstraction instead of being tied directly to the console layer.

## What it supports

- Creating events with a name, date/time, description, location, and invitees
- Listing events for the upcoming window or for a specific month
- Deleting events by identifier
- Managing named calendar services, including setting a default service
- Falling back to a local filesystem calendar when no service is configured

## CLI usage

For the full command reference, switches, and examples, see:

- [calendar-cli/CLI_USAGE.md](calendar-cli/CLI_USAGE.md)

## Project location

The main CLI application lives in:

- `calendar-cli/`
