#!/usr/bin/env node
// Sequential focused checks: spec then architecture.

import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)))
const checks = [
  join(root, 'checks/spec.mjs'),
  join(root, 'checks/architecture.mjs'),
  join(root, 'checks/participant-identity-boundary.mjs'),
  join(root, 'checks/provider-projection-boundary.mjs'),
  join(root, 'checks/semantic-owners.mjs'),
  join(root, 'checks/owner-contracts.mjs'),
  join(root, 'checks/owner-projects.mjs'),
  join(root, 'checks/dsl-ownership.mjs'),
  join(root, 'checks/authority-boundary.mjs'),
  join(root, 'checks/fsharp-control-pyramid.mjs'),
  join(root, 'checks/plugin-transforms-invariant.mjs'),
  join(root, 'checks/interaction-repair-invariant.mjs'),
  join(root, 'checks/retry-owner.mjs'),
  join(root, 'checks/enforcer-bounds-owner.mjs'),
  join(root, 'checks/hook-policy.mjs'),
  join(root, 'checks/semantic-decorator-invariant.mjs'),
  join(root, 'checks/deadcode.mjs'),
  join(root, 'checks/p0-recovery-join.mjs'),
  join(root, 'checks/causal-wait-boundary.mjs'),
  join(root, 'checks/cross-callback-pc.mjs'),
  join(root, 'checks/session-ownership-ratchet.mjs'),
  join(root, 'checks/js-surface-gate.mjs'),
  join(root, 'checks/capability-isomorphism-gate.mjs'),
  join(root, 'checks/unified-store-gate.mjs'),
  join(root, 'checks/external-effect-reconciliation.mjs'),
  join(root, 'checks/tool-referential-integrity.mjs'),
  join(root, 'checks/provider-leak-gate.mjs'),
  join(root, 'checks/llm-facing-format-gate.mjs'),
  join(root, 'checks/language-parity-gate.mjs'),
  join(root, 'checks/prompt-depth-ratchet.mjs'),
  join(root, 'checks/provider-prose-ownership.mjs'),
  join(root, 'checks/g4r-ce-vocabulary.mjs'),
  join(root, 'checks/test-boundary.mjs'),
  join(root, 'checks/js-boundary-gate.mjs'),
  join(root, 'checks/e2e-watchdog-feed.mjs'),
  join(root, 'checks/requirement-trace.mjs'),
]

for (const script of checks) {
  const args = [script]
  if (script.endsWith('dsl-ownership.mjs')) args.push('--threshold=0')
  if (script.endsWith('fsharp-control-pyramid.mjs')) args.push('--root=src/Wanxiangshu')
  const result = spawnSync(process.execPath, args, { stdio: 'inherit', env: process.env })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

process.exit(0)
