# School FAQs in Ask ICAS

The homepage does not add a separate FAQ section. Open **Ask ICAS** and choose Enrollment, Courses, Campus location, Contact school or Account help. Existing visit/request tools remain available. These answers work in the rule-based school guide without a paid AI provider.

`sims/includes/assistant-school-faqs.php` builds public knowledge from the same School settings used by the homepage:

- The owner-confirmed HRS, IT, BSHM and BSIT programs; optional HRS/IT full names follow approved settings.
- Campus address and landmark, public telephone/mobile, email, validated Facebook/website links and office hours when entered. Missing office hours are explicitly unconfirmed.
- Enrollment steps and the relevant new-student/transferee requirements only when the existing public enrollment status is Published and its completeness check passes. Draft content is not exported as chatbot facts. Missing lists lead to school contact guidance.
- Existing student registration, Super Admin approval and password-help workflows. Chat cannot approve accounts or reset passwords.

Changing public settings updates subsequent chatbot answers without copying records into the FAQ table. The internal PHP knowledge endpoint remains protected by the existing server secret. Existing published custom FAQs from the **Assistant knowledge** page continue to be available; private/draft FAQs, student records, photos and credentials are not exported by the new school-fact helper. Historical poster wording is not treated as proof that admissions are currently open. Tuition amounts or unconfirmed policies are not invented.

No new external service, API key, schema, homepage layout or navigation is added. If a model provider is already configured, existing grounding, source citations, budget and safety fallbacks still apply.

## Verification

Use disposable data and run sequentially because both HTTP suites restore their initial backup:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/assistant-school-faq-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/assistant-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/assistant-browser.cjs
```

The new suite checks common English/Tagalog questions, exact source selection, dynamic contact updates, published enrollment and hidden drafts. The existing assistant suites cover custom FAQ publishing, credential guards, confirmed reservations and mobile/desktop UI. Run with provider keys unset so tests make no paid model calls.
