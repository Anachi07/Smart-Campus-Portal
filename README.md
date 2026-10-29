# ICAS Philippines Campus Portal

A connected **SmartSIMS + SmartQueue web demo** with one account, role-based access, and a responsive interface. The current scope is a **free demo using sample data**; ICAS Philippines branding uses the supplied logo reference and a green, white, and yellow interface.

## What works

- Public campus home, shared login, mobile navigation, password changes, and account notifications.
- Admin account/profile management, courses, subjects, sections, class schedules, calendar events, announcements, and school settings.
- Class-scoped teacher gradebook and attendance desk with validation. Re-saving attendance updates the student/class/date instead of duplicating it.
- Student access to their own academic records; parents access only their linked child's records. Reports and exports follow the same access boundaries.
- Rule-based academic support indicators calculated from recorded grades and attendance, with the correct GPA direction (1.00 best, 5.00 failing).
- Student ID attendance check-in, with optional browser camera barcode detection where supported and manual/keyboard-scanner fallback.
- Queue ticket creation, own-ticket tracking and QR links, counters, call/complete/skip/return, estimates, reports, and account notifications. Repeat submissions return an existing active ticket for the same service. New ticket numbers include the issue date; unresolved tickets from earlier dates expire, and tracking refreshes while the page is visible.
- Validated paired backup/restore of the academic database and queue JSON, including safety copies and revocation of prior sessions after restore.
- Atomic queue writes, corruption detection, CSRF checks, login throttling, private-file protection, and security headers.

## Run locally

Requires .NET 8 SDK, PHP 8.2+ with PDO/SQLite, and Node.js 18+.

```sh
dotnet restore queue/SmartQueueSystemGUI.csproj
node scripts/start.mjs
```

The local gateway uses port 8080; PHP and ASP.NET Core use internal ports 8082 and 8081. Stop the launcher with Ctrl+C to stop all services. The prepared cloud workspace automatically selects tools under `/workspace/tools`; elsewhere, put tools on PATH or set `DOTNET_BIN` and `PHP_BIN`.

The launcher can use `CAMPUS_DATA_DIR` for a separate data directory; original sample files are copied only if target data files do not already exist. It generates a fresh internal session-service key each startup and does not print it. All components use Asia/Manila time. Without an explicit data directory, the launcher uses ignored `.runtime/data` so sample files in the checkout remain unchanged.

## Accounts

Accounts are managed privately by the school Super Admin. Public pages do not display sample usernames or passwords. Password inputs remain masked, and new or changed passwords require at least 12 characters. Change any initial/sample account passwords through My profile before giving accounts to actual users; hiding a login hint does not rotate existing credentials.

The repository's sample-data fixtures are for development testing only. Keep real account credentials out of source files, screenshots, public FAQs, and chat messages.

## Deployment

See [DEPLOY.md](DEPLOY.md). The Docker/Render Blueprint remains on the **free** plan with `CAMPUS_MODE=demo`. No paid service or disk is created by these changes. Free Render storage is ephemeral: records may reset on redeploy, and idle services can sleep. Download a workspace backup before redeploying if you need to preserve changes.

This free demo is not approved for real student information. A production rollout still requires durable storage, private credentials, operational backups, and a separate school-specific review. Facial recognition, RFID hardware, live SMS delivery, and trained AI predictions are not connected. In-app notifications work; phone logs are explicitly marked as not sent.

## Validation

Use disposable sample data; these tests write records.

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 python scripts/smoke-test.py
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 python scripts/regression-test.py
```

The regression suite exercises permissions/report isolation, student-only registration, password session revocation, admin CRUD/linked profiles, last-owner protection, grade/GPA validation, attendance upsert, duplicate tickets, ticket ownership, calendar/settings, and backup validation/round-trip restore. It requires explicit acknowledgement because it restores a workspace backup.

Optional browser checks require Playwright and Chromium:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/browser-check.cjs
```

