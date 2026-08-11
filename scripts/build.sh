#!/usr/bin/env bash
set -euo pipefail

repository_dir="$(cd "$(dirname "$0")/.." && pwd)"
configuration="${1:-Release}"

dotnet run --project "$repository_dir/tests/EmbySubtitleOptimization.Tests/EmbySubtitleOptimization.Tests.csproj" --configuration "$configuration"
dotnet build "$repository_dir/src/EmbySubtitleOptimization/EmbySubtitleOptimization.csproj" --configuration "$configuration"
mkdir -p "$repository_dir/artifacts/plugin"
cp "$repository_dir/src/EmbySubtitleOptimization/bin/$configuration/netstandard2.0/EmbySubtitleOptimization.dll" "$repository_dir/artifacts/plugin/"

echo "Plugin files are available in $repository_dir/artifacts/plugin"
