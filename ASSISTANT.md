# ICAS Assistant

The assistant answers questions about implemented portal services and published school facts. With no provider credentials, it is labelled **School guide** and returns approved information directly. It does not pretend a language model is connected. Scheduling works independently of an AI provider.

## School information

Administrators open **School administration → Assistant knowledge**. Add a question, its approved answer, and a source label. Draft entries are private; publish only school-approved information. Unpublished entries are never supplied to the public assistant. Do not put passwords, access keys, private student details, or payment credentials in this knowledge base. The initial facts cover portal workflows, not official tuition amounts, contact details, document requirements, or office hours.

## Optional generative AI

A school administrator must create a provider account and credential. In the existing Render service's **Environment** settings, configure these server-side values, then redeploy:

| Variable | Value |
| --- | --- |
| `CAMPUS_AI_PROVIDER` | `gemini` or `openai` |
| `CAMPUS_AI_MODEL` | A model ID supported by that provider and account |
| `CAMPUS_AI_API_KEY` | The provider credential, entered only in secure hosting settings |
| `CAMPUS_AI_DAILY_LIMIT` | Optional per-process daily request cap; default `100` |

Gemini uses the generateContent API; OpenAI uses Chat Completions with JSON responses. Model support and pricing depend on the provider. Do not commit credentials, paste them into chat, or put them in browser JavaScript. No paid provider or account is created automatically. The per-process cap resets on restart and is not a billing guarantee; configure provider-side quotas too.

Only relevant published facts and the submitted conversation are sent to the selected provider. Personal class schedules and student records are not included in model prompts. Conversations are not persisted by this app. Provider retention policies still apply. Answers must cite supplied facts; unsupported, malformed, or failed responses fall back to the school guide. Unknown school policies are referred to the school office. Live provider access has not been tested without a real key; automated tests use a simulated transport.

## Queue visits

Signed-in users can select Registrar document requests or Cashier fee/tuition transactions, compare available times, review details, and explicitly confirm a reservation. Chat text alone never creates a booking. The assistant recommends times with more remaining capacity, then earlier times. Saved class schedules are excluded with a 15-minute buffer. Users must still check personal commitments; recommendations do not predict actual crowd levels.

Administrators manage booking days, hours, slot duration, capacity, and advance-booking days in queue **Settings**. Defaults are portal configuration, not verified school office hours. Upcoming reservations appear under **Appointments**. Slots require a configured counter for the relevant office.

Users check in from 15 minutes before until 30 minutes after their reserved time. Check-in joins the waiting queue; it does not guarantee immediate service. Missed reservations expire. Users can cancel their own scheduled or waiting requests. Capacity and ownership are checked on the server, and repeated confirmation requests do not create duplicate bookings.

This does not process payments, issue receipts, release documents, or confirm tuition balances. Staff perform these actions through the school's actual processes.

## Verification

Use disposable data for these checks; the HTTP suite creates and restores records.

```sh
/workspace/tools/dotnet/dotnet run --project tests/AssistantChecks/AssistantChecks.csproj
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/assistant-test.py
CAMPUS_TEST_URL=http://127.0.0.1:18080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/assistant-browser.cjs
```

The free Render demo still has ephemeral storage. Back up school facts and reservations before redeploying. Remove sample data and change all initial account passwords before considering real users; hiding account hints does not change existing passwords.
