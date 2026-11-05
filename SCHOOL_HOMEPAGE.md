# Public school homepage

The page at `/` is the public website for **Infotech College of Arts and Sciences (ICAS), Sucat branch**, using the name and branch supplied by the project owner. It displays HRS, IT, BSHM and BSIT separately, with program inquiries, school contact information and portal access. The existing supplied school logo is retained. The page uses its own styles; signed-in academic and queue layouts are unchanged.

## Update school information

Sign in as Admin or Super Admin and open **School settings**. The public homepage has separate official school name and branch fields so that updating its identity does not rename the existing portal account interface. The default public identity is supplied in `sims/includes/public-school.php` and works without resetting an existing database.

Optional public fields include About, Mission, Vision, campus address, public phone/email, official Facebook and website links, office hours, and confirmed full names for HRS and IT. Empty optional fields and their sections are hidden. No contact numbers, street addresses, history, accreditations, admissions dates, mission/vision text or campus photographs have been invented. The existing demo-data notice remains in the footer.

Settings accept plain text, render escaped HTML, validate email and HTTPS URLs, reject credentials in links, and save all fields together. Only the explicit public-field list is read by the homepage; account data and private announcements are not published. Older clients submitting just school name and academic year preserve the new public fields. These values are stored in the existing settings table and included in paired backups. On free Render, download backups before losing ephemeral storage.

The accessibility toolbar, school assistant, public queue link and install controls remain available. Install controls appear in the public footer; their placement on signed-in pages is unchanged. The homepage and private pages are not added to the offline cache.

## Verification

PHP syntax checks, desktop/mobile screenshots, existing browser navigation checks and accessibility checks cover the new page. The public-settings integration test requires disposable data and restores its initial paired backup:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/school-home-test.py
```

It verifies the confirmed identity and four courses, public text escaping, contact URL/email validation, blank-field hiding, Admin permissions, CSRF and compatibility with basic settings clients.
