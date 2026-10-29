# Smart Campus Portal

Pinagsamang **SmartSIMS web** at **SmartQueueSystemGUI web** mula sa dalawang uploaded ZIP.

## Kasama

- Isang portal home at SIMS login para sa parehong module.
- SmartSIMS: student records, grades, attendance, schedules, academic reports, calendar, teacher/parent pages, at existing analytics screens.
- SmartQueue: ticket generation, tracking, counters, call/complete/skip/return, queue reports, notifications, at public display.
- Navigation para makalipat ng module nang hindi nagla-login ulit.
- Local Bootstrap, Chart.js, at Font Awesome assets.

## Requirements

- .NET **8 SDK** (the queue app was upgraded from .NET 6)
- PHP **8.2+** with `PDO`, `pdo_sqlite`, and `sqlite3` enabled
- Node.js **18+**
- Python 3 only for the optional integration checks

## Patakbuhin

Sa folder na ito:

```sh
dotnet restore queue/SmartQueueSystemGUI.csproj
node scripts/start.mjs
```

Buksan ang browser sa local port **8080**, path `/portal`. Ports 8081 and 8082 are internal services. Stop with Ctrl+C; the launcher stops all three processes. If tools are not on PATH, set `DOTNET_BIN` and `PHP_BIN` to their executable paths. The prepared cloud workspace automatically uses tools under `/workspace/tools`.

The launcher builds the queue project, creates a random internal authentication key, and starts PHP, ASP.NET Core, and the Node gateway. Do not launch the queue directly as a standalone app: authentication now depends on the SIMS session service. Changing `PORT` changes only the gateway port.

## Demo login

These are the existing sample accounts from SmartSIMS, verified against the included sample database:

| Role | Username | Password |
| --- | --- | --- |
| Admin | ADMIN | ADMIN |
| Student | 2024-0002 | student123 |

Admin opens queue administration. Students, teachers, and parents open queue ticket services. Queue account-management links go to SIMS Users & Roles. Only an active SIMS account can establish queue access. Logging out or expiring the SIMS session revokes queue access on the next request.

## Data and compatibility

- `sims/database/SmartSIMS.db`: original uploaded SIMS SQLite database.
- `queue/Data/smartqueue-data.json`: original uploaded queue data, plus linked campus account ids on first use.
- Original uploaded ZIPs are not modified. This package uses the PHP web version of SmartSIMS; the Windows Forms executable is not part of the web portal.
- Data stores remain separate. SIMS is the shared account authority. Existing queue-only passwords are not used for portal login. Old queue tickets keep their original account links; historical queue accounts have not been automatically merged with SIMS people. Existing public ticket search remains available.
- Queue phone fields can be supplied when a SIMS profile does not have a phone number. Editing a ticket phone does not edit the academic profile.

## Checks

```sh
# With the portal already running on port 8080:
python scripts/smoke-test.py
```

This uses the sample admin and student accounts and **creates demo queue tickets and calls the next ticket**. Run it on a disposable copy when you want to preserve live data. Verified: .NET build; admin/student login; academic pages; role restrictions; ticket creation/search; call-next; shared logout; database/config blocking; local assets. Broader academic workflows were retained from the source apps and have not all been individually exercised.

## Development scope

This is a locally runnable integration using a PHP development server and a Node gateway. Before public deployment, configure HTTPS and production service hosting, replace demo credentials, and review the source apps' authorization, registration policies, data handling, and backup behavior. Existing AI attendance, predictive analytics, and SMS features retain the original implementations; this merge does not add a trained AI model or a live SMS provider.
