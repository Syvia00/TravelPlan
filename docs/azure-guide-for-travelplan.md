# Azure in TravelPlan: what we use, why each plan, what it costs

This is the project-specific guide. It covers only what TravelPlan actually
uses, and for each piece it explains why that plan was chosen, what it costs,
and how to change it later. The general learning guide (azure-general-guide.md)
explains every plan and tier in Azure; this one applies that to our project.

Written on 5 October 2026 from the project history and Microsoft's docs. The
resource names and settings below come from reports during the build, so they
should be checked against the live setup (the last section has a prompt for
that). Prices change, so treat dollar figures as rough.

---

## 1. Where things stand

- Subscription: "Azure subscription 1", on the free trial. The trial credit is
  A$252.73 and it expires on 17 October 2026.
- Actual cost for 1 to 5 October: A$3.66, about A$0.75 a day. The App Service
  plan is A$2.56 of that and SQL is A$1.10.
- The cost forecast of A$121.85 is misleading right now. It was projected from
  September's spike, before the free database existed. Judge by the actual
  figure and recheck in a week.
- Budget: A$25 a month on rg-travelplan, with email alerts. It only warns, it
  does not stop anything.
- The one recurring cost that cannot be avoided is the App Service plan, about
  A$0.50 to A$0.60 a day (roughly A$16 to A$18 a month). Everything else is on
  a free tier or a free allowance.

---

## 2. How a request flows through the resources

1. The browser loads the Blazor app from the Static Web App. It is just static
   files, nothing runs on a server for each page view.
2. When the user clicks log in, the browser goes to the Entra External ID
   tenant (travelplansy.onmicrosoft.com). Entra handles email or Google sign
   in and hands back a signed token (a JWT).
3. The browser calls the API on App Service with that token. The API checks the
   token's signature, so it never has to ask Entra again.
4. The API reads and writes the free SQL database. Only the API talks to the
   database. The browser never does.
5. For some features the API calls outside services: Azure Maps for map
   images, the Frankfurter website for exchange rates. Their keys live only on
   the server.

The point of this shape is that secrets (database password, Maps key) exist
only on the server side.

---

## 3. Each resource, and why this plan

### Resource group: rg-travelplan
- A folder that holds everything. It costs nothing.
- Its location is East US. The group's location only says where its metadata
  is stored, not where resources inside it run.
- Most resources ended up in Central US — SQL provisioning was rejected in
  other regions on this new subscription. The Azure Maps account is the one
  exception: it is in East US, not Central US (verified 6 October 2026;
  an earlier version of this guide said all resources were in Central US).

### App Service plan travelplan-plan and App Service travelplan-api-dac4a409
- Plan: Basic B1, Linux. B1 is one vCPU, 1.75 GB of memory and 10 GB of
  storage, on a dedicated virtual machine.
