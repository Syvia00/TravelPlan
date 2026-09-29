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

az webapp deploy \
  --resource-group "$RG" \
  --name           "$API_APP" \
  --src-path       "$ZIP_PATH" \
  --type           zip
