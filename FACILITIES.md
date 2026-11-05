# School courses and facility reservations

The school's offered course choices are **HRS, IT, BSHM, and BSIT**. IT and BSIT are
separate records. The application does not guess expanded names for HRS or IT.
Existing demo courses outside this catalog remain historical, with linked records
preserved. They are excluded from new course choices; an existing historical profile
can retain its current assignment while staff correct other details. New assignments
must use the offered catalog. No student is automatically moved to a different
course. Super Admin should confirm historical students' actual course before they
start a new enrollment checklist.

## Configure facilities

Super Admin opens **Manage facilities** and adds the school's actual Room,
Laboratory, or Equipment records, location, capacity information, reviewing office,
approval requirement, and availability. No rooms, capacities, or official schedules
are assumed or seeded. Use **Offices & approval routes** to assign a staff reviewer.
Super Admin can review all resources. Archive a resource to stop new requests while
retaining existing bookings. Create a new record for a different physical resource.

Reservations are exclusive for one resource at a time, including equipment. Capacity
is information; this module does not sell seats or reserve individual units from a
shared inventory pool. Configure separate records for individually reservable units.

## Reserve, review, and cancel

Active students and staff use **Facility reservations**, select a resource, and give
future start/end times and a purpose. Times are interpreted and displayed in
Asia/Manila, stored in UTC, and limited to a start within 90 days and a duration of
up to eight hours. Pending and approved bookings both hold their interval. Adjacent
bookings are allowed; overlapping ones are denied. A database write lock serializes
conflict checks so concurrent submissions cannot claim the same resource/time.
Submission keys prevent duplicate creates.

An assigned active Admin/Teacher reviewer or Super Admin can approve or reject a
pending booking, or cancel an approved booking. Reviewers cannot approve their own
requests. Rejection and office cancellation need a reason. Requesters can cancel
their own pending/approved bookings before the start, freeing the time immediately.
Revision checks reject stale actions. Reservations whose start has passed are kept
as history and cannot be changed through this screen. Approval, cancellation, and
history describe reservations, not proof that a facility was actually used.

The daily calendar shows reserved intervals, resource names, and approval status to
signed-in students/staff. Requester identities and purposes are available only in
private request details to the owner and authorized reviewers. Parent accounts do
not reserve facilities. Resource availability is checked again at submission; the
calendar is not a guarantee that a slot stays free. School holidays, business hours,
equipment checkout, damage charges, recurring bookings, and maintenance calendars
are not inferred by this first module. Staff can archive unavailable resources or
cancel affected reservations with an explanation.

Definitions, reservations, and their history are in the private academic SQLite DB
and included in existing manual/scheduled backups. Restore checks references,
states, dates, and overlapping active intervals. Free Render remains ephemeral;
download a private full backup before redeploying.

## Checks

Run sequentially on disposable sample data only. These suites restore their initial
workspace and invalidate sessions.

```sh
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 python3 scripts/course-catalog-test.py
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 python3 scripts/facilities-test.py
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/facilities-browser.cjs
```

Coverage includes the exact offered catalog, preserved historical assignments,
permissions, private purposes, duplicate requests, approval/cancellation, stale
updates, overlapping concurrent requests, adjacent slots, invalid times, resource
archiving, complete backup/restore, and desktop/mobile reservation screens.
