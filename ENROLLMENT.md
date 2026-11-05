# Enrollment checklist

This module tracks school-published requirements, student preparation, and office
verification. It does not confirm admission, enrollment, payment, balances, or
academic eligibility. No official enrollment requirements are seeded.

## School setup

Super Admin opens **Enrollment requirements** and publishes requirements for a
school year, an optional course (blank means all courses), and a reviewing office.
Each definition has a title, instructions, required/optional flag, and published
flag. The school must have an active school year, and the student must have an
assigned course before starting. Inactive offices are excluded from new checklists.

Use **Offices & approval routes** to assign an active Admin or Teacher as an office
reviewer. Super Admin can review all offices. Changing a reviewer applies to existing
checklists; editing or archiving a requirement affects future checklists only.
Existing checklists preserve their titles, instructions, requirement flags, course,
school year, and office-name snapshots. If a student's course changes after starting,
contact the school: this first module does not automatically rebuild the snapshot.

## Student and office workflow

Students open **Enrollment checklist**, then start the active year's checklist.
Repeated starts open the same checklist. A checklist cannot start without published
requirements applicable to that course/year. Marking items prepared does not verify
them. Assigned offices verify actual requirements or return them with a reason.
A returned item becomes pending review when the student marks the corrected item
prepared again. Feedback remains in history. Verified items cannot be unchecked by
the student; the office can return an item again if a correction is necessary.

Progress counts required office-verified items. Optional items remain visible but
are not included in that denominator. An optional-only checklist does not display
required-checklist completion. Read the next-step instructions and ask the school
for actual enrollment confirmation, even after all required items are verified.

Students see only their own checklists. Parents can read their linked student's
checklist, without editing it. Assigned active Admin/Teacher reviewers and Super
Admin can review; other staff and students cannot access unrelated checklists.
Every form uses CSRF protection. Revision checks reject stale updates, and review
results create private student notifications and history entries.

This first enrollment increment tracks preparation and office review. Existing
private document uploads stay in **My service requests**; enrollment checklists do
not introduce a second upload store. Contact the reviewing office for the correct
submission process rather than assuming every requirement requires a file upload.

## Storage and validation

The four enrollment tables are in the existing private academic SQLite database,
so manual and scheduled complete workspace backups include definitions, snapshots,
feedback, and history. Restore validates their columns, references, and item states.
Older backups without enrollment tables remain supported through automatic schema
initialization. Free Render storage remains ephemeral; keep private downloaded
backups before redeploying.

Run on disposable data only, sequentially with other restore suites:

```sh
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 python3 scripts/enrollment-test.py
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/enrollment-browser.cjs
```

Coverage includes publication/course targeting, repeated starts, student and parent
privacy, assigned review, preparation versus verification, correction/resubmission,
stale updates, immutable snapshots, full backup/restore, and desktop/mobile screens.
Tests restore their initial disposable workspace and invalidate its sessions.

The school's course choices are HRS, IT, BSHM, and BSIT. Historical demo profiles
outside those offerings must have their real course confirmed by school staff before
starting a new checklist; historical checklist snapshots remain readable. Automated
tests assign IT only in their disposable fixture copies, not to actual school users.
