# ICAS Assistant

The assistant answers questions about implemented portal services and published school facts. With no provider credentials, it is labelled **School guide** and returns approved information directly. It does not pretend a language model is connected. Scheduling works independently of an AI provider.

## School information

Administrators open **School administration → Assistant knowledge**. Add a question, its approved answer, and a source label. Draft entries are private; publish only school-approved information. Unpublished entries are never supplied to the public assistant. Do not put passwords, access keys, private student details, or payment credentials in this knowledge base. The initial facts cover portal workflows, not official tuition amounts, contact details, document requirements, or office hours.

## Optional generative AI

The deployment Blueprint selects Google AI Studio (Gemini), with `gemini-2.5-flash` as its initial configurable model and a per-process daily cap of 100 calls. Model availability must be confirmed in the school's Google AI Studio project; change the model setting if that account no longer supports this ID. Live provider access is not verified by the Blueprint.

For the existing service, open Render **Environment** and ensure `CAMPUS_AI_PROVIDER=gemini` and `CAMPUS_AI_MODEL=gemini-2.5-flash` are present (or use a supported model from AI Studio). Add a newly generated Google AI Studio key as `CAMPUS_AI_API_KEY`, then save and redeploy. Existing Blueprints may require **Manual sync** for the non-secret defaults. The key is deliberately absent from the repository and Blueprint: enter it only in secure hosting settings. Revoke any key previously shared in a chat.

To use another supported provider or override the defaults, configure:

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

## Rescheduling and voluntary exchanges

In **Ask ICAS → My requests**, an upcoming unchecked-in visit offers **Change visit time** and **Request time exchange**. Changes close one hour before the current visit. Available alternatives follow office hours, booking limits, capacity, and the user's saved classes. The original time is released only after a confirmed, successful change; a stale confirmation is rejected.

For an occupied time, the owner can explicitly offer their existing reservation in exchange. Eligible holders at that time receive an **in-app invitation**, accessible in **Ask ICAS → Exchange requests** and the Notifications page. No email or SMS is sent. Names, contact details, student references, and the other person's ticket number are not shared. Invitations expire within 24 hours or one hour before either visit, whichever comes first. No response means no change; the requester can withdraw and the recipient can decline.

Acceptance requires a second explicit confirmation. The server rechecks both active accounts, both authoritative saved class schedules, current reservation states, office booking settings, and capacity. Both times are updated under one store lock and persisted together. When multiple eligible holders share a time, the first valid acceptance wins and the other invitations close. Cancelling, checking in, or changing an involved reservation invalidates that participation. Changed school schedules or office rules can prevent a previously proposed exchange. AI text cannot grant consent or exchange reservations automatically.

## Service workflow integration

The assistant's booking form previews school-published requirements and links each reserved visit to **Requirements & progress**. Office closures are applied by the same slot engine used for booking, changes, and exchanges. Document processing, claim stubs, reminders, feedback, and reports are documented in [CAMPUS_SERVICES.md](CAMPUS_SERVICES.md). Model text does not verify requirements, release documents, confirm payments, or submit feedback automatically.
