#!/usr/bin/env node
// Sequential focused checks: spec then architecture.
// Usage: node scripts/check.mjs

import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)))
const checks = [
  join(root, 'checks/spec.mjs'),
  join(root, 'checks/architecture.mjs'),
  join(root, 'checks/dsl-ownership.mjs'),
  join(root, 'checks/dsl-ownership-ratchet.mjs'),
  join(root, 'checks/p0-recovery-join.mjs'),
]

for (const script of checks) {
  const args = [script]
  // --threshold freezes current Direct-CE debt baseline; must only ever decrease.
  // P0 157→153; P1-1a 153→139; P1-3 139→124; program-counter rename 124→116.
  if (script.endsWith('dsl-ownership.mjs')) args.push('--threshold=116')
  // Per-file ratchet against the frozen baseline (missing baseline fails with a --generate hint).
  if (script.endsWith('dsl-ownership-ratchet.mjs')) {
    args.push(
      `--baseline=${join(root, 'checks/dsl-ownership-ratchet-baseline.json')}`,
      '--root=src/Wanxiangshu',
    )
  }
  const result = spawnSync(process.execPath, args, { stdio: 'inherit' })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

process.exit(0)
