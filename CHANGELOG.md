# Demo release 1.1

The campus demo now has a consistent responsive shell, public home, shared sign-in, clear role navigation, and real record-based dashboards. Final logo/design assets remain pending.

Account and report permissions were tightened. Self-registration cannot create staff, teachers are limited to assigned classes, unlinked students/parents no longer fall back to another student's profile, and tickets are visible only to their owner or authorized queue staff. Password changes and restores invalidate older sessions.

Academic forms now validate grades, dates, linked records, credentials, and administrator availability. Attendance saves update existing daily class entries. Academic insights use documented GPA/attendance rules; fabricated confidence percentages, random dashboard figures, and unsupported AI claims were removed from operational screens.

Queue writes are atomic, corrupt files are not silently overwritten, issue numbers include the date, duplicate submissions reuse an active ticket, and counters enforce one serving ticket with valid state transitions. Phone logs explicitly do not claim SMS delivery.

Full-workspace backup/restore now validates both data stores, creates safety copies, reloads the queue, and requires a deliberate restore confirmation. PDF reports paginate instead of drawing records beyond a single page.

The Render service remains a free sample-data demo. Persistent production storage and final school branding are not included in this release.

Daily queue handling also expires unresolved tickets from earlier dates, and the tracker refreshes automatically when its tab is visible. New student profiles no longer start with fabricated attendance/GPA figures; summaries are calculated from recorded class data. Migration recalculates the original sample grade GPAs using the documented grading scale.

# ICAS school branding and Registrar/Cashier update

- ICAS Philippines identity, supplied-reference logo asset, and green/white/yellow university-style interfaces across public and signed-in pages.
- Dedicated document requests, school fees, and tuition queues with transaction details, student/reference number, office notes, validation, and clear receipt/payment disclaimers.
- Registrar/Cashier counter assignments, office-aware call-next and waiting estimates, separate configurable tuition service time, and a public display that shows all serving offices.
- Browser coverage now includes the student services form, its dynamic transaction options, and mobile layouts; regression checks cover office routing and student-reference requirements.
