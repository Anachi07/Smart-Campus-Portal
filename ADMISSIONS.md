# Public enrollment requirements and steps

Admin or Super Admin opens **School settings → Public enrollment information** and enters school-approved content:

- New student requirements: one item per line.
- Transferee requirements: one item per line.
- Enrollment steps: one step per line, in order.
- Optional enrollment notes, shown as plain text.

Save as **Draft** while preparing the information. Draft is the default; no official requirements, fees, schedules or enrollment procedures are seeded. Draft content is absent from the public HTML, not merely hidden with CSS. To publish, provide steps and at least one applicant requirements list, select **Published**, and save. If one applicant list is still blank, visitors are directed to contact the school for that applicant type. Choose Draft and save to withdraw the entire section.

Publishing shows an Enrollment navigation link, requirement lists and numbered steps on the homepage. The hero inquiry link leads to this section when published, or to school contact information while unpublished. The section is informational: it does not submit an application, upload student documents, collect payment or replace the private student enrollment checklist.

Only school staff who already have Admin/Super Admin settings access may change this public content. All text is escaped; lists support up to 30 nonblank lines, with 500 bytes per item and the field limits shown by the form. Blank lines are ignored. Invalid publication attempts leave all previous settings intact. Older settings clients preserve these fields when omitted. The existing settings table and paired backups store the content; no schema migration, external service or paid resource is required.

## Verification

Use disposable data and run sequentially; both tests restore their starting paired backup:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/admissions-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 node scripts/admissions-browser.cjs
```

Tests cover permissions, CSRF, draft source privacy, publish completeness, escaped copy, ordered steps, atomic validation, backup restore, compatibility, unpublishing and public layouts at 320/390/768/1440px. Test examples are restored afterward and never become production school policy.

## Supplied admission poster

The project owner supplied a school poster headed “Admission Open for 2026–2027,” with Sucat campus identification. The default public admission announcement quotes that claim and directs visitors to confirm current schedules; it does not assert that admissions are still open today. Admin can edit or clear this independent public notice in School settings. Clearing it hides the notice. Existing saved notice values are preserved.

The poster does not supply official requirements or enrollment steps, so their separate Draft/Published workflow is unchanged. It labels the college block “Marcos Hi-way”; those program promotions, free-tuition/no-exam claims are not copied as Sucat-specific facts. Contact details were added separately after the owner supplied clear contact/location information; see SCHOOL_HOMEPAGE.md. The four existing course codes remain based on the owner's earlier explicit instructions. No tuition amounts, application procedure or admissions deadlines are inferred.
