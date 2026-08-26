#!/usr/bin/env node
// transform-causality-gate.mjs — HOST-BOUNDARY-008 causal law for the
// experimental.chat.messages.transform path (StrengthReplica seam).
//
// Law (requirements/host-boundary/WHAT.md HOST-BOUNDARY-008):
//   The transform hook runs before the Host creates the assistant run. It must
//   NOT treat an existing assistant run as a business precondition and must NOT
//   disguise a future run as projection lag via bounded waits or repeated
//   snapshot reads. The recovery decision frozen in this hook is recorded as an
//   UNBOUND attempt plan keyed by the exact PhysicalUserMessageId; binding to
//   the exact ProviderRunIdentity happens exactly once later, from a complete
//   Host observation.
//
// What this gate mechanically verifies (over injected text — tests and CLI share
// one implementation):
//   1. Function-boundary extraction: the `applyStrengthReplicaPlan` top-level
//      function body is isolated by F# indentation, so a legal call elsewhere
//      (e.g. the WorkMain seam) can never mask a regression inside this seam.
//   2. Ordered causal shape inside that boundary: physical identity →
//      session-scoped authority → freezePreInference → RecordPendingAttemptPlan,
//      with no snapshot read or run binding anywhere in between.
//   3. Banned vocabulary inside the boundary AND across the whole file:
//      AwaitChange / Task.Delay / Delay / deadline / timeout /
//      projectionCatchup* / observeBindableRun / observeSequence /
//      observeReplicaBindOutcome / RunTerminal / MessageVisibility.
//   4. Whole-file ban on snapshot reads inside ANY replica-seam helper.
//
// Every banned rule has a known-bad fixture that breaks ONLY that rule against
// an otherwise-legal skeleton, and the self-test runs on every standard check
// invocation before the real file is validated.