- **Always On is off, by design, not an oversight** (checked live,
  6 October 2026 — `az webapp config show` reports `alwaysOn: false`). B1
  supports Always On; it is deliberately left off. Without it, the app
  idle-unloads after a period of inactivity and the next incoming request
  triggers a cold start — and both background jobs run their first iteration
  immediately on every cold start (see below), before waiting out their
  configured interval. The decision made here (6 October 2026) is to lean
  into that rather than fight it: whoever visits triggers a fresh run of both
  jobs, so data is current when someone is actually looking at it, and
  Always On is not paid for (in free-database wake-up budget, not dollars —
  B1's price is the same either way) to keep jobs running on a fixed clock
  when nobody is around to benefit from it. Turning Always On on remains an
  option — `az webapp config set --name travelplan-api-dac4a409
  --resource-group rg-travelplan --always-on true` — but would need the SQL
  budget in this section re-checked first, since a job interval at or below
  the database's auto-pause delay can keep it permanently awake instead of
  saving anything (see the SQL bullet below).
- App: runs the ASP.NET Core API on .NET 10.
- Why B1: it is the cheapest tier with dedicated compute. The Free tier shares
  a machine, has a daily CPU quota, and the app is stopped when that quota is
  used up. The API has background jobs and renders reports, so that is risky.
  B1 also supports custom domains and TLS, which Free does not.
- What B1 does not give us: autoscale rules and deployment slots. Both start
  at Standard. B1 can only scale out manually, up to three instances. Earlier
  versions of the docs said the API autoscales. That was wrong for B1.
- Cost: billed for as long as the plan exists. Stopping the app does not stop
  the charge. Only deleting the plan or moving it to Free does.
- Switching tiers is easy, see section 6.

### SQL server travelplan-sql-hda1thty and database TravelPlanDb-free
- The server is only a container. It costs nothing.
- The database is on Azure's free SQL Database offer: serverless General
  Purpose, with the first 100,000 vCore-seconds and 32 GB of storage free every
  month, renewing monthly for the life of the subscription.
- Why: the first paid database cost most of the money in September. The free
  offer removes that cost for light use.
- It auto-pauses after an hour with no activity, so compute stops billing.
- Free limit behaviour: set to auto-pause until next month. That guarantees no
  charge but means the database is inaccessible for the rest of the month if
  the allowance runs out, and the site would show errors. The alternative is
  overage billing, which keeps it online and bills only the excess. That switch
  is one-way.
- **The awake-hour budget, from real values** (checked live, 6 October 2026 —
  `az sql db show`): minimum capacity 0.5 vCore, auto-pause delay 60 minutes.
  100,000 vCore-seconds ÷ (0.5 vCore × 3,600 seconds/hour) ≈ **55.6 hours of
  awake time a month** at the idle rate. Because the database stays awake for
  at least the 60-minute auto-pause delay after its last touch, that's close
  to one hour of budget per discrete wake-up too — the old "about 55
  wake-ups" figure and this hours figure are the same budget, just two ways
  of stating it. If the database is ever genuinely busy rather than idle
  (vCore usage above 0.5), the same vCore-second budget is used up in fewer
  wall-clock hours, not more.
- Both background jobs are now configured to run once a day
  (`CompletionSweepIntervalMinutes` and `ExchangeRateRefreshIntervalMinutes`,
  both 1440 — set live on the App Service, 6 October 2026, with the committed
  `appsettings.json` defaults matching so a fresh deploy doesn't fall back to
  the old, more frequent values). With Always On off, this interval is really
  an upper bound rather than a guarantee: because both jobs run their first
  iteration immediately at process start, a visit after any idle gap still
  triggers one run of each regardless of the configured interval. What the
  once-a-day setting actually buys is that a *single continuous period* of
  traffic (a demo session, active testing) no longer re-triggers either job
  again partway through — previously the exchange-rate job in particular
  would re-fire every 60 minutes even within one unbroken session. The
  database budget is now driven almost entirely by how many separate
  visit-after-a-gap cold starts happen in a day, not by the job intervals
  themselves.
- Backup redundancy is Local, not Geo. The free offer's auto-pause mode needs
  it. In practice this means no copy in another region. That is an acceptable
  trade for a portfolio project.
- It could not be converted in place, and a BACPAC import into a free database
  is refused, so the data was moved with EF Core migrations plus a small copy
  script. That is recorded in the runbook.
- The old paid database (TravelPlanDb) has been deleted — confirmed live,
  6 October 2026: `az sql db list` on this server shows only
  `TravelPlanDb-free` and the system `master` database. While it existed it
  would have billed about US$5.74 a month in storage even while paused,
  because storage is billed on the configured maximum size — that charge no
  longer applies.

### Static Web App travelplan-web-dac4a409
- Plan: Free. It hosts the compiled Blazor app and rebuilds from GitHub on
  every push through a workflow Azure added to the repo.
- Why Free: no server work per page, so there is nothing to pay for at this
  scale.
- Free limits worth knowing: 250 MB per app, 100 GB of bandwidth a month, two
  custom domains, three preview environments, and no SLA. Standard raises these
  and costs a monthly fee per app.
- Needed: a routing rule that sends unknown paths to index.html, otherwise the
  login callback returns a 404. That rule is already in place.

### Microsoft Entra External ID tenant: travelplansy.onmicrosoft.com
- A separate identity system for the app's end users. It was created in the
  Entra admin center, not the normal portal.
- Contains three app registrations (API, Web, Mobile), the Google identity
  provider, and the signupsignin user flow.
- Cost: free for the first 50,000 monthly active users.
- Setup detail that caused a real bug: the API app registration needed the
  email claim added under Token configuration for the access token. Without it,
  email invites could not match the person who signed up.

### Azure Maps account travelplan-maps
- Gen2 (SKU G2). The old Gen1 pricing was retired, so Gen2 has to be named
  explicitly when creating it.
- Used for geocoding and static map images on transport legs.
- Cost: a free monthly allowance, then pay as you go. The key is stored as an
  App Service setting on the server only.
- One finding from the real API: the static image call cannot combine a
  bounding box with width and height. The code computes a centre and zoom
  instead.

### Not created yet
- Blob Storage (trip photos): plan is a Standard general purpose v2 account,
  LRS, Hot tier. New accounts get 5 GB of LRS Hot storage free for 12 months,
  and after that it is about US$0.018 per GB per month, so photos cost pennies
  at this scale. The upload endpoint should reject non-images and cap file
  size.
- Azure Communication Services (phone sign up): deferred. It needs a
  Pay-As-You-Go subscription, a registered 10DLC number, and it charges per
  message.
- Application Insights: listed in the stack but not wired in.

### Outside Azure
- Google Cloud OAuth client: free, used only for "Sign in with Google".
- Frankfurter exchange rate API: free, no key.
- GitHub: repository plus Actions, which runs the migration checks and tests.

---

## 4. What costs money and what does not

Free or close to it:
- Resource group, SQL server shell, app registrations
- Static Web App (Free)
- Entra External ID under 50,000 users
- The free SQL database, within its monthly allowance
- Azure Maps within its monthly allowance

Costs money:
- App Service plan B1: fixed, about A$16 to A$18 a month, whether or not the app
  is running
- Anything beyond a free allowance

(The old paid database used to be on this list — it has since been deleted;
see section 3.)

The lesson that cost real money: a serverless database only saves money if it
actually reaches the paused state. In September a background job and an
always-running API kept waking it, and two open browser tabs did the same, so
it billed almost continuously. "Serverless" and "auto-pause" describe what is
possible, not what will happen. After adding any timer or background job,
check Cost Analysis.

---

## 5. The deadline: 17 October 2026

- The trial credit expires. Azure's portal says paid services pause and free
  services do not incur charges. It does not promise free services keep
  running, so assume everything could stop until the subscription is upgraded.
- The App Service plan is a paid service, so the API would go down.
- Microsoft's free-services page says the 12-month free allowances (such as the
  5 GB Blob allowance) are only kept if a trial account moves to Pay-As-You-Go
  within 30 days of signup, which is about the same date.
