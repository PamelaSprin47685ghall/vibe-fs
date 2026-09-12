// port-observation-timing.test.mjs — Deterministic observation-timing contract
// for the journal-port adapters introduced by the port migration.
//
// Contract pinned here: each port member performs its journal snapshot at call
// time. A member's reads MUST appear lexically inside that member's `fun` body
// (or a lazy `projections ()` thunk invoked from inside it); nothing may bind
// `AgentJournal.snapshot journal` or `journal.IsPoisoned` at adapter
// construction time, ahead of the member record literal.
//
// The earlier form of these assertions drove the real journal through
// dist internals; `scanAll` rightly charged that as JS-boundary debt.
// These source contracts fail against the frozen-at-construction regression
// (each member's capture `let`s would sit before the record) and hold under
// any implementation that still reads on member call.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const ADAPTER = 'src/Wanxiangshu/Composition/Durable/AgentJournalPortAdapter.fs'

const adapterSource = () => readFileSync(join(ROOT, ADAPTER), 'utf8')

/** Slice the adapter source from `let <name>` to the next top-level binding. */
const functionBlock = (source, name) => {
  const start = source.indexOf(`    let ${name} `)
  assert.notEqual(start, -1, `${name} must exist in ${ADAPTER}`)
  const rest = source.slice(start + 1)
  const next = rest.search(/\n    let /)
  return next === -1 ? rest : rest.slice(0, next)
}

/** Slice a member: `Foo = fun args ->` through just before the next `Xxx = fun` or block end. */
const memberBlock = (block, member) => {
  const start = block.indexOf(`${member} =\n`) !== -1
    ? block.indexOf(`${member} =\n`)
    : block.indexOf(`${member} = fun`)
  assert.notEqual(start, -1, `member ${member} must exist`)
  const rest = block.slice(start)
  const next = rest.slice(1).search(/\n\s{8,10}[A-Z][A-Za-z]*\s*=\s*|^\s*\}\s*\r?\n?$/m)
  return next === -1 ? rest : rest.slice(1, next + 1)
}

// Extraction regions: the adapter header region before the record literal is
// where a regression would park a construction-time capture.
const preRecordHead = (block) => {
  const recordAt = block.indexOf('{')
  assert.notEqual(recordAt, -1, 'adapter must open a record literal')
  return block.slice(0, recordAt)
}

test('WHAT[DURABLE-EVENTS-023] PORT_MEMBER_READS: every member reads the journal at call time, not at adapter construction', () => {
  const source = adapterSource()

  const cases = [
    {
      ctor: 'forTurnObservation',
      members: ['TryBloggerReceiptKind', 'TryContinuationKind', 'IsFissionActive'],
      reads: ['AgentJournal.snapshot journal', 'projections ()'],
    },
    {
      ctor: 'forTerminalPolicy',
      members: ['IsPoisoned', 'HasListableHandles', 'HasActiveOrchestratorJobs', 'IsLinkedChild', 'TryCanonicalRole'],
      reads: ['AgentJournal.snapshot journal', 'journal.IsPoisoned', 'projections ()'],
    },
    {
      ctor: 'forHostJoinGuard',
      members: ['HasOutstandingJoinClaim'],
      reads: ['AgentJournal.snapshot journal'],
    },
  ]

  for (const { ctor, members, reads } of cases) {
    const block = functionBlock(source, ctor)
    // The ctor preamble (before the member record literal) may only bind a
    // lazy thunk such as `let projections () = ...`; a non-function eager
    // binding of a journal read there is exactly the regression.
    const preamble = preRecordHead(block)
    assert.doesNotMatch(
      preamble,
      /let\s+\w+\s*=\s*(?!.*->)[^=\n]*(?:AgentJournal\.snapshot journal|journal\.IsPoisoned)[^=\n]*$/m,
      `${ctor} must not bind an eager snapshot/poison capture before the member record`,
    )
    // Contract: the whole port still reads through one snapshot within a
    // member call (all member-declared reads sit inside `fun` scope, or in a
    // lazy thunk the member calls); nothing sits in the pre-record head.
    for (const member of members) {
      const memberBody = memberBlock(block, member)
      const readSighted = reads.some((read) => memberBody.includes(read) ||
        (memberBody.includes('projections ()') && reads.includes('projections ()')))
      assert.ok(
        readSighted,
        `${ctor}.${member} must perform a call-time read inside its fun body (or through the lazy projections thunk)`,
      )
    }
  }
})

test('WHAT[DURABLE-EVENTS-023] PORT_CONTRACT_TEXT: port contracts document call-time reads, matching the adjudicated observation point', () => {
  // The contract `fsi`s previously claimed every member "derives from exactly
  // one Journal Snapshot revision captured when the port is built" — the exact
  // wording of the regression these adapters carried. The contract must keep
  // the call-time promise legible to consumers.
  const contracts = [
    'src/Wanxiangshu/Composition/Turn/TurnObservationPort.fsi',
    'src/Wanxiangshu/OpenCode/Host/TerminalPolicyPort.fsi',
    'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/HostJoinGuardJournalPort.fsi',
  ]
  for (const rel of contracts) {
    const text = readFileSync(join(ROOT, rel), 'utf8')
    assert.match(text, /call time|per call/i, `${rel} must document call-time snapshot reads`)
    assert.doesNotMatch(text, /captured when the port is built/i, `${rel} must not promise construction-time capture`)
  }
})

test('WHAT[DURABLE-EVENTS-023] RECOVERY_VIEW_SEPARATE_ADAPTER: OrchestratorJournalAdapter is the only place that cuts the recovery view', () => {
  // The recovery-view seam must live in its own adapter shard module, not back
  // inside the shared durable adapter that middle layers import for unrelated
  // consumers. A rebound `forOrchestrator*` inside the hub would reintroduce
  // the shared gravity well this batch split.
  const source = adapterSource()
  assert.doesNotMatch(source, /forOrchestrator/, `${ADAPTER} must not own orchestrator port adapters — they live in Change/Orchestrator/OrchestratorJournalAdapter`)
  const recoveryContract = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Change/Orchestrator/OrchestratorPort.fsi'),
    'utf8',
  )
  assert.match(recoveryContract, /RecoveryView/, 'OrchestratorSweepPort must expose RecoveryView')
  assert.doesNotMatch(
    recoveryContract,
    /Snapshot\s*:\s*unit\s*->\s*ProjectionSet|AgentProjectionSet|AgentJournal/,
    'OrchestratorSweepPort must not hand back the fat ProjectionSet',
  )
})

test('WHAT[DURABLE-EVENTS-023] PROVIDER_RECOVERY_PORT_EXCISED: provider-recovery dead port stays excised', () => {
  // `forProviderRecovery` produced `ProviderRecoveryJournalPort`, which no
  // consumer ever called. This batch removed the port contract, its adapter,
  // and the dead `recoveryPort` parameter at Fallback/Workflow — assert the
  // symbols stay gone so the dead surface cannot silently return.
  const source = adapterSource()
  assert.doesNotMatch(source, /forProviderRecovery|ProviderRecoveryJournalPort/, `${ADAPTER} must not keep the dead provider-recovery adapter`)
  const workflowContract = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fsi'),
    'utf8',
  )
  assert.doesNotMatch(
    workflowContract,
    /ProviderRecoveryJournalPort|recoveryPort/,
    'Fallback/Workflow.fsi must not re-introduce the dead provider-recovery parameter',
  )
})
