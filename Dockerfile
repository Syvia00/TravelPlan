# Multi-stage build for TravelPlan.Api. Published framework-dependent for linux-x64 — same RID
# and --self-contained false flags scripts/deploy-api.sh already uses for the App Service zip
# deploy, for the same reason: a bare `dotnet publish` with no RID bundles native runtime assets
# for every platform any referenced package declares (Windows debug symbols for SkiaSharp/MSAL
# included), not just the one this image actually runs on.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TravelPlan.Shared/ TravelPlan.Shared/
COPY TravelPlan.Api/ TravelPlan.Api/

# One combined restore+publish call — the exact invocation scripts/deploy-api.sh already uses
# for the App Service zip deploy, kept identical rather than split into separate restore/publish
# steps (which can resolve a subtly different restore graph for a non-self-contained, RID-specific
# publish and fail on --no-restore) for the sake of Docker layer caching this project is too small
# to need.
RUN dotnet publish TravelPlan.Api/TravelPlan.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# SkiaSharp's native libSkiaSharp.so ships via the SkiaSharp.NativeAssets.Linux NuGet package
# (already in the publish output), but it dynamically links against fontconfig/freetype at the
# OS level, which this base image doesn't include — TripReportSkiaRenderer and
# PhotoThumbnailGenerator would throw DllNotFoundException without it. fonts-dejavu-core gives
# fontconfig an actual font to resolve, so text in rendered reports/thumbnails isn't just boxes
# or blank glyphs.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# $APP_UID is the non-root "app" user baked into the .NET 8+ Linux images. Must come after the
# apt-get install above, which needs root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TravelPlan.Api.dll"]
