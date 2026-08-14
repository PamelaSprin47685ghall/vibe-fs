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
  join(root, 'checks/session-ownership-ratchet.mjs'),
  join(root, 'checks/kolmogorov-size.mjs'),
  join(root, 'checks/enforcer-cross-family-collision.mjs'),
  join(root, 'checks/js-surface-gate.mjs'),
  join(root, 'checks/capability-isomorphism-gate.mjs'),
  join(root, 'checks/unified-store-gate.mjs'),
  join(root, 'checks/tool-referential-integrity.mjs'),
  join(root, 'checks/provider-leak-gate.mjs'),
  join(root, 'checks/language-parity-gate.mjs'),
  join(root, 'checks/prompt-depth-ratchet.mjs'),
  join(root, 'checks/provider-prose-ownership.mjs'),
  join(root, 'checks/g4r-ce-vocabulary.mjs'),
  join(root, 'checks/test-boundary.mjs'),
  join(root, 'checks/e2e-watchdog-feed.mjs'),
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
  // Kolmogorov size is advisory only. The baseline gives comparative context
  // (growth is a stronger suggestion) but no line/function threshold can fail check.
  if (script.endsWith('kolmogorov-size.mjs')) {
    args.push(`--baseline=${join(root, 'checks/kolmogorov-size-baseline.json')}`)
  }
  // ARCH-016 Gate B: grandfathered Join/horizon migration debt — counts must only shrink.
  if (script.endsWith('provider-leak-gate.mjs')) {
    args.push(`--baseline=${join(root, 'checks/provider-leak-gate-baseline.json')}`)
  }
  // ARCH-016 Gate E: provider-visible prose ownership — counts must only shrink.
  if (script.endsWith('provider-prose-ownership.mjs')) {
    args.push(`--baseline=${join(root, 'checks/provider-prose-ownership-baseline.json')}`)
  }
  const result = spawnSync(process.execPath, args, { stdio: 'inherit' })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

process.exit(0)
