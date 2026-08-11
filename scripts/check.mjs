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
  join(root, 'checks/causal-wait-boundary.mjs'),
  join(root, 'checks/student-teacher-absence.mjs'),
  join(root, 'checks/session-ownership-ratchet.mjs'),
  join(root, 'checks/enforcer-rulebook-gate.mjs'),
  join(root, 'checks/enforcer-cross-family-collision.mjs'),
  join(root, 'checks/js-surface-gate.mjs'),
  join(root, 'checks/capability-isomorphism-gate.mjs'),
  join(root, 'checks/unified-store-gate.mjs'),
  join(root, 'checks/g4r-freeze.mjs'),
  join(root, 'checks/g4r-ce-vocabulary.mjs'),
]

for (const script of checks) {
  const args = [script]
  // G4R-CE static ratchet in hard mode (S14 / Exit).
  if (script.endsWith('g4r-ce-vocabulary.mjs')) args.push('--phase=hard')
  // --threshold freezes current Direct-CE debt baseline; must only ever decrease.
  // P0→P2-3c: 157→13. P2-2 Host boundary open allowlist: 13→0.
  if (script.endsWith('dsl-ownership.mjs')) args.push('--threshold=0')
  // Per-file ratchet against the frozen baseline (missing baseline fails with a --generate hint).
  if (script.endsWith('dsl-ownership-ratchet.mjs')) {
    args.push(
      `--baseline=${join(root, 'checks/dsl-ownership-ratchet-baseline.json')}`,
      '--root=src/Wanxiangshu',
    )
  }
  // Constitution headings + structural rubric (Appendix A37/A38) across all 120 tips.
  if (script.endsWith('enforcer-rulebook-gate.mjs')) {
    args.push('--require-headings', '--strict')
  }
  const result = spawnSync(process.execPath, args, { stdio: 'inherit' })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

process.exit(0)