The browser suite checks desktop/mobile navigation, JavaScript errors, and horizontal page overflow; screenshots are written only to ignored `.runtime/screens`.

## Original uploads and storage

The original uploaded ZIPs remain unchanged. This web integration uses the PHP SmartSIMS version; its Windows Forms executable is not included. Academic and queue stores remain separate but are backed up/restored together. Old queue-only passwords cannot establish a portal session, and historical queue-only accounts were not automatically matched to academic people. Existing sample records are preserved in the repository.

## ICAS Philippines branding and student services

The site now uses ICAS Philippines branding with green, white, and yellow across the public home, sign-in, academic workspace, and Registrar/Cashier pages. A web-prepared logo asset derived from the supplied chat reference is stored at `sims/assets/images/icas-logo.png`. `/sims/brand-logo.php` serves that asset; it can be replaced with the school's original high-resolution PNG/JPG/SVG without changing the templates. The artwork is a prepared reference version, not an independently verified official master file.

School queues are limited to **Document Request**, **School Fees**, and **Tuition Payment**. Each request includes the relevant transaction type, contact details, student/reference number, and optional office notes. Documents go to Registrar counters; fees and tuition go to Cashier counters. A counter can also be assigned All Services. Estimates use only matching active counters, and the public display shows simultaneous office transactions without displaying student names.

These are office-service queue tickets. They do not generate academic documents, charge tuition online, calculate an official balance, issue receipts, or confirm payment. The responsible school office verifies student details, requirements, amounts, release, and receipts. The deployment remains a free, sample-data demo.

## School assistant and planned visits

The compact ICAS Assistant provides published school guidance, optional Gemini/OpenAI answers with server-side credentials, and confirmed queue reservations. Administrators publish school facts and configure booking availability. See [ASSISTANT.md](ASSISTANT.md) for setup and limits. Without a provider key, it is explicitly a school guide; scheduling still works.

## Super Admin and account approval

**Super Admin** alone creates accounts, assigns roles, resets other users' passwords, and approves or rejects pending registrations. Regular Admin retains academic and office operations; account-management routes return 403. Complete workspace backup/restore is also restricted to Super Admin because it includes and can replace account credentials and roles.

On first upgrade, the oldest active Admin is promoted once to Super Admin, preserving the existing username and password and invalidating that account's old sessions. No new shared password is generated or displayed. Sign in again with the existing owner login, open **Account governance → Accounts & approvals**, and create a separate regular Admin if needed. Future production initialization uses the existing `CAMPUS_ADMIN_USERNAME` / `CAMPUS_ADMIN_PASSWORD` settings to create the first Super Admin. Existing approved users remain active. New self-registrations request Student access and stay **Pending** until approved. Keep at least one active Super Admin; self-demotion and deleting the last owner are blocked.

Visits can be rescheduled after confirmation, or exchanged through private in-app invitations with the other holder's consent. See [ASSISTANT.md](ASSISTANT.md) for time limits and availability checks.

The new governance/exchange checks use disposable data and restore a full backup:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/governance-exchange-test.py
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/exchange-browser.cjs
```

Coverage includes regular Admin access denial, pending-account approval/rejection, last-owner protection, reschedule ownership and stale confirmations, private exchange invitations, decline/withdraw/accept, class-schedule revalidation, simultaneous acceptance, and preserved office capacity.

## Complete campus service workflow

Document requests now have staged tracking and private printable claim stubs. School-managed requirements appear before a transaction and become a request checklist, with separate student-prepared and office-verified flags. In-app reminders, office holidays/weekly breaks, private transaction feedback, and filterable service reports with summary CSV exports are integrated with the queue and booking engine. See [CAMPUS_SERVICES.md](CAMPUS_SERVICES.md) for the staff workflow, notification limits, and validation commands. Office requirements and closures must be entered by the school; none are assumed official by default.
