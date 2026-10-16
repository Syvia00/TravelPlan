#!/usr/bin/env bash
# Points the Container App at an image tag already built and pushed by
# .github/workflows/build-and-push-api-image.yml. This script does not build or push anything
# itself — the image is always built in CI, on a GitHub-hosted amd64 runner. Building locally on
# this project's dev machines (Apple Silicon, arm64) would silently produce an image Azure
# Container Apps Consumption can't run.
#
# Every push to main that touches TravelPlan.Api, TravelPlan.Shared, or the Dockerfile pushes two
# tags: `latest` and the full commit SHA. Deploy a specific commit's image by SHA for anything
# that matters (so you know exactly what's running and can roll back to an earlier SHA the same
# way); `latest` is fine for quick manual testing.
#
# Usage: scripts/deploy-api.sh <tag>
#   scripts/deploy-api.sh latest
#   scripts/deploy-api.sh $(git rev-parse HEAD)
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Usage: $0 <tag>" >&2
  echo "  e.g. $0 latest" >&2
  echo "  e.g. $0 \$(git rev-parse HEAD)" >&2
  exit 1
fi

TAG="$1"
RG="${RG:-rg-travelplan}"
CONTAINER_APP="${CONTAINER_APP:-travelplan-api-ca}"
IMAGE="ghcr.io/syvia00/travelplan-api:${TAG}"

echo "Updating $CONTAINER_APP to image $IMAGE ..."

# `az containerapp update --image` only replaces the container's image reference; every other
# template field (env vars, secrets, the probes — see the Addendum in deployment-runbook.md for
# why those were set via `az rest` instead of this CLI) carries forward unchanged from the
# current active revision into the new one.
az containerapp update \
  --name "$CONTAINER_APP" \
  --resource-group "$RG" \
  --image "$IMAGE"

echo ""
echo "Active revision now:"
az containerapp revision list \
  --name "$CONTAINER_APP" \
  --resource-group "$RG" \
  --query "[?properties.active].{name:name, image:properties.template.containers[0].image, healthState:properties.healthState, replicas:properties.replicas}" \
  -o table