- Upgrading to Pay-As-You-Go charges nothing by itself. It only means usage
  beyond free allowances is billed to your card afterwards.

Options:
- Upgrade and keep B1: about A$16 to A$18 a month, reliable.
- Upgrade and move the API to Free F1: about A$0, but with the limits in
  section 6.
- Do not upgrade: the site goes down on the 17th until you do.

---

## 6. Changing plans later

### B1 and F1
Microsoft documents this as a simple tier change that can be done at any time,
with no code change and no redeploy. You can keep B1 now and switch later.

To move to Free F1:
1. Always On must be off, because Free does not support it — it already is,
   by design (see section 3), so this is already satisfied:
   az webapp config set --name travelplan-api-dac4a409 --resource-group rg-travelplan --always-on false
2. Change the tier: Portal, then the App Service plan, then Scale up, then
   Free F1. Or:
   az appservice plan update --name travelplan-plan --resource-group rg-travelplan --sku F1
3. If F1 is greyed out for Linux in Central US, it is not available there.

What F1 means for this app:
- A daily CPU quota (third-party sources say 60 CPU minutes). Microsoft says the
  app is stopped until the quota resets if it goes over.
- No Always On, so the app unloads when idle — the same behaviour B1 already
  has here by choice, so this is less of a change than it would be for an app
  that relied on Always On.
