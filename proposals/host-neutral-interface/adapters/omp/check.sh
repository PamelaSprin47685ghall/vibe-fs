#!/bin/bash
# HNI-03 offline conformance: tsc type-check against omp 18.12.4 types + fixture run + F#/TS mirror check.
# No model call, no omp process. Env: OMP_PKG (pi-coding-agent package dir), TSC (typescript compiler).
set -eu
cd "$(dirname "$0")"
OMP_PKG=${OMP_PKG:-$HOME/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent}
# Portable compiler discovery: an explicit override wins, then the repo-local
# TypeScript, then the one installed with the agent runtime.
if [ -z "${TSC:-}" ]; then
  for candidate in \
    "$PWD/../../../node_modules/.bin/tsc" \
    "$PWD/../../../../node_modules/.bin/tsc" \
    "/usr/local/lib/hermes-agent/node_modules/.bin/tsc"; do
    if [ -x "$candidate" ]; then TSC=$candidate; break; fi
  done
fi
TMP="$PWD/.tsconfig.check.json"
cat > "$TMP" <<JSON
{ "extends": "./tsconfig.json",
  "compilerOptions": { "resolveJsonModule": true,
    "paths": { "@oh-my-pi/pi-coding-agent/*": ["$OMP_PKG/dist/types/*"] } },
  "files": ["$PWD/ports.ts", "$PWD/adapter.ts", "$PWD/sdk-children.ts", "$PWD/session-files.ts", "$PWD/fixtures/fixture.test.ts"] }
JSON
trap 'rm -f "$TMP"' EXIT
echo "== tsc --noEmit against $OMP_PKG"; "$TSC" -p "$TMP"; echo "tsc: ok"
echo "== mirror check (F# Host/Ports/*.fsi vs generated ports.ts)"; node mirror-check.mjs
echo "== offline fixture"; bun run fixtures/fixture.test.ts
