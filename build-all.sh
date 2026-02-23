#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$ROOT_DIR/TryCr4ckP4ss/TryCr4ckP4ss.csproj"
ARTIFACTS_DIR="$ROOT_DIR/artifacts"

CONFIGURATION="${1:-Release}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"
PUBLISH_SINGLE_FILE="${PUBLISH_SINGLE_FILE:-true}"

RIDS=(
  "linux-x64"
  "win-x64"
  "osx-arm64"
  "osx-x64"
)

echo "==> Restoring project"
dotnet restore "$PROJECT_PATH"

for rid in "${RIDS[@]}"; do
  output_dir="$ARTIFACTS_DIR/$rid"
  echo "==> Publishing $rid ($CONFIGURATION) -> $output_dir"
  mkdir -p "$output_dir"

  dotnet publish "$PROJECT_PATH" \
    -c "$CONFIGURATION" \
    -r "$rid" \
    --self-contained "$SELF_CONTAINED" \
    /p:PublishSingleFile="$PUBLISH_SINGLE_FILE" \
    -o "$output_dir"
done

echo
echo "Build complete. Artifacts are in: $ARTIFACTS_DIR"
