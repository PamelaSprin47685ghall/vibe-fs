#!/usr/bin/env node
// Sequential focused checks: spec then architecture.

import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)))
const checks = [
  join(root, 'checks/architecture.mjs'),
  join(root, 'checks/participant-identity-boundary.mjs'),
  join(root, 'checks/provider-projection-boundary.mjs'),
  join(root, 'checks/subsystems.mjs'),
  join(root, 'checks/fsharp-control-pyramid.mjs'),
  join(root, 'checks/retry-owner.mjs'),
  join(root, 'checks/enforcer-bounds-owner.mjs'),
  join(root, 'checks/hook-policy.mjs'),
  join(root, 'checks/causal-wait-boundary.mjs'),
  join(root, 'checks/unified-store-gate.mjs'),
  join(root, 'checks/tool-referential-integrity.mjs'),
  join(root, 'checks/provider-leak-gate.mjs'),
  join(root, 'checks/llm-facing-format-gate.mjs'),
  join(root, 'checks/language-parity-gate.mjs'),
  join(root, 'checks/js-boundary-gate.mjs'),
]

for (const script of checks) {
  const args = [script]
  if (script.endsWith('dsl-ownership.mjs')) args.push('--threshold=0')
  if (script.endsWith('fsharp-control-pyramid.mjs')) args.push('--root=src/Wanxiangshu')
  const result = spawnSync(process.execPath, args, { stdio: 'inherit', env: process.env })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

process.exit(0)
