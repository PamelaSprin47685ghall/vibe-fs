#!/usr/bin/env node
// Sequential focused checks: spec then architecture.
// Usage: node scripts/check.mjs

import { spawnSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)))
const checks = [
  join(root, 'checks/spec.mjs'),
  join(root, 'checks/architecture.mjs'),
  join(root, 'checks/provider-projection-boundary.mjs'),
  join(root, 'checks/semantic-owners.mjs'),
  join(root, 'checks/owner-dependencies.mjs'),
  join(root, 'checks/dsl-ownership.mjs'),
  join(root, 'checks/authority-boundary.mjs'),
  join(root, 'checks/fsharp-control-pyramid.mjs'),
  join(root, 'checks/migration-ledger.mjs'),
  join(root, 'checks/plugin-transforms-invariant.mjs'),
  join(root, 'checks/interaction-repair-invariant.mjs'),
  join(root, 'checks/composition-root-invariant.mjs'),
  join(root, 'checks/semantic-decorator-invariant.mjs'),
  join(root, 'checks/deadcode.mjs'),
  join(root, 'checks/p0-recovery-join.mjs'),
  join(root, 'checks/causal-wait-boundary.mjs'),
  join(root, 'checks/cross-callback-pc.mjs'),
  join(root, 'checks/session-ownership-ratchet.mjs'),
  join(root, 'checks/js-surface-gate.mjs'),
  // js-surface-manifest moved to build.mjs post-compile validation: it requires
  // emitted dist/ surfaces and cannot pass pre-build with partial/missing dist.
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
  // REQUIREMENT-SYSTEM-018: test↔WHAT bidirectional closure. Hard cutover
  // (2026-08-16): the migration ratchet is deleted; this gate is an absolute
  // prohibition — any orphan/unknown/multi-primary/unproved test is RED.
  join(root, 'checks/requirement-trace.mjs'),
]

const fcsRunId = randomUUID()
const fcsEvidencePath = resolve(root, '../.fable-build/owner-dependencies-fcs/normalized-evidence.json')
let ownerEvidenceReady = false

for (const script of checks) {
  const args = [script]
  // --threshold freezes current Direct-CE debt baseline; must only ever decrease.
  // P0→P2-3c: 157→13. P2-2 Host boundary open allowlist: 13→0.
  if (script.endsWith('dsl-ownership.mjs')) args.push('--threshold=0')
  // STRUCTURED-WORKFLOW-016: nested lexical decisions are an absolute zero gate.
  if (script.endsWith('fsharp-control-pyramid.mjs')) args.push('--root=src/Wanxiangshu')
  const env = { ...process.env }
  delete env.OMP_FCS_REUSE_PATH
  delete env.OMP_FCS_REUSE_RUN_ID
  delete env.OMP_FCS_NORMALIZED_OUTPUT_PATH
  if (script.endsWith('owner-dependencies.mjs')) {
    env.OMP_FCS_EVIDENCE_RUN_ID = fcsRunId
    env.OMP_FCS_NORMALIZED_OUTPUT_PATH = fcsEvidencePath
  } else delete env.OMP_FCS_EVIDENCE_RUN_ID
  if (ownerEvidenceReady) {
    env.OMP_FCS_REUSE_PATH = fcsEvidencePath
    env.OMP_FCS_REUSE_RUN_ID = fcsRunId
  }
  const result = spawnSync(process.execPath, args, { stdio: 'inherit', env })
  if (result.status !== 0) process.exit(result.status ?? 1)
  if (script.endsWith('owner-dependencies.mjs')) ownerEvidenceReady = true
}

process.exit(0)
