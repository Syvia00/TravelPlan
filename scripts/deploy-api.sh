#!/usr/bin/env bash
# Publishes and deploys TravelPlan.Api to the Azure App Service.
#
# Always publish with -r linux-x64 --self-contained false. A plain `dotnet publish -c Release`
# (no RID) is a *portable* publish — it bundles native runtime assets for every platform any
# referenced package declares, not just the one actually deployed. For this project that meant
# ~614MB under runtimes/ (Windows-only debug symbols for SkiaSharp and MSAL that can never execute
# on the Linux App Service target), inflating the publish output to 633MB and the deploy package to
# ~216MB. That size was a direct contributor to a production outage: uploads took long enough that
# the deploy overlapped with an unrelated Database.Migrate()-vs-auto-pause race (see
# DatabaseMigrationRunner.cs and deployment-runbook.md section 8), making that race far more likely
# to actually be hit. Publishing for the one real target (linux-x64, framework-dependent — the App
# Service already provides the .NET runtime via its DOTNET|10.0 image) produces ~70MB instead, with
# only the native libs actually needed (libSkiaSharp.so, libmsalruntime.so) present.
set -euo pipefail

RG="${RG:-rg-travelplan}"
API_APP="${API_APP:-travelplan-api-dac4a409}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/artifacts/api-publish"
ZIP_PATH="$REPO_ROOT/artifacts/api-publish.zip"

rm -rf "$PUBLISH_DIR" "$ZIP_PATH"
mkdir -p "$PUBLISH_DIR"

dotnet publish "$REPO_ROOT/TravelPlan.Api/TravelPlan.Api.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "$PUBLISH_DIR"

PUBLISH_SIZE=$(du -sh "$PUBLISH_DIR" | cut -f1)
echo "Publish output: $PUBLISH_SIZE ($PUBLISH_DIR)"

(cd "$PUBLISH_DIR" && zip -r -q "$ZIP_PATH" .)

ZIP_SIZE=$(du -sh "$ZIP_PATH" | cut -f1)
echo "Deploy package: $ZIP_SIZE ($ZIP_PATH)"

# A stopped site never starts itself — az webapp deploy only copies files and then polls for the
# site to come up, so deploying onto a Stopped app sits out its full wait and reports a false
# "failed to start" (confirmed live, Session 7.5: the app had been Stopped for 3 unrelated days,
# the deploy's own poll timed out at 20 minutes, and a plain `az webapp start` afterward brought it
# up in under a minute). Starting it first avoids that every time, regardless of why it was stopped.
APP_STATE=$(az webapp show --resource-group "$RG" --name "$API_APP" --query state -o tsv)
if [ "$APP_STATE" = "Stopped" ]; then
  echo "App Service was Stopped — starting it before deploying."
  az webapp start --resource-group "$RG" --name "$API_APP"
fi

az webapp deploy \
  --resource-group "$RG" \
  --name           "$API_APP" \
  --src-path       "$ZIP_PATH" \
  --type           zip
