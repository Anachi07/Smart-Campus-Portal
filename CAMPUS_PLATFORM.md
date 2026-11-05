# Connected campus workflows

This batch extends the existing free demo. It does not provision paid Render services,
process payments, establish official school policy, or make ephemeral storage durable.
Use sample data until the school completes its production review.

## Where to start

The shared sidebar links to Digital clearance, School helpdesk, School forms,
Announcements, Class QR attendance, and Account security. Admins also have Staff
workload; Super Admin has Offices & approval routes. The campus overview combines
requests requiring attention, a transaction timeline, appointments, and office notices.
The calendar adds assigned classes and reserved visits to school events and closures.

### Offices, clearance, forms, and helpdesk

Super Admin configures active offices and assigns an active Admin or Teacher as its
reviewer. Super Admin can review all offices; ordinary accounts see only their own
requests, and assigned staff see their office requests. Demoting a reviewer removes
staff access. Changing assignments takes effect immediately. Archive an office to
exclude it from new requests; historical steps retain their office name and route.

The demo starts with Registrar, Cashier, and Library, plus sample Leave request,
Schedule change, and School permit form types. Their one-office routes are examples,
not official policy. Production starts without these sample workflows. Configure
real offices, published forms, and the active school year before accepting requests.

Students submit one active clearance per active school year. Its offices review
independently; all must approve to complete clearance. Forms follow their configured
sequence: a later office cannot approve before earlier offices. A return requires an
explanation, and students can edit details and resubmit. Earlier details and office
decisions remain in history; only returned steps reopen. Approval does not calculate
or establish an official balance, payment, or academic eligibility.

Anyone with an active account can send a helpdesk concern to a school office.
Assigned staff can reply and resolve, return for information, or reject the concern.
Internal staff notes never appear in the requester's conversation. New requests,
resubmissions, public replies, and office decisions produce private in-app notices.
Duplicate creation keys and revision checks prevent repeated submissions and stale
decisions. Terminal requests cannot be cancelled or silently reopened. Requesters
cannot approve their own requests, including accounts that also hold office access.

### Targeted announcements

Admins publish by role, course, and section. Teachers may publish only to Students
or Parents in their assigned sections. The overview and announcement feed apply
these filters server-side; parents use their linked child's section/course. The old
announcement management links redirect to the shared publishing screen. Academic
admins can see all announcements for administration.

### Rotating class QR

A Teacher opens a window for an assigned schedule; Admins can manage any schedule.
A window lasts 15 minutes, its code expires after 30 seconds, and the teaching screen
refreshes it. Students must sign in, belong to the schedule's section, and explicitly
confirm check-in. The QR token uses a URL fragment so the camera link does not send
it in the initial server URL; it is submitted in a CSRF-protected form.

Each student can check in once per window. Existing attendance for that class/date
is preserved; the teacher handles corrections. Codes fail after expiry or closure.
Workspace restore closes all restored windows, requiring teachers to open fresh ones.
Rotating codes reduce stale sharing; they do not prove location, prevent all live
sharing, or implement facial recognition. Staff ID scanning remains available.

### Issued document verification

On a Ready for pickup or Released document request, authorized office staff can
register the final school-issued PDF (up to 2 MB). It receives a public verification
QR and a SHA-256 fingerprint. Only the requester and authorized staff can download
the actual file. Requirement submissions and claim stubs are distinct from this
registry and do not become issued academic documents.

The public page discloses the issuing school, document type, registration date,
fingerprint, and revocation status. It does not disclose student names, student
numbers, request notes, or the private file. A verifier can compare a PDF's fingerprint
locally in the browser; the PDF is not uploaded. A copied QR on different content does
not authenticate that content. This is a school-controlled registry, not a digital
signature, automatic academic-document generator, or external accreditation check.

Staff can revoke registrations with a reason. Revocation remains visible publicly,
and replacements retain history. There may be one active registration and at most
five historical registrations per request. Issued PDFs and requirement submissions
share the demo's existing 8 MB aggregate raw-file budget and 500-file/version limit.
Their bytes, hashes, and registry history are included in complete workspace backups.

