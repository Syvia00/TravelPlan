# Deployment Runbook — Azure Setup (v2)

Updates `deployment-runbook.md`: replaces the Azure AD B2C tenant steps with
Microsoft Entra External ID, and adds Azure Communication Services (phone OTP) and
Azure Maps provisioning.

> **Note:** No Bicep templates exist in the repository yet. All provisioning uses the
> Azure CLI (`az`). Run commands from a terminal where you are logged in: `az login`.

---

## Contents

1. [Prerequisites](#1-prerequisites)
2. [Variables](#2-variables)
3. [Resource group](#3-resource-group)
4. [Microsoft Entra External ID tenant](#4-microsoft-entra-external-id-tenant)
5. [Azure Communication Services — phone OTP](#5-azure-communication-services--phone-otp)
6. [Azure Maps](#6-azure-maps)
7. [Azure SQL Database](#7-azure-sql-database)
8. [App Service — API](#8-app-service--api)
9. [Static Web App — Blazor frontend](#9-static-web-app--blazor-frontend)
10. [Application configuration](#10-application-configuration)
11. [EF Core migrations](#11-ef-core-migrations)
12. [GitHub Actions secrets](#12-github-actions-secrets)
13. [Smoke test](#13-smoke-test)
14. [Teardown](#14-teardown)

---

## 1. Prerequisites

| Tool | Install |
|---|---|
| Azure CLI | https://learn.microsoft.com/cli/azure/install-azure-cli |
| .NET 10 SDK | https://dotnet.microsoft.com/download |
| An Azure subscription | https://portal.azure.com |

```bash
az --version        # 2.60+
dotnet --version    # 10.0+
az login
```

---

## 2. Variables

```bash
# Core identifiers
RG="rg-travelplan"
LOCATION="australiaeast"          # change to your preferred region

# SQL
SQL_SERVER="travelplan-sql"
SQL_DB="TravelPlanDb"
SQL_ADMIN="tpadmin"
SQL_PASSWORD="<strong-password>"  # min 12 chars, upper+lower+digit+symbol

# App Service (API)
APP_PLAN="travelplan-plan"
API_APP="travelplan-api"

# Static Web App (Blazor)
SWA_NAME="travelplan-web"

# Microsoft Entra External ID (fill in after step 4)
EXTERNAL_ID_TENANT="<your-tenant>.onmicrosoft.com"
EXTERNAL_ID_TENANT_ID="<tenant-id>"
API_CLIENT_ID="<api-app-registration-id>"
WEB_CLIENT_ID="<web-app-registration-id>"
MOBILE_CLIENT_ID="<mobile-app-registration-id>"
USER_FLOW="signupsignin"
API_SCOPE="https://${EXTERNAL_ID_TENANT}/api/access_as_user"

# Azure Communication Services (fill in after step 5)
ACS_NAME="travelplan-acs"
ACS_CONNECTION_STRING="<from-step-5>"

# Azure Maps (fill in after step 6)
MAPS_ACCOUNT="travelplan-maps"
MAPS_KEY="<from-step-6>"
```

---

## 3. Resource group

```bash
az group create \
  --name     "$RG" \
  --location "$LOCATION"
```

**Before provisioning anything else**: set a cost budget alert on this resource
group now, while it's still empty — this is the cheapest moment to do it, and
matches the cost-consciousness goal in `TravelPlan-Project-Plan-v2.md` §8.
Portal is simpler than CLI for a one-off: **Cost Management + Billing** →
**Budgets** → **Add** → scope it to `$RG` → set a monthly amount (e.g. $25) →
add an email alert at 80% and 100%. Takes under a minute and means you find out
before a surprise bill, not after.

---

## 4. Microsoft Entra External ID tenant

Replaces the old Azure AD B2C steps — Azure AD B2C has been unavailable for new
tenants since May 2025. External ID must be created in the Azure Portal — it cannot
be fully provisioned via CLI alone.

### 4a. Create the External ID tenant

1. Portal → **Create a resource** → search **Microsoft Entra External ID**
2. Select **Create a new external tenant**
3. Choose an **Organisation name** and **Initial domain name** (becomes
   `<tenant>.onmicrosoft.com`)
4. Select your subscription and the resource group created in step 3
5. Click **Review + Create**

Note the **Tenant ID** and **Domain name** — paste them into the variables above.

### 4b. Register the API application

In the External ID tenant (switch directory to the new tenant first):

1. **App registrations** → **New registration**
   - Name: `TravelPlan API`
   - Supported account types: *Accounts in this organizational directory only*
   - No redirect URI
2. Note the **Application (client) ID** → set `API_CLIENT_ID`
3. **Expose an API** → **Add a scope**
   - Scope name: `access_as_user`
   - Admin consent display name: `Access TravelPlan API`
   - State: Enabled

### 4c. Register the Web (SPA) application

1. **App registrations** → **New registration**
   - Name: `TravelPlan Web`
   - Redirect URI: **Single-page application** → `https://<swa-hostname>/authentication/login-callback`
2. Note **Application (client) ID** → set `WEB_CLIENT_ID`
3. **API permissions** → **Add a permission** → **My APIs** → `TravelPlan API` → `access_as_user`
4. **Grant admin consent**

### 4d. Register the Mobile (native) application

1. **App registrations** → **New registration**
   - Name: `TravelPlan Mobile`
   - Redirect URI: **Public client/native** → `msal<mobile-client-id>://auth`
2. Note **Application (client) ID** → set `MOBILE_CLIENT_ID`
3. **API permissions** → **Add a permission** → **My APIs** → `TravelPlan API` → `access_as_user`
4. **Grant admin consent**

### 4e. Create the sign-up/sign-in user flow

1. **User flows** → **New user flow**
2. Select **Sign up and sign in**
3. Name: `signupsignin`
4. Identity providers: **Email** + **Google** (configure a Google OAuth client ID/secret first, under **Identity providers** → **Google**)
5. User attributes and claims: **Email Address**, **Display Name**, **Given Name**, **Surname**
6. Click **Create**

> Phone-number sign-up is deliberately **not** configured here — it's handled by the
> app's own OTP flow (step 5), independent of External ID.

---

## 5. Azure Communication Services — phone OTP

Used only to send the SMS one-time code; verification happens in the API against the
`PhoneVerifications` table, not inside ACS.

**Before provisioning**: phone numbers cannot be acquired on a free-trial or
free-credits-only Azure subscription — a real paid subscription is required. Also,
sending SMS is not instant: the number needs carrier registration first. Prefer a
**10DLC local number** over toll-free — Brand + Campaign registration typically
takes 1–3 weeks, versus 5–6 weeks for Toll-Free Verification, and 10DLC has no
separate monthly leasing fee (toll-free numbers carry a flat $2/month regardless
of usage, on top of per-message cost). Budget for this lead time before Session 6
can be fully tested end-to-end — the code can be written and unit-tested
immediately, but live SMS delivery won't work until registration clears.

```bash
az communication create \
  --name              "$ACS_NAME" \
  --resource-group    "$RG" \
  --location          "global" \
  --data-location     "Australia"

az communication list-key \
  --name              "$ACS_NAME" \
  --resource-group    "$RG"
```

Copy the connection string from the output into `ACS_CONNECTION_STRING`. Then in
the Azure Portal, go to the ACS resource → **Phone numbers** → **Get** → choose
**Local number** (not Toll-Free) → complete Brand and Campaign registration via
The Campaign Registry (TCR) to enable SMS on it.

---

## 6. Azure Maps

Used for geocoding destinations and generating static map thumbnails for transport
legs. Explicitly requests **Gen2** — Azure Maps' older Gen1 pricing tier retires
September 15, 2026, and the CLI's default SKU (`S0`) maps to the now-retired Gen1
model, so `--sku G2 --kind Gen2` must be passed explicitly to land on current
pricing. Gen2's free monthly transaction allowance is comfortable for this app's
early-stage usage before any charge applies.

```bash
az maps account create \
  --name              "$MAPS_ACCOUNT" \
  --resource-group    "$RG" \
  --sku               G2 \
  --kind              Gen2 \
  --accept-tos

az maps account keys list \
  --name              "$MAPS_ACCOUNT" \
  --resource-group    "$RG"
```

Copy the primary key into `MAPS_KEY`.

---

## 7. Azure SQL Database

```bash
# Create SQL Server
az sql server create \
  --resource-group "$RG" \
  --name           "$SQL_SERVER" \
  --location       "$LOCATION" \
  --admin-user     "$SQL_ADMIN" \
  --admin-password "$SQL_PASSWORD"

# Allow Azure services to connect (required for App Service)
az sql server firewall-rule create \
  --resource-group "$RG" \
  --server         "$SQL_SERVER" \
  --name           "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address   0.0.0.0

# Create the database (General Purpose Serverless — cost-effective for low traffic)
az sql db create \
  --resource-group "$RG" \
  --server         "$SQL_SERVER" \
  --name           "$SQL_DB" \
  --edition        GeneralPurpose \
  --family         Gen5 \
  --capacity       1 \
  --compute-model  Serverless \
  --auto-pause-delay 60
```

```bash
SQL_CONN="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DB};Persist Security Info=False;User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
echo "$SQL_CONN"
```

---

## 8. App Service — API

```bash
az appservice plan create \
  --resource-group "$RG" \
  --name           "$APP_PLAN" \
  --location       "$LOCATION" \
  --sku            B1 \
  --is-linux

az webapp create \
  --resource-group "$RG" \
  --plan           "$APP_PLAN" \
  --name           "$API_APP" \
  --runtime        "DOTNET|10.0"

az webapp config set \
  --resource-group "$RG" \
  --name           "$API_APP" \
  --generic-configurations '{"healthCheckPath":"/health"}'
```

```bash
API_URL="https://${API_APP}.azurewebsites.net"
echo "$API_URL"
```

---

## 9. Static Web App — Blazor frontend

```bash
az staticwebapp create \
  --resource-group "$RG" \
  --name           "$SWA_NAME" \
  --location       "$LOCATION" \
  --source         "https://github.com/<your-org>/TravelPlan" \
  --branch         main \
  --app-location   "TravelPlan.Web" \
  --output-location "wwwroot" \
  --login-with-github
```

```bash
SWA_HOSTNAME=$(az staticwebapp show \
  --resource-group "$RG" \
  --name           "$SWA_NAME" \
  --query "defaultHostname" -o tsv)
echo "https://$SWA_HOSTNAME"
```

Update the Web app registration redirect URI (step 4c) if the hostname differs.

---

## 10. Application configuration

### API — App Service settings

```bash
az webapp config appsettings set \
  --resource-group "$RG" \
  --name           "$API_APP" \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=${SQL_CONN}" \
    "AzureAdExternalId__Instance=https://<your-tenant>.ciamlogin.com" \
    "AzureAdExternalId__ClientId=${API_CLIENT_ID}" \
    "AzureAdExternalId__Domain=${EXTERNAL_ID_TENANT}" \
    "AzureAdExternalId__SignUpSignInPolicyId=${USER_FLOW}" \
    "AzureAdExternalId__TenantId=${EXTERNAL_ID_TENANT_ID}" \
    "AzureCommunicationServices__ConnectionString=${ACS_CONNECTION_STRING}" \
    "AzureMaps__SubscriptionKey=${MAPS_KEY}" \
    "ExchangeRateApi__BaseUrl=https://api.frankfurter.dev"
```

### Web — `wwwroot/appsettings.json`

```json
{
  "ApiBaseUrl": "https://travelplan-api.azurewebsites.net/",
  "AzureAdExternalId": {
    "Authority": "https://<tenant>.ciamlogin.com/<tenant>.onmicrosoft.com/",
    "ClientId": "<web-client-id>",
    "ValidateAuthority": false,
    "DefaultScopes": [
      "https://<tenant>.onmicrosoft.com/api/access_as_user"
    ]
  }
}
```

### Mobile — `AuthService.cs` constants

```csharp
private const string Tenant       = "<tenant>.onmicrosoft.com";
private const string ClientId     = "<mobile-client-id>";
private const string SignInPolicy = "signupsignin";
private const string Scope        = "https://<tenant>.onmicrosoft.com/api/access_as_user";
```

Also update `ApiService.cs`:

```csharp
private const string BaseUrl = "https://travelplan-api.azurewebsites.net/";
```

---

## 11. EF Core migrations

The API applies pending migrations automatically on startup (`Database.Migrate()` in
`Program.cs`), so this section is normally only needed to apply a migration manually or out of
band. SQLite (dev) and SQL Server (Azure) each have their own migration history — see
`docs/migrations.md` for why and for the full day-to-day workflow. To apply manually against Azure
SQL:

```bash
cd TravelPlan.Api

dotnet ef database update --context TravelPlanSqlServerDbContext \
  --connection "$SQL_CONN"
```

If you need to create a new migration after schema changes, scaffold it for **both** providers and
verify each applies cleanly before merging — see `docs/migrations.md`:

```bash
dotnet ef migrations add <MigrationName> --context TravelPlanSqliteDbContext -o Migrations/Sqlite
dotnet ef migrations add <MigrationName> --context TravelPlanSqlServerDbContext -o Migrations/SqlServer
```

---

## 12. GitHub Actions secrets

The CI pipeline (`ci-cd.yml`) currently only builds and runs tests. If you re-add a
deployment job, configure these under **Settings → Secrets and variables → Actions**:

| Secret | Value |
|---|---|
| `AZURE_CREDENTIALS` | Output of `az ad sp create-for-rbac --sdk-auth --role contributor --scopes /subscriptions/<sub>/resourceGroups/$RG` |
| `AZURE_WEBAPP_NAME` | `travelplan-api` |
| `AZURE_RESOURCE_GROUP` | `rg-travelplan` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Added automatically by the SWA CLI in step 9 |

```bash
az ad sp create-for-rbac \
  --name        "travelplan-github-actions" \
  --role        contributor \
  --scopes      "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RG" \
  --sdk-auth
```

---

## 13. Smoke test

```bash
curl -s -o /dev/null -w "%{http_code}" "${API_URL}/health"
# Expected: 200

curl -s -o /dev/null -w "%{http_code}" "${API_URL}/swagger"
# Expected: 200

curl -s -o /dev/null -w "%{http_code}" "https://${SWA_HOSTNAME}"
# Expected: 200
```

---

## 14. Teardown

```bash
az group delete \
  --name  "$RG" \
  --yes   \
  --no-wait
```

> This deletes everything in the resource group. The Microsoft Entra External ID
> tenant must be deleted separately from the Azure Portal (directory → Manage →
> Delete tenant).

---

## Addendum: Azure Storage — trip photos

Not in the original numbered sequence above (added later, for the Photos &
journaling feature — see `TravelPlan-Project-Plan-v2.md` section 2). Provision
whenever that feature is being built; doesn't need to happen alongside sections
3–9.

```bash
STORAGE_ACCOUNT="travelplanphotos"   # must be globally unique, lowercase, no hyphens

az storage account create \
  --resource-group "$RG" \
  --name           "$STORAGE_ACCOUNT" \
  --location       "$LOCATION" \
  --sku            Standard_LRS \
  --kind           StorageV2 \
  --access-tier    Hot

az storage container create \
  --account-name "$STORAGE_ACCOUNT" \
  --name         "trip-photos" \
  --auth-mode    login

az storage account show-connection-string \
  --resource-group "$RG" \
  --name           "$STORAGE_ACCOUNT"
```

Copy the connection string into the API's app settings as
`AzureStorage__ConnectionString`. Photos should be organised under a
`{userId}/{tripId}/` blob path prefix per `TravelPlan-Project-Plan-v2.md`'s
original tech-stack note.
