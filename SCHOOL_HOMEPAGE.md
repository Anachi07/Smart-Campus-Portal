# Public school homepage

The page at `/` is the public website for **Infotech College of Arts and Sciences (ICAS), Sucat branch**, using the name and branch supplied by the project owner. It displays HRS, IT, BSHM and BSIT separately, with program inquiries, school contact information and portal access. The school logo uses a transparent-background PNG. Its homepage container has no white fill, outer frame or gold top line. The install/loading icons use the same transparent logo. The page uses its own styles; signed-in academic and queue layouts are unchanged.

## Update school information

Sign in as Admin or Super Admin and open **School settings**. The public homepage has separate official school name and branch fields so that updating its identity does not rename the existing portal account interface. The default public identity is supplied in `sims/includes/public-school.php` and works without resetting an existing database.

Optional public fields include About, Mission, Vision, campus address, public phone/email, official Facebook and website links, office hours, and confirmed full names for HRS and IT. Empty optional fields and their sections are hidden. No contact numbers, street addresses, history, accreditations, admissions dates, mission/vision text or campus photographs have been invented. The existing demo-data notice remains in the footer.

Settings accept plain text, render escaped HTML, validate email and HTTPS URLs, reject credentials in links, and save all fields together. Only the explicit public-field list is read by the homepage; account data and private announcements are not published. Older clients submitting just school name and academic year preserve the new public fields. These values are stored in the existing settings table and included in paired backups. On free Render, download backups before losing ephemeral storage.

The homepage automatically adapts to the screen width. The manual Accessibility menu, standalone toolbar row and green top strip are removed. Native browser zoom and device display preferences remain available. The header contains the single primary portal sign-in button; the repeated green sign-in section is removed. The school assistant, public queue link in the footer and install controls remain available. Install controls appear in the public footer; their placement on signed-in pages is unchanged. The homepage and private pages are not added to the offline cache.

## Public enrollment information

School settings also offers reviewed enrollment requirements for new students/transferees and ordered enrollment steps, with explicit Draft/Published status. Drafts remain absent from the public page. Read [ADMISSIONS.md](ADMISSIONS.md) for publishing instructions and limits.

## Verification

PHP syntax checks, desktop/mobile screenshots, existing browser navigation checks and accessibility checks cover the new page. The public-settings integration test requires disposable data and restores its initial paired backup:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/school-home-test.py
```

It verifies the confirmed identity and four courses, public text escaping, contact URL/email validation, blank-field hiding, Admin permissions, CSRF and compatibility with basic settings clients.

## Confirmed Sucat contacts

The owner supplied the contact/location information used by the homepage: 8347 CAP Building, Dr. A. Santos Ave. (Sucat), Brgy. San Antonio, Parañaque City (in front of Nissan Sucat); telephone (02) 7-717-2982, 825-6531 or 473-4349; mobile/inquiries 0995-922-2204. The Sucat Facebook URL follows the supplied poster, https://www.facebook.com/icasparanaque. Mobile has a tap-to-call link; multiple landlines remain readable text. No office hours or email were inferred.

A versioned, atomic one-time settings initializer fills missing/blank contact fields on existing databases, preserving nonblank admin edits. Later edits or clears remain unchanged because the initializer records completion. Restoring an archive from before this contact profile applies the same backfill once. scripts/contact-profile-check.php verifies fill/preservation/clear behavior with an in-memory SQLite database; school-home-test.py verifies public rendering and editing.

## Campus photographs and loading

Read [GALLERY_LOADING.md](GALLERY_LOADING.md) for the original-photo upload controls and animated website loading logo. Real photos appear in the hero/gallery after upload; no synthetic campus images are seeded.
