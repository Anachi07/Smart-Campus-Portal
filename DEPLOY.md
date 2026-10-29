# Live demo on Render

The repository includes a Docker image and `render.yaml` for a single-service demo.

1. Sign in to Render and connect your GitHub account.
2. Open **New → Blueprint** and select `Anachi07/Smart-Campus-Portal` (`main`).
3. Review the `smart-campus-portal-demo` free web service and create it.
4. Wait for build, deployment, and health checks. Render supplies the actual public HTTPS URL; open its `/portal` path.

The Blueprint selects a free service; inspect the provider's current terms before creating it. No custom domain is required to explore the demo. The public URL is assigned by Render and cannot be known before deployment.

## Demo behavior

The free setup uses an ephemeral filesystem. Records and queue tickets can reset when the service is replaced or restarted. Free services may sleep when idle. Use sample data only. This is a demo, with privately managed sample accounts, not a school production deployment.

The app exposes only the gateway port. PHP and ASP.NET Core listen internally. `/health` checks the queue application and its PHP session service. HTTPS cookies are enabled through `CAMPUS_PUBLIC_HTTPS=1`; leave it unset when testing over local HTTP.

For durable data, use hosting with a persistent volume and set `CAMPUS_DATA_DIR` to that mount. The launcher copies the sample databases only if target files do not already exist; existing records are preserved. Render persistent disks require an eligible paid service. This Blueprint does not request a disk or a paid plan.

## Container validation

```sh
docker build -t smart-campus-portal .
docker run --rm -p 8080:8080 smart-campus-portal
```

Optional durable local storage:

```sh
docker run --rm -p 8080:8080 -v campus-data:/app/data smart-campus-portal
```

A local container is accessible through local port 8080. Public hosting must be created before a public URL exists. Google indexing is separate from deployment; private academic pages should remain behind login.

The container image builds the .NET SDK from Microsoft's official archive and verifies its SHA-512 checksum before extraction. The final image includes only the .NET runtime, PHP, Node, and the application. The cloud build optionally accepts a `system_ca` BuildKit secret for its configured proxy trust; regular hosting builds do not need that secret.

## Validation performed

The container was built with checksum verification enabled and tested through its gateway: shared admin/student login, academic pages, queue role restrictions, ticket creation and search, admin call-next, shared logout, private-file blocking, local assets, and `/health`. These local checks do not establish that a Render service has been created or that a public URL is live.

## Updating the existing free demo

The Blueprint stays on `plan: free` and explicitly sets `CAMPUS_MODE=demo`. Pushes to the connected `main` branch can trigger Render's automatic deploy if automatic deployment is enabled. A successful Git push does not confirm that Render's build or deployment has completed; use the service's Events/Logs and `/health` to confirm it.

Before updating, download **Backup workspace** in the admin portal if you need existing demo changes. After a redeploy on ephemeral storage, sign in to the fresh demo and use **Restore workspace** to upload the full JSON backup. Academic records and queue data are restored together. Restore requires typing `RESTORE`, validates the files, creates safety copies, and signs out existing sessions.

The ICAS release uses a green, white, and yellow school theme with the supplied-reference logo asset. Public, academic, and Registrar/Cashier interfaces share the same school identity. No real student data should be entered into this free demo.

## Assistant configuration

See [ASSISTANT.md](ASSISTANT.md) for published school facts, appointment rules, and optional server-side AI configuration. No provider key is committed or required for school-guide answers and queue scheduling.

## Super Admin upgrade

Back up before redeploying. The first upgrade promotes the oldest active Admin account to Super Admin once and preserves its existing login. Sign in again after deployment. Account creation, approvals, role changes, password resets for other users, and full backup/restore move to **Account governance → Accounts & approvals** (and its adjacent backup links). Regular Admin remains available as a separate role the Super Admin can assign. Pending self-registrations cannot sign in until approved. Old backups without the Super Admin migration marker are migrated on first use; migrated backups must contain an active Super Admin.