- A slow first request after idle, made worse when the database is also waking.
- Report rendering is CPU heavy and could use up the quota.
- It is fine for a demo that is only opened occasionally.

To move back to B1, run the same update command with --sku B1.

### Other switches
- Moving up to Standard S1 is what adds autoscale and deployment slots (zero
  downtime releases). It costs several times more than B1.
- Static Web App Free to Standard: Portal, Settings, Hosting plan.
- SQL free offer to overage billing: a one-way switch.
- Blob Storage: change the access tier or redundancy at any time. Changing
  redundancy can take days.

---

## 7. How it was set up, in order

The exact commands are in deployment-runbook.md. The order matters:

1. Resource group, then a budget alert before anything billable exists.
2. SQL server and database.
3. App Service plan and app, then deploy the API.
4. Static Web App connected to the GitHub repository.
5. Entra External ID tenant (separate admin center), then the app
   registrations, the Google provider, the user flow, and the email claim.
6. App settings and connection strings on the server.
7. Migrations applied to the database.
8. Azure Maps account and key.
9. A smoke test against /health and the live site.

---

## 8. Decisions and open items

- Decide before 17 October: upgrade to Pay-As-You-Go or not, and B1 or F1.
- ~~Delete the old paid database once the free one is trusted.~~ Done —
  confirmed deleted, 6 October 2026.
- ~~Decide whether Always On should be turned back on.~~ Decided, 6 October
  2026: left off, by design — see section 3 for the reasoning.
- Set an alert on the free database's "Free amount remaining" metric below
  10,000 vCore-seconds.
- Stop the App Service when not working on the project. It does not stop the
  plan charge, but it stops the background jobs from waking the database.
- Close browser tabs on the live site when idle for the same reason.

---

## 9. Things that went wrong, and what they taught

- Serverless database that never paused: tiers describe potential cost, not
  actual cost. Check Cost Analysis after adding scheduled work.
- Cascade delete rules worked on SQLite and failed on SQL Server: development
  and production databases enforce different rules, so test on the real one.
- Role stored as text compared alphabetically: a Viewer could out-rank an
  Editor. Roles are now stored as numbers, with tests that reject as well as
  allow.
- Missing email claim: the identity provider's configuration is part of the
  system. Decode a real token instead of assuming what it contains.
- Free tier migration blocked twice: the in-place conversion and the backup
  import were both refused, so read the limits before planning the move.
- Database waking up during a request: normal requests need retry handling
  too, not only the startup path.
- Deploy package of 633 MB: it included native files for platforms that never
  run here. Publishing for the target platform cut it to about 70 MB.
- Autoscale claimed on a tier that cannot do it: check what a tier supports
  before writing it into the design.

---

## 10. Check this document against the live setup

This guide was originally written from reports, not by reading the live
resources. It was checked once already — see the verification note below —
but the live setup can drift from this document again, so the same prompt
is still useful later. Ask Claude Code to verify it:

Check every fact in docs/azure-guide-for-travelplan.md against the live
Azure resources using az commands. For each resource list its name, region,
SKU or tier, and the settings the guide mentions (App Service plan SKU and
Always On, SQL database free-offer flags and backup redundancy, Static Web App
plan, Maps SKU). Never print secret values, only whether a setting exists.
Tell me every mismatch and whether the old paid TravelPlanDb still exists.
Correct the guide where it is wrong.

**Verified 6 October 2026** against live resources via `az` commands. SQL
database free-offer flags, backup redundancy, Static Web App plan, Maps SKU,
and the budget all matched this guide exactly. Three things did not and were
corrected in place: the Azure Maps account's region (East US, not Central
US — section 3), Always On being off rather than on (section 3 and section
6), and the old paid database already being deleted rather than still a
pending task (sections 3, 4, and 8).

**Updated again, 6 October 2026**, same day: both background job intervals
changed from 240/60 minutes to 1440/1440 (once a day), live and in the
committed defaults, specifically to fit the free database's awake-hour
budget — see section 3. Always On's earlier "turn it back on" recommendation
was reconsidered and reversed: it is being kept off deliberately, not as an
unresolved risk — see section 3 and section 8.