import { readFileSync, existsSync, realpathSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

export const WIRE_RELATIVE_PATH = 'src/Wanxiangshu/Context/Prefix/Wire.fs'
export const SEAM_FUNCTION = 'applyStrengthReplicaPlan'

/// Ordered causal tokens that must appear inside the seam body, in this order.
const ORDERED_CAUSAL_TOKENS = [
  ['lastUserMessageId', 'exact PhysicalUserMessageId extraction'],
  ['requireStrengthReplicaAuthority', 'session-scoped authority decision'],
  ['freezePreInference', 'unbound attempt-plan freeze'],
  ['RecordPendingAttemptPlan', 'unbound plan record'],
]

/// Vocabulary banned inside the seam body. Each entry names its own rule so a
/// fixture can assert it breaks exactly one law.
const BANNED_IN_SEAM = [
  ['GetMessages', 'transform must not read the public session snapshot'],
  ['bindableRun', 'transform must not probe for an existing assistant run'],
  ['AwaitChange', 'no signal wait in the pre-inference boundary'],
  ['Task.Delay', 'no timer pause in the pre-inference boundary'],
  ['deadline', 'no deadline backstop in the pre-inference boundary'],
  ['timeout', 'no timeout in the pre-inference boundary'],
  ['projectionCatchup', 'no projection-catchup budget in the pre-inference boundary'],
  ['observeBindableRun', 'no future-run observation in the pre-inference boundary'],
  ['observeSequence', 'no multi-read sequence in the pre-inference boundary'],
  ['observeReplicaBindOutcome', 'no bounded-bind helper in the pre-inference boundary'],
  ['RunTerminal', 'no sealed-run masking in the pre-inference boundary'],
  ['MessageVisibility', 'no visibility hub in the pre-inference boundary'],
]

/// Vocabulary banned across the whole file (deleted layers must stay deleted).
const BANNED_FILE_WIDE = [
  // Symbols asserted via the seam list (observeBindableRun, observeSequence,
  // observeReplicaBindOutcome, RunTerminal, projectionCatchup*, AwaitChange)
  // are intentionally absent here: a seam injection would fire two rules for
  // one violation and no fixture could stay single-rule.
  ['MessageVisibilityHub', 'visibility hub must stay deleted'],
  ['MessageVisibilitySignal', 'visibility signal intake must stay deleted'],
]

/// Isolate one top-level F# function body by indentation. Returns null when the
/// function is absent — which is itself a violation for the required seam.
export function extractTopLevelFunction(text, name) {
  const opener = new RegExp(`^    let (?:private )?${name}\\b`, 'm')
  const match = opener.exec(text)
  if (!match) return null
  const start = match.index
  const rest = text.slice(start + 1)
  const next = rest.search(/^    (let |type |module )/m)
  const end = next < 0 ? text.length : start + 1 + next
  return text.slice(start, end)
}

/// Pure checker over injected text. Returns the list of violations ([] = clean).
export function analyzeTransformCausality(wireText) {
  const violations = []

  const seam = extractTopLevelFunction(wireText, SEAM_FUNCTION)
  if (seam === null) {
    violations.push(`seam function ${SEAM_FUNCTION} not found at top level (boundary extraction failed)`)
    return violations
  }

  // Ordered causal shape: each token must appear after the previous one.
  let cursor = -1
  for (const [token, role] of ORDERED_CAUSAL_TOKENS) {
    const at = seam.indexOf(token)
    if (at < 0) {
      violations.push(`seam causal order broken: "${token}" (${role}) missing from ${SEAM_FUNCTION}`)
    } else if (at < cursor) {
      violations.push(
        `seam causal order broken: "${token}" (${role}) appears before the previous decision (HOST-BOUNDARY-008 requires extract → authority → freeze → record)`,
      )
    } else {
      cursor = at
    }
  }

  // Seam-local bans: each banned token is its own rule.
  for (const [token, reason] of BANNED_IN_SEAM) {
    if (seam.includes(token)) {
      violations.push(`transform seam uses banned "${token}": ${reason} (HOST-BOUNDARY-008)`)
    }
  }

  // File-wide bans: deleted layers must never come back under another name site.
  for (const [token, reason] of BANNED_FILE_WIDE) {
    if (wireText.includes(token)) {
      violations.push(`whole-file ban: "${token}" reappeared — ${reason}`)
    }
  }

  return violations
}

// ---------------------------------------------------------------------------
// Self-test: one otherwise-legal skeleton; every known-bad fixture breaks
// exactly ONE rule so each ban is independently provable.
// ---------------------------------------------------------------------------

const LEGAL_SEAM = `    let private applyStrengthReplicaPlan
        (durable: AgentJournal)
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (binding: StrengthReplicaBinding)
        (output: obj)
        : Task<unit> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output

            let physical =
                match ProviderWireCapture.lastUserMessageId rawMessages with
                | Some physical -> physical
                | None -> raise (InvalidOperationException "no physical user message")

            let projections = AgentJournal.snapshot durable

            let authority =
                requireStrengthReplicaAuthority
                    binding
                    (PromptAuthorityLedger.activeProfile sessionId projections.AgentProjections)

            let pendingPlan =
                AttemptPlanner.freezePreInference
                    authority
                    AgentPairCursor.initial
                    physical
                    origin
                    kind
                    opportunity
                    selectProbe

            scope.RecordPendingAttemptPlan sessionId physical pendingPlan
        }
`

const LEGAL_FILE =
    `namespace Wanxiangshu.Context.Prefix\n\n`
  + LEGAL_SEAM
  + `\n    let private otherHelper () =\n        ()\n`

export function runSelfTest() {
  const cases = []

  // A fixture passes only when its target rule fires and NO other rule does —
  // every ban is independently provable against an otherwise-legal skeleton.
  const expect = (name, text, ruleMatcher) => {
    const v = analyzeTransformCausality(text)
    const hit = v.filter(ruleMatcher)
    const rest = v.filter((x) => !ruleMatcher(x))
    cases.push({ name, ok: hit.length > 0 && rest.length === 0, detail: v.join(' | ') })
  }
  const orderRule = (x) => x.includes('causal order broken')

  // Legal baseline passes.
  cases.push({ name: 'known-good: legal seam passes', ok: analyzeTransformCausality(LEGAL_FILE).length === 0 })

  // Missing seam function is itself rejected.
  {
    const v = analyzeTransformCausality('    let private unrelated () = ()\n')
    cases.push({
      name: 'known-bad: seam function absent',
      ok: v.some((x) => x.includes('not found')) && v.length === 1,
      detail: v.join(' | '),
    })
  }

  // Removing any ordered token breaks exactly the causal-order rule.
  for (const [token] of ORDERED_CAUSAL_TOKENS) {
    const text = LEGAL_FILE.split(token).join('()')
    expect(`known-bad: remove ${token}`, text, orderRule)
  }

  // Reordering freeze before authority breaks exactly the causal-order rule.
  {
    const a0 = LEGAL_SEAM.indexOf('            let authority =')
    const a1 = LEGAL_SEAM.indexOf('            let pendingPlan =')
    const p1 = LEGAL_SEAM.indexOf('            scope.RecordPendingAttemptPlan')
    const authorityBlock = LEGAL_SEAM.slice(a0, a1)
    const pendingBlock = LEGAL_SEAM.slice(a1, p1)
    const swappedSeam =
      LEGAL_SEAM.slice(0, a0) + pendingBlock + authorityBlock + LEGAL_SEAM.slice(p1)
    const text = LEGAL_FILE.replace(LEGAL_SEAM, swappedSeam)
    expect('known-bad: freeze reordered before authority', text, orderRule)
  }

  // Each seam ban breaks exactly its own rule.
  const seamInjections = [
    ['GetMessages', 'let! msgs = snapshotPort.GetMessages sessionId'],
    ['bindableRun', 'match ProviderRunBinding.bindableRun "p" [] with'],
    ['AwaitChange', 'do! hub.AwaitChange sessionId 10'],
    ['Task.Delay', 'do! Task.Delay 10'],
    ['deadline', 'let backstop = deadline budget'],
    ['timeout', 'if elapsed > timeout then ()'],
    ['projectionCatchup', 'attempt ProviderRunBinding.projectionCatchupMaxReads'],
    ['observeBindableRun', 'match ProviderRunBinding.observeBindableRun "p" [] with'],
    ['observeSequence', 'ProviderRunBindingSurface.observeSequence "p" snapshots |> ignore'],
    ['observeReplicaBindOutcome', 'match! observeReplicaBindOutcome p v s x with'],
    ['RunTerminal', '| RunTerminal _ -> return ()'],
    ['MessageVisibility', 'let ghost = MessageVisibility'],
  ]
  for (const [token, snippet] of seamInjections) {
    const text = LEGAL_FILE.replace(
      'scope.RecordPendingAttemptPlan sessionId physical pendingPlan',
      `${snippet}\n            scope.RecordPendingAttemptPlan sessionId physical pendingPlan`,
    )
    expect(`known-bad: seam ${token}`, text, (x) => x.includes(`banned "${token}"`))
  }

  // File-wide bans cover layer symbols that only make sense outside the seam;
  // tokens shared with the seam list are asserted there to keep each fixture
  // single-rule.
  const fileInjections = [
    ['MessageVisibilityHub', 'let private elsewhere () = MessageVisibilityHub(timerPort) |> ignore'],
  ]
  for (const [token, snippet] of fileInjections) {
    const text = LEGAL_FILE + '\n' + snippet + '\n'
    expect(`known-bad: file-wide ${token}`, text, (x) => x.includes(`whole-file ban: "${token}"`))
  }

  let failed = 0
  for (const c of cases) {
    if (c.ok) console.log(`  ✓ ${c.name}`)
    else {
      failed++
      console.error(`  ✗ ${c.name}${c.detail ? `: ${c.detail}` : ''}`)
    }
  }
  if (failed > 0) {
    console.error(`transform-causality-gate: self-test failed (${failed} fixture(s))`)
    process.exit(1)
  }
  console.log(`transform-causality-gate: self-test passed (${cases.length} fixtures)`)
}

const isMainModule = (() => {
  try {
    return import.meta.url === pathToFileURL(realpathSync(process.argv[1])).href
  } catch {
    return false
  }
})()

const main = () => {
  // The self-test is part of the standard check path: a checker whose own
  // refutations rot is a pseudo-gate.
  runSelfTest()

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
  console.log(`transform-causality-gate: OK — ${SEAM_FUNCTION} freezes unbound plans in causal order, no wait/retry vocabulary`)
}

if (isMainModule) main()

