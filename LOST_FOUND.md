# Lost-and-found portal

The sidebar's **Lost and found** link is available to signed-in campus members.
This module is independent of facility reservations, enrollment, and payments.

## Reports and safe listings

Members report Lost or Found items with a category, generic title, broad location,
date, and separate private identifying details. Reports begin Pending and are visible
only to the reporter and active Admin/Super Admin staff. Teachers do not automatically
receive lost-and-found moderation privileges.

Staff review and, if necessary, redact the title/location before publication. They
must confirm those fields contain no identifying marks, names, contact details,
account numbers, or claim answers. For a Found report, staff must also confirm the
item has reached the school desk. A generic listing becomes visible to other
signed-in members only after approval. Anonymous visitors cannot browse listings.
The search supports text, Lost/Found, category, and date filters.

Private identifying details and reporter identities are not part of the member
listing projection. Neither private descriptions nor claim evidence are copied into
general activity-log details or notification bodies. Moderators must still inspect
submitted text: the application cannot automatically determine whether a public
title reveals ownership evidence. This first increment uses text reports; no public
item photographs or uploaded identity documents are introduced.

## Private claims and handover

Members can claim a published, open Found item, except their own report. They give
private evidence such as unique markings or contents. Do not send passwords, PINs,
full ID numbers, or sensitive financial information. Evidence and claim history are
visible only to the claimant and Admin/Super Admin staff, including when the reporter
is the finder. A parent does not automatically see a linked student's claims.

There is one claim record per account/item. Repeated submissions open that claim
without replacing its evidence. Staff can request more information, verify evidence,
or reject with a reason. A request for information allows the claimant to submit
corrected evidence; previous evidence remains in the private claim history. Withdrawn,
rejected, and released claims are closed; contact the desk for any further review.
Claimants can withdraw Pending, Needs information, or Verified claims before release.

Only one claim may be Verified or Released for an item. Verification checks are
serialized and protected by a database uniqueness rule. Staff cannot review their
own claim. Release requires a currently Verified claim, a review note, and explicit
confirmation that the recipient was checked and the physical handover completed.
A portal verification alone never authorizes collection without the school's check.

Release marks the item Returned and closes other pending claims with a generic
outcome, without exposing the successful claimant's identity or evidence. Returned
items leave the open search list. Their report/claim histories remain private.
Staff can archive listings, but must resolve a held Verified claim first. Archiving
closes pending claims with a generic notice. A Lost reporter can mark their own
missing-item report resolved after recovery; Found reports require the verified-claim
handover flow instead.

Forms use CSRF protection. Revision checks reject stale moderation/review actions,
submission keys prevent duplicate reports, and daily report/claim limits bound abuse.
New reports, claims, and claimant responses notify school admins; review outcomes
notify the affected account privately. These are in-app notifications, not SMS,
email, guaranteed live alerts, or background push delivery.

## Records, backups, and operation

Report, claim, and history tables live in the existing private academic SQLite DB.
Full manual/scheduled backups include them. Restore validates columns, references,
report/claim states, private-history references, and competing verified claims.
Older backups without these tables remain supported through automatic initialization.
No example lost/found reports or real school custody policies are seeded. The school
sets its own desk location, proof standards, collection procedure, and retention rules.
Free Render remains ephemeral; download a private full backup before redeploying.

## Validation

Run sequentially on disposable sample data; both suites restore their initial archive
and invalidate sessions. Never run them against actual school reports.

```sh
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 python3 scripts/lost-found-test.py
CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/lost-found-browser.cjs
```

Checks cover hidden drafts and identifying details, search filters, desk receipt,
CSRF, claim ownership, finder isolation from proof, duplicate submissions,
correction/resubmission, simultaneous verification, stale/early release denial,
handover confirmation, other-claim closure, recovered Lost reports, backup/restore,
and desktop/mobile screens.
