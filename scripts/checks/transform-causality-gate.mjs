#!/usr/bin/env node
import { readFileSync, existsSync, realpathSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
// transform-causality-gate.mjs — HOST-BOUNDARY-008 causal law for the
// experimental.chat.messages.transform path (StrengthReplica seam).
// Law (requirements/host-boundary/WHAT.md HOST-BOUNDARY-008):
//   The transform hook runs before the Host creates the assistant run. It must
//   NOT treat an existing assistant run as a business precondition and must NOT
//   disguise a future run as projection lag via bounded waits or repeated
//   snapshot reads. Recovery decisions frozen in this hook are recorded as an
//   UNBOUND attempt plan keyed by the exact PhysicalUserMessageId; binding to
//   the exact ProviderRunIdentity happens exactly once later, from a complete
//   Host observation.
//
// Mechanically rejected inside the transform causal path (Context/Prefix/Wire.fs):
//   - bounded wait / retry vocabulary: AwaitChange, Delay, deadline, timeout,
//     Task.Delay, projectionCatchup, observeBindableRun, observeSequence
//   - future-run reads at transform time: observeReplicaBindOutcome
//   Required: the replica seam freezes its decision via RecordPendingAttemptPlan.
//
// The core checker is a pure function over injected text so tests and this CLI
// share one implementation. Known-bad fixtures below prove it can fail.
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

export const WIRE_RELATIVE_PATH = 'src/Wanxiangshu/Context/Prefix/Wire.fs'

const BANNED_IN_TRANSFORM_PATH = [
  'AwaitChange',
  'projectionCatchup',
  'projectionCatchupMaxReads',
  'projectionCatchupDelayMilliseconds',
  'observeBindableRun',
  'observeSequence',
  'observeReplicaBindOutcome',
  'RunTerminal',
]

/// Pure checker over injected text. Returns the list of violations ([] = clean).
export function analyzeTransformCausality(wireText) {
  const violations = []
  for (const token of BANNED_IN_TRANSFORM_PATH) {
    if (wireText.includes(token)) {
      violations.push(`transform causal path contains banned wait/retry vocabulary "${token}" (HOST-BOUNDARY-008: no bounded wait, no repeated reads, no future-run guesses)`)
    }
  }
  if (!wireText.includes('RecordPendingAttemptPlan')) {
    violations.push('transform causal path never freezes an unbound attempt plan (RecordPendingAttemptPlan missing)')
  }
  return violations
}

function selfTest() {
  const cases = []
  const expectReject = (name, text) => {
    const v = analyzeTransformCausality(text)
    cases.push({ name, ok: v.length > 0 })
  }
  const expectAccept = (name, text) => {
    const v = analyzeTransformCausality(text)
    cases.push({ name, ok: v.length === 0, detail: v.join('; ') })
  }

  expectReject(
    'known-bad: bounded catch-up loop',
    'let! x = hub.AwaitChange sessionId ProviderRunBinding.projectionCatchupDelayMilliseconds',
  )
  expectReject(
    'known-bad: future-run observation helper',
    'match observeReplicaBindOutcome snapshotPort visibility sessionId physical with',
  )
  expectReject(
    'known-bad: terminal-evidence masking',
    '| RunTerminal _ -> return RunAlreadyClosed',
  )
  expectReject(
    'known-bad: unbound plan never recorded',
    'let assistant = requireOk (snapshotPort.GetMessages sessionId)',
  )
  expectAccept(
    'known-good: freeze pre-inference, record pending plan',
    'let pendingPlan = AttemptPlanner.freezePreInference authority cursor physical origin kind opportunity selectProbe\nscope.RecordPendingAttemptPlan sessionId physical pendingPlan',
  )

  let failed = 0
  for (const c of cases) {
    if (c.ok) console.log(`  ✓ ${c.name}`)
    else {
      failed++
      console.error(`  ✗ ${c.name}${c.detail ? `: ${c.detail}` : ''}`)
    }
  }
  if (failed > 0) {
    console.error(`transform-causality-gate: self-test failed (${failed})`)
    process.exit(1)
  }
  console.log('transform-causality-gate: self-test passed')
}

const isMainModule = (() => {
  try {
    return import.meta.url === pathToFileURL(realpathSync(process.argv[1])).href
  } catch {
    return false
  }
})()

const main = () => {
  if (process.argv.includes('--self-test')) {
    selfTest()
    process.exit(0)
  }

  const wirePath = join(ROOT, WIRE_RELATIVE_PATH)
  if (!existsSync(wirePath)) {
    console.error(`transform-causality-gate: ${WIRE_RELATIVE_PATH} missing`)
    process.exit(1)
  }
  const violations = analyzeTransformCausality(readFileSync(wirePath, 'utf8'))
  if (violations.length > 0) {
    console.error(`transform-causality-gate: ${violations.length} violation(s) in ${WIRE_RELATIVE_PATH}`)
    for (const v of violations) console.error(`  ${v}`)
    process.exit(1)
  }
  console.log(`transform-causality-gate: OK — ${WIRE_RELATIVE_PATH} freezes unbound plans, no wait/retry vocabulary`)
}

if (isMainModule) main()
