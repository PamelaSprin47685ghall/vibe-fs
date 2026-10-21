#!/bin/bash
# HNI-03 offline conformance: tsc type-check against omp 18.12.4 types + fixture run + F#/TS mirror check.
# No model call, no omp process. Env: OMP_PKG (pi-coding-agent package dir), TSC (typescript compiler).
set -eu
cd "$(dirname "$0")"
OMP_PKG=${OMP_PKG:-$HOME/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent}
TSC=${TSC:-/workspace/vfs-cut/tools/ts/node_modules/.bin/tsc}
grep -q '"version": "18.12.4"' "$OMP_PKG/package.json" || { echo "omp types are not 18.12.4: $OMP_PKG"; exit 2; }
TMP=$(mktemp -d); trap 'rm -rf "$TMP"' EXIT
cat > "$TMP/tsconfig.json" <<JSON
{ "extends": "$PWD/tsconfig.json",
  "compilerOptions": { "baseUrl": "$PWD", "resolveJsonModule": true,
    "paths": { "@oh-my-pi/pi-coding-agent/*": ["$OMP_PKG/dist/types/*"] } },
  "files": ["$PWD/ports.ts", "$PWD/adapter.ts", "$PWD/sdk-children.ts", "$PWD/fixtures/fixture.test.ts"] }
JSON
echo "== tsc --noEmit against $OMP_PKG"; "$TSC" -p "$TMP/tsconfig.json"; echo "tsc: ok"
echo "== mirror check (F# Host/*.fsi vs ports.ts)"; node mirror-check.mjs
echo "== offline fixture"; bun run fixtures/fixture.test.ts
