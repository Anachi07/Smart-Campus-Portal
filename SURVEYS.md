# School feedback surveys

Open **School surveys** in the sidebar. Admin and Super Admin accounts create a draft, review its question preview, then publish it. No surveys or official school questions are seeded. Surveys accept one submission per active eligible account, with all questions required.

Choose an audience: All, Student, Parent, Teacher, or Admin (includes Super Admin). Optional course and section filters apply only to Student or Parent audiences; parent eligibility follows the linked child. Offered courses remain HRS, IT, BSHM and BSIT. Historical course assignments are not remapped.

Questions use one line per question, with at most 15 questions:

```text
rating|How was the service?
choice|Preferred session?|Morning|Afternoon
text|What can we improve?
```

Ratings run from 1 (lowest) to 5 (highest); choices support 2–8 distinct options. Text accepts up to 1000 characters. The pipe character separates fields and cannot be used inside question labels or choices. Privacy, questions and audience are fixed at creation. Create a replacement draft for corrections. Publishing opens submissions; closing permanently stops them. There is no automatic opening/closing schedule.

## Privacy and results

Only Admin and Super Admin can see results. Participants see their own participation status, but no response listings or results.

- **Identified:** answers retain the submitting account ID; staff see names and submitted answers.
- **Anonymous:** answers have a random response ID and no user ID or submission timestamp. A separate `(survey_id, user_id)` participation table prevents duplicate submissions without linking directly to responses. Results appear only after permanent closure with at least three responses. Rating and choice totals and free-text responses are shown without respondent rows or participation lists. Closing below three responses leaves results hidden permanently.

This is a privacy safeguard, not guaranteed anonymity. Small groups, identifying text, server access logs and direct database or backup access can permit inference. The form explains this before submission. Do not request passwords, financial credentials or identifying details in anonymous feedback. The module does not add answer content or response identifiers to account activity logs. It does not provide downloadable respondent exports.

Survey tables are included in existing private paired backups, with integrity and response-privacy validation during restore. Existing archives without survey tables remain supported. Free Render storage is ephemeral; download backups to keep a durable copy.

## Verification

Use a disposable data directory, then run these sequentially (both restore the original snapshot):

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/surveys-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 node scripts/surveys-browser.cjs
```

The integration suite covers answer validation, duplicate submissions from independent sessions, role/course/section eligibility, linked parents, privacy freezes, closed surveys, the anonymous reporting threshold, escaped text and backup/restore. The browser suite covers draft creation, publishing, mobile submission, staff reporting, and desktop/mobile overflow.
