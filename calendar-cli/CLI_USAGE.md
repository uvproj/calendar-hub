# Calendar CLI Documentation

This document describes the commands, switches, and usage examples for the calendar CLI tool (`calendar`).

---

## Global Help
To view help for any command or subcommand, append `--help`, `-h`, or `help` to the command line.
For example:
```bash
calendar --help
calendar events add --help
calendar services --help
```

---

## Command Reference

### 1. `events` Command
Manages calendar events.

#### `calendar events add`
Adds a new calendar event.
- **Switches**:
  - `--name <text>` (Required): The title/name of the event.
  - `--when <date-time>` or `--date-time <date-time>` (Required): The start date and time of the event. Must use an ISO-8601-like format (e.g. `2026-06-18T14:30`).
  - `--description <text>` (Optional): A description of the event.
  - `--location <text>` (Optional): The physical or virtual location of the event.
  - `--invitee <email>` (Optional, Multiple Allowed): An invitee's email address. Can be supplied multiple times for multiple invitees.
  - `--service <name>` (Optional): Specifies the target service to create the event in. If not provided, the default service will be used. If no services are registered, it falls back to the local filesystem calendar.
- **Example**:
  ```bash
  calendar events add --name "Sprint Planning" --when "2026-06-18T10:00" --description "Plan tasks for Sprint 5" --location "Meeting Room 4" --invitee alex@example.com --service MyGoogle
  ```

#### `calendar events list`
Lists events within a specific time window.
- **Switches**:
  - `--month <yyyy-MM>` (Optional): Specifies the month to list events for (e.g. `2026-06`). If omitted, lists events for the next one month starting from the current system date and time.
  - `--service <name>` (Optional): Specifies the target service to list events from. If not provided, the default service will be used.
- **Examples**:
  ```bash
  calendar events list
  calendar events list --month 2026-06 --service MyFile
  ```

#### `calendar events delete`
Deletes an event by its unique Identifier.
- **Switches**:
  - `--id <guid>` (Required): The GUID of the event to delete.
  - `--service <name>` (Optional): Specifies the target service to delete the event from. If not provided, the default service will be used.
- **Example**:
  ```bash
  calendar events delete --id 3b2d1c5a-ff04-4530-9b48-1875c740a32e --service MyGoogle
  ```

---

### 2. `services` Command
Manages the calendar services (e.g. Google, FileSystem) and their settings.

#### `calendar services add`
Registers a new service provider.
- **Switches**:
  - `--name <text>` (Required): The unique name of the service (e.g., `MyFile`, `MyGoogle`).
  - `--type <FileSystem|Google>` (Required): The type of the service.
  - `--file-path <path>` (Required for `FileSystem` type): The file path where the calendar events JSON file will be stored.
  - `--secrets-path <path>` (Required for `Google` type): The path to the Google Client ID secrets JSON file.
  - `--default` (Optional, Flag): Sets this service as the default calendar service. If it is the first service added, it is automatically set as the default service.
- **Examples**:
  ```bash
  calendar services add --name MyFile --type FileSystem --file-path C:\Users\yuvaraj\mycal.json
  calendar services add --name MyGoogle --type Google --secrets-path C:\path\to\secrets.json --default
  ```

#### `calendar services list`
Lists all registered service providers, showing their names, types, configurations, and which service is default.
- **Switches**: None.
- **Example**:
  ```bash
  calendar services list
  ```
  *Output Example:*
  ```
  Registered services:
    - MyFile [Type: FileSystem, file: C:\Users\yuvaraj\mycal.json]
    - MyGoogle (default) [Type: Google, secrets: C:\path\to\secrets.json]
  ```

#### `calendar services remove`
Removes a registered service provider. If the removed service was set as default and other services remain, the default status is automatically reassigned to the first remaining service in the list.
- **Switches**:
  - `--name <text>` (Required): The name of the service to remove.
- **Example**:
  ```bash
  calendar services remove --name MyFile
  ```

#### `calendar services set-default`
Sets a registered service provider as the default service. Only one service can be set as default at any given time.
- **Switches**:
  - `--name <text>` (Required): The name of the service to set as default.
- **Example**:
  ```bash
  calendar services set-default --name MyGoogle
  ```

---

## Data Storage Location

- **Events Data (Default FileSystem Fallback)**: Stored in a JSON file at `%localappdata%\calendar-cli\events.json`.
- **Services Data**: Stored in a JSON file at `%localappdata%\calendar-cli\services.json`.
- **Google OAuth Tokens**: Cached at `%localappdata%\calendar-cli\google-tokens\`.
