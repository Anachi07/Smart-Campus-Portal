# Registrar and Cashier services

This release adds document processing, school-managed preparation checklists, in-app reminders, office closures, optional feedback, and service reports. The free demo still uses ephemeral storage: back up before redeploying and do not enter real student records.

## Where to find the features

| User | Location |
| --- | --- |
| Student, teacher, parent | Campus transactions → My service requests; or Registrar/Cashier → My requests |
| Office staff, Admin, Super Admin | Registrar/Cashier sidebar → Document requests, Requirements, Office calendar, Service reports |
| Reminders and progress notices | Queue Notifications / Alerts; also linked from the academic Notifications page |

Account creation and approvals remain exclusive to Super Admin. Regular Admin manages office operations and requirements, but cannot manage accounts or restore the full workspace.

## Document requests

New document tickets and reservations start **For review**. Registrar staff advance them in order: **Processing → Ready for pickup → Released**. Scheduled visits must be checked in before document progress begins. Queue status is separate: completing a counter visit does not confirm a document was released. Confirming an actual document release also completes any still-waiting/serving queue visit, without inventing a counter-call timestamp; an already-cancelled/expired visit retains that queue outcome. Previously existing document tickets are imported for review, with a history note explaining that their release is unknown.

Each progress change records its time and staff ID, and sends a private in-app notification. Notes are visible to the requester: do not include sensitive ID numbers, credentials, or payment details. Required checklist items must be office-verified before readiness. To confirm release, staff enter a note after checking the recipient's identity in person. A started document workflow remains with the Registrar even if its queue visit is later cancelled or expires; an unreviewed cancelled/expired request cannot begin processing through this screen.

The **claim stub** becomes available at Ready for pickup. It is printable and visible only to the requester or authorized staff. Its random reference code is not an academic document, receipt, or proof of identity. Staff must still verify collection eligibility; the code alone never authorizes document release. This system does not generate official academic documents or process payments.

## Requirements and student preparation

The school publishes approved requirements per service and transaction type. No official requirements are invented or seeded. Until requirements are configured, students are asked to confirm with the office.

Requirements appear before both a walk-in ticket and an assistant booking. A request snapshots the requirements at creation. Editing or archiving a definition affects future requests and preserves existing checklists. Students can mark their own items prepared; this does not mark them office-verified. Staff separately verify actual submissions. This release tracks readiness flags and instructions; it does not upload or validate identity documents.

## Office calendar

Admins can add a specific-date holiday/unavailable period, an all-day closure, or a recurring weekly break. Entries can target Registrar, Cashier, or All Services. Empty weekly day means every day; business booking days still apply. Times use Asia/Manila. Published booking hours come from queue Settings, not an assumed official school policy.

The same calendar excludes overlapping slots for new bookings, reschedules, and both sides of an exchange. Existing reservations are preserved and their owners receive a notice when a new closure affects them. Closed visits cannot check in. An affected reservation can move to a valid future slot even inside the normal one-hour change deadline. New times still need explicit confirmation and availability/class checks. Staff can remove a closure; removal does not silently move reservations back.

Explicit closures also stop new walk-in tickets and new counter calls for the closed office at that time. A visitor already being served may finish. Standard booking hours constrain appointment slots; they do not independently impose new walk-in business-hour restrictions on the existing queue workflow.

## Reminders

A server worker checks every 30 seconds while the service is running:

- One reminder within the 30 minutes before each reserved time; rescheduling to a different time allows a new reminder.
- One near-turn reminder for each waiting turn, scoped to the correct office and configured almost-next position. It is based on queue order, not a guarantee of immediate service.

Notifications respect account-notification settings; near-turn notices also respect the almost-next setting. Reminder markers persist with the ticket to prevent duplicate notices after a restart. Office updates and document progress generate transactional in-app notices. Full restore temporarily pauses the reminder worker during file replacement.

No email, SMS, browser push, or guaranteed offline delivery is connected. A sleeping free Render service cannot generate timely reminders. Users must check the portal and office notices; reservations overlapping a closure do not receive an ordinary visit reminder.

## Feedback and reports

A requester may submit one private rating (1–5) and an optional comment after a transaction is complete. Cashier feedback requires a served queue visit; document feedback requires Released. Other accounts cannot submit or overwrite that feedback. Authorized office staff can review responses; they are not public testimonials.

**Service reports** filter by request creation date and office. They show request totals, current queue outcomes, actual recorded wait times, common transactions, document progress, released-document count, and feedback averages. Wait starts at check-in/arrival and ends at counter call. Uncalled tickets are omitted from the wait average, not counted as zero. Scheduled visits without a recorded arrival are omitted as well. Outcomes and feedback refer to the chosen request cohort, even when completed later. Blank averages mean there are no eligible samples.

CSV exports contain aggregate metrics, not student names, phone numbers, claim codes, or feedback comments. Reports and exports require an authorized staff session.

## Verification

Run against disposable data only; integration/browser checks restore a full workspace backup.

```sh
dotnet run --project tests/AssistantChecks/AssistantChecks.csproj
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/operations-test.py
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/operations-browser.cjs
```

Checks cover ownership and office permissions, document transitions, verified requirements, private claims, feedback eligibility/deduplication, office/date isolation, late-closure changes, reminder deduplication, actual wait calculations, escaped feedback, summary exports, storage round trips, and desktop/mobile flows. Existing account, queue, assistant, and exchange regression suites remain applicable.
