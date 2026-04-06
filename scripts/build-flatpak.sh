#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FLATPAK_DIR="$ROOT_DIR/flatpak"
PUBLISH_DIR="$FLATPAK_DIR/publish"
APP_ID="io.github.hoursbooking.HoursBooking"
MANIFEST="$FLATPAK_DIR/$APP_ID.yml"
BUNDLE_PATH="$ROOT_DIR/$APP_ID.flatpak"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/hoursbooking-flatpak.XXXXXX")"
BUILD_DIR="$WORK_DIR/build"
REPO_DIR="$WORK_DIR/repo"
STATE_DIR="$WORK_DIR/state"

trap 'rm -rf "$WORK_DIR"' EXIT

mkdir -p "$PUBLISH_DIR"
rm -rf "$PUBLISH_DIR"/*
rm -f "$BUNDLE_PATH"

echo "[1/4] Publishing HoursBooking.App for linux-x64..."
dotnet publish "$ROOT_DIR/HoursBooking.App/HoursBooking.App.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeAllContentForSelfExtract=true \
  /p:PublishTrimmed=false \
  -o "$PUBLISH_DIR"

echo "[2/4] Generating Flatpak icons from SVG..."
cp "$ROOT_DIR/HoursBooking.App/Assets/hoursbooking-icon-dark.svg" "$FLATPAK_DIR/$APP_ID.svg"

magick -background none "$ROOT_DIR/HoursBooking.App/Assets/hoursbooking-icon-dark.svg" \
  -resize 128x128 \
  "$FLATPAK_DIR/$APP_ID.128.png"

magick -background none "$ROOT_DIR/HoursBooking.App/Assets/hoursbooking-icon-dark.svg" \
  -resize 256x256 \
  "$FLATPAK_DIR/$APP_ID.256.png"

magick -background none "$ROOT_DIR/HoursBooking.App/Assets/hoursbooking-icon-dark.svg" \
  -resize 512x512 \
  "$FLATPAK_DIR/$APP_ID.512.png"

echo "[3/4] Building Flatpak repository..."
flatpak-builder --state-dir="$STATE_DIR" --repo="$REPO_DIR" "$BUILD_DIR" "$MANIFEST"

echo "[4/4] Creating Flatpak bundle..."
flatpak build-bundle "$REPO_DIR" "$BUNDLE_PATH" "$APP_ID"

echo "Done. Bundle created at:"
echo "    $BUNDLE_PATH"
echo
echo "Optional installation command:"
echo "    flatpak install --user --bundle \"$BUNDLE_PATH\""