### Account MFA

Account security supports standard six-digit, 30-second TOTP authenticator apps.
Setup requires the current password and a valid authenticator code. The setup key
is shown only in the authenticated setup screen. Enabled secrets are encrypted with
libsodium secretbox; the private key is `.campus-security.key` beside the academic DB.
Never publish this file, session files, backups, setup keys, or recovery codes.

Enabling MFA revokes earlier sessions. Future password sign-ins require a second
step; challenges expire after five minutes, failures are rate-limited, and a TOTP
step cannot be reused. Ten high-entropy recovery codes are shown once, stored only
as hashes, and consumed on use. Replacing them requires password plus a valid second
factor and invalidates the previous set. Disabling MFA also requires both factors.
MFA is optional for the free demo and required for Admin/Super Admin in production;
production admins without enrollment are directed to setup and cannot use queue APIs.

Complete workspace backups include the security key required to restore encrypted
MFA. Archives containing enrolled accounts but missing or mismatching that key are
rejected before replacement. Safety copies preserve the prior key too. Keep full
backups private, since they contain both records and account recovery material.
There is no email/SMS MFA or password-only bypass for an enrolled account. Keep
recovery codes securely; they are the intended fallback for a lost authenticator.

### Scheduled local backups and Free Render

The launcher starts `scripts/backup-worker.mjs`. While running, it makes its first
complete local snapshot after one minute, then every 24 hours by default. The newest
seven snapshots are retained. Files are written privately (0600) via a temporary file
and rename. Retention removes only files generated by this worker. Manual downloads
and automatic snapshots use the same archive format and restore validation. Snapshot
creation and workspace restoration are serialized; academic SQLite and queue JSON
remain separate stores, so use a quiet period for operational backups.

Super Admin's Backup workspace page shows the latest successful local snapshot and
any recorded failure. Download a private full backup before redeploying. **Free Render
storage, including local snapshots on the same disk, is ephemeral. A sleeping service
cannot run scheduled work. Local snapshots are not off-site backups.** The current
scope intentionally retains the user's free demo hosting choice.

Optional process settings:

| Variable | Default | Purpose |
| --- | --- | --- |
| `CAMPUS_DATA_DIR` | ignored `.runtime/data` locally; `/app/data` in Docker | Private data root; point at durable storage for a separately provisioned persistent deployment |
| `CAMPUS_BACKUP_DIR` | `backups` beside the academic DB | Private local snapshot destination, outside all public roots |
| `CAMPUS_BACKUP_INTERVAL_MINUTES` | `1440` | Interval while the worker is alive; minimum five minutes |
| `CAMPUS_BACKUP_KEEP` | `7` | Retain 2–30 generated snapshots |

An S3/off-site provider, malware scanning, durable storage, background web push,
email, and SMS are not connected by this batch. These require separate service
configuration and are not promised by the free hosting setup.

## Validation

Use a fresh disposable `CAMPUS_DATA_DIR`. These HTTP/browser suites change records
and restore their initial workspace; run them sequentially and before smoke tests
create active document tickets. Restoration invalidates sessions. The tests use the
repository's development fixtures; never print credentials, MFA keys, recovery
codes, or backup contents.

```sh
/workspace/tools/dotnet/dotnet run --project tests/AssistantChecks/AssistantChecks.csproj
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/campus-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/campus-browser.cjs
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_DATA_DIR=/path/to/disposable-data python3 scripts/backup-test.py
```

Campus HTTP checks cover office ownership, sequential/parallel review, internal-note
privacy, revision checks, targeted audiences, actual QR expiry/rotation, registry
privacy/revocation, encrypted MFA setup, TOTP replay, recovery replacement, and full
MFA backup/restore. Browser checks cover desktop/mobile screens, class QR rendering
and check-in, document fingerprint matching/mismatch, and revoked registrations.
Backup-worker checks cover execution, integrity, private modes, retention,
unrelated-file preservation, and restoration of an actual generated snapshot.
Existing academic/account, governance/exchange, queue, upload, live, and PWA suites
remain applicable.
