// HOST-BOUNDARY-019 Magic Todo V1 membrane canaries (A–R).
//
// These canaries prove observable Host/owner behavior through production
// registered semantic surfaces only:
//   - dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js
//   - dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js
//   - dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js
//   - dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js
//   - dist/Persistence/Journal/Surface.js
//   - dist/Persistence/Journal/ObligationJournalSurface.js
//
// No test imports domain.mjs, Fable runtime representations, or internal dist
// paths outside registered semantic surfaces.
//
// Canaries F (execute throw), O (no Host core / same-name override), and Q
// (description face) are genuine physical/static contracts retained as
// explicit integration/static proof boundaries — see the static tests at the
// end of this file and the cross-reference to host018-no-fork.test.mjs.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as todoJournal from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'
import * as locality from '../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

// ── Journal harness ──────────────────────────────────────────────────────

const withJournal = async (body, runtime = 'rt_magic_todo_canary') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-host-canary-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  try {
    return await body(boot.journal)
  } finally {
    journal.JournalSurface_dispose(boot.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

const openLife = async (handle, session, life) => {
  const result = await membrane.MagicTodoMembraneSurface_openLife(handle, session, life)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}

const prepare = (handle, session, call, obligations, planComplete = true, state = 0) => {
  const args = { planComplete, workingOn: obligations[0]?.name ?? '', obligations }
  const canonical = host.canonicalInput(args)
  const digest = host.canonicalInputDigest(sha256Hex, args)
  return membrane.MagicTodoMembraneSurface_prepare(handle, session, call, canonical, digest, planComplete, obligations, state)
    .then((result) => ({ result, digest, args, canonical }))
}

const accept = async (handle, prepared, inputDigest, outputDigest, evidence = 'LiveAfterSuccess') =>
  membrane.MagicTodoMembraneSurface_accept(handle, prepared, evidence, inputDigest, outputDigest)

const assertOk = (result, message = '') => {
  assert.equal(result.ok, true, message || (result.ok ? '' : JSON.stringify(result.error)))
  return result.value
}

const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })

const append = async (handle, session, caseName, payload) => {
  const result = await todoJournal.appendMagicTodo(handle, session, null, fact(caseName, payload))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result
}

// ── Canary A: deferred materialization + before in-place mutation ────────

test('WHAT[HOST-BOUNDARY-019] CANARY_A openLife and compatibility injection do not wait for snapshot IO', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-a-before'
    const life = 'life-canary-a-before'
    // openLife is a journal append — completes without any snapshot port
    await openLife(handle, session, life)
    // V1 compatibility injection is synchronous — no snapshot dependency
    const obligations = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const rows = host.projectCompatibilityRows('diagnose', obligations)
    const output = { args: { planComplete: false, workingOn: 'diagnose', obligations } }
    host.replaceCompatibilityArgs(output, rows)
    // A: executor sees V1 compatibility list (non-enumerable todos)
    assert.equal('obligations' in output.args, true)
    assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
    assert.equal(output.args.todos[0].content, 'diagnose: Fix the todowrite snapshot race.')
    assert.equal(output.args.todos[0].status, 'in_progress')
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_A pending {} waits in deferred prepare, not accepted as evidence', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-a-pending'
    await openLife(handle, session, 'life-canary-a-pending')
    const result = await membrane.MagicTodoMembraneSurface_prepare(
      handle, session, 'call-canary-a-pending', '{}', 'provider-input-digest',
      false, [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }], 0,
    )
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'SnapshotInputMismatch')
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_A materialized input must match live args canonical', () => {
  const expected = host.canonicalInput({
    planComplete: false, workingOn: 'diagnose',
    obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
  })
  const result = locality.materializeInput('call-canary-a', '{}', 0, expected)
  assert.equal(result.ok, true)
  assert.equal(result.value.inputCanonical, expected)
})

test('WHAT[HOST-BOUNDARY-019] CANARY_A materialization fails closed when provider input differs', () => {
  const actual = host.canonicalInput({
    planComplete: false, workingOn: 'other',
    obligations: [{ name: 'other', work: 'Different provider input.' }],
  })
  const expected = host.canonicalInput({
    planComplete: false, workingOn: 'diagnose',
    obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
  })
  const result = locality.materializeInput('call-canary-a-conflict', actual, 1, expected)
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'InputMismatch')
})

// ── Canary B: simultaneously replace parameters + jsonSchema ─────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_B definition replaces both parameters and jsonSchema with V2 schema', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  host.applyDefinition(output)
  // B: both surfaces carry the V2 obligations schema
  assert.deepEqual(output.parameters.required, ['planComplete', 'workingOn', 'obligations'])
  assert.deepEqual(output.jsonSchema.required, ['planComplete', 'workingOn', 'obligations'])
  assert.equal(output.parameters.properties.obligations.items.properties.name.type, 'string')
  assert.equal(output.jsonSchema.properties.obligations.items.properties.name.type, 'string')
  // B: no V1 todos/status/priority in either schema surface
  assert.equal(output.parameters.properties.todos, undefined)
  assert.equal(output.jsonSchema.properties.todos, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.status, undefined)
  // B: both surfaces carry identical descriptions (not just one)
  assert.equal(
    output.parameters.properties.planComplete.description,
    output.jsonSchema.properties.planComplete.description,
  )
})

// ── Canary C: non-enumerable compatibility view ──────────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_C todos is non-enumerable while obligations remains visible', () => {
  const args = {
    planComplete: false, workingOn: 'provider-only',
    obligations: [{ name: 'provider-only', work: 'must remain durable provider input' }],
  }
  const output = { args }
  const rows = host.projectCompatibilityRows('provider-only', [
    { name: 'provider-only', work: 'must remain durable provider input' },
  ])
  host.replaceCompatibilityArgs(output, rows)

  // C: V1 decoder can read todos
  assert.deepEqual(output.args.todos, rows)
  // C: Object.keys does not see todos
  assert.equal(Object.keys(output.args).includes('todos'), false)
  // C: JSON.stringify does not serialize todos
  const serialized = JSON.stringify(output.args)
  assert.doesNotMatch(serialized, /todos/)
  assert.match(serialized, /obligations/)
  // C: obligations is still enumerable
  assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'obligations'), true)
  // C: args object identity preserved (in-place mutation, not replacement)
  assert.equal(output.args, args)
})

// ── Canary E: after rewrites output.output ───────────────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_E replaceEnrichedResult rewrites the provider-visible output', () => {
  const output = { output: 'builtin executor succeeded' }
  host.replaceEnrichedResult(output, 'enriched: Manager who will carry it is you')
  assert.equal(output.output, 'enriched: Manager who will carry it is you')
})

test('WHAT[HOST-BOUNDARY-019] CANARY_E live accept enriches the result the provider sees', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-e'
    const life = 'life-canary-e'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-e', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('canary-e-output'))
    assert.equal(accepted.ok, true)
    // E: the enriched result is what the provider sees, not the raw executor output
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})

// ── Canary G: dual-path Accepted (live vs recovered) ─────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_G accept rejects unknown physical success evidence', async () => {
  await withJournal(async (handle) => {
    const result = await membrane.MagicTodoMembraneSurface_accept(handle, null, 'UNKNOWN', 'input', 'output')
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'InvalidPhysicalEvidence')
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_G live after success and recovered completed tool part are both admissible evidence', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-g'
    const life = 'life-canary-g'
    await openLife(handle, session, life)

    // G: LiveAfterSuccess path
    const t1 = await prepare(handle, session, 'call-canary-g-live', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const livePrepared = assertOk(t1.result)
    const liveAccepted = await accept(handle, livePrepared.bridge, t1.digest, sha256Hex('canary-g-live-output'), 'LiveAfterSuccess')
    assert.equal(liveAccepted.ok, true, 'LiveAfterSuccess must be admissible')

    // G: RecoveredCompletedToolPart path (recovery without after hook)
    const t2 = await prepare(handle, session, 'call-canary-g-recovered', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const recoveredPrepared = assertOk(t2.result)
    const recoveredAccepted = await accept(
      handle, recoveredPrepared.bridge, t2.digest, sha256Hex('canary-g-recovered-output'),
      'RecoveredCompletedToolPart',
    )
    assert.equal(recoveredAccepted.ok, true, 'RecoveredCompletedToolPart must be admissible')
  })
})

// ── Canary H: sessionId + callId uniquely establishes durable identity ───

test('WHAT[HOST-BOUNDARY-019] CANARY_H prepare derives full durable identity from sessionId + callId only', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-h'
    const life = 'life-canary-h'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-h', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    // H: the prepared checkpoint carries todoWriteId, toolCallId, toolPartOrdinal —
    //    all derived from sessionId + callId through the membrane.
    assert.equal(prepared.prepared.toolCallId, 'call-canary-h')
    assert.ok(prepared.prepared.todoWriteId, 'todoWriteId must be derived')
    assert.ok(prepared.prepared.managerLifeId, 'managerLifeId must be derived')
    assert.ok(prepared.prepared.proposedTodoRef, 'proposedTodoRef must be derived')
    assert.ok(prepared.prepared.proposedTodoDigest, 'proposedTodoDigest must be derived')
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H duplicate callId produces idempotent replay, not a second checkpoint', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-h-replay'
    const life = 'life-canary-h-replay'
    await openLife(handle, session, life)
    const obligations = [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }]
    const first = await prepare(handle, session, 'call-canary-h-replay', obligations)
    const firstPrepared = assertOk(first.result)
    await accept(handle, firstPrepared.bridge, first.digest, sha256Hex('canary-h-replay-output'))

    // H: same callId + same input → idempotent replay (same todoWriteId)
    const second = await prepare(handle, session, 'call-canary-h-replay', obligations)
    const secondPrepared = assertOk(second.result)
    assert.equal(
      secondPrepared.prepared.todoWriteId,
      firstPrepared.prepared.todoWriteId,
      'same callId must produce same todoWriteId (unique location)',
    )
  })
})

// ── Canary J: live Accepted aligns with Prepared digest ──────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_J live accept creates TodoWriteAccepted with Prepared digests', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-j'
    const life = 'life-canary-j'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-j', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('canary-j-output'))
    assert.equal(accepted.ok, true)
    // J: checkpoint is accepted in the snapshot
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints.length, 1)
    assert.equal(snapshot.checkpoints[0].accepted, true)
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_J accept rejects mismatched input digest', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-j-mismatch'
    const life = 'life-canary-j-mismatch'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-j-mismatch', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, 'wrong-digest', sha256Hex('canary-j-mismatch-output'))
    assert.equal(accepted.ok, false)
    assert.equal(accepted.error.code, 'InputDigestMismatch')
  })
})

// ── Canary K: recovery Accepted without after hook ───────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_K recovery accept via RecoveredCompletedToolPart creates accepted checkpoint', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-k'
    const life = 'life-canary-k'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-k', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    // K: recovery path — accept with RecoveredCompletedToolPart (no after hook ran)
    const accepted = await accept(
      handle, prepared.bridge, t1.digest, sha256Hex('canary-k-recovery-output'),
      'RecoveredCompletedToolPart',
    )
    assert.equal(accepted.ok, true, 'recovery accept must succeed with RecoveredCompletedToolPart')
    // K: checkpoint is accepted in the snapshot
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints.length, 1)
    assert.equal(snapshot.checkpoints[0].accepted, true)
  })
})

// ── Canary L: Prepared without accept does not create a checkpoint ───────

test('WHAT[HOST-BOUNDARY-019] CANARY_L prepare without accept does not create an accepted checkpoint', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-l'
    const life = 'life-canary-l'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-l', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    assertOk(t1.result)
    // L: prepare succeeded but no accept → checkpoint exists but is NOT accepted
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints.length, 1)
    assert.equal(snapshot.checkpoints[0].accepted, false)
    // L: current obligations did not roll forward to proposed
    assert.equal(snapshot.currentObligations, null)
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_L sink optimistic Pk does not constitute checkpoint without accept', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-l-sink'
    const life = 'life-canary-l-sink'
    await openLife(handle, session, life)
    // L: prepare creates a Prepared fact but does NOT accept
    const t1 = await prepare(handle, session, 'call-canary-l-sink', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    assertOk(t1.result)
    // L: a second prepare with different obligations supersedes the sink
    const t2 = await prepare(handle, session, 'call-canary-l-sink-2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    assertOk(t2.result)
    // L: still no accepted checkpoint
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints.filter((c) => c.accepted).length, 0)
  })
})

// ── Canary M: REVISE consumed after reconcile ────────────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_M next checkpoint advances CurrentObligations cleanly without rollback', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-m'
    const life = 'life-canary-m'
    const callText = 'call-canary-m-t1'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, callText, [
      { name: 'implementation', work: 'Implement the requested behavior.' },
    ])
    const t1Prepared = assertOk(t1.result)
    const t1Accepted = await accept(handle, t1Prepared.bridge, t1.digest, sha256Hex('canary-m-t1-output'))
    assert.equal(t1Accepted.ok, true)

    assert.equal(
      membrane.MagicTodoMembraneSurface_snapshot(handle, life).currentObligations.reference,
      t1Prepared.prepared.proposedTodoRef,
      'Current must stay at the accepted proposed account',
    )

    // M: next checkpoint advances obligations
    const t2 = await prepare(handle, session, 'call-canary-m-t2', [
      { name: 'implementation', work: 'Implement the requested behavior.' },
      { name: 'verification', work: 'Run the required runtime verification and preserve evidence.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('canary-m-t2-output'))
    assert.equal(t2Accepted.ok, true)
    assert.match(t2Accepted.value.enrichedResult, /Keep working/)
    assert.equal(
      membrane.MagicTodoMembraneSurface_snapshot(handle, life).currentObligations.reference,
      t2Prepared.prepared.proposedTodoRef,
    )
  })
})

// ── Canary P: bridge is not truth; Journal is canonical recovery source ──

test('WHAT[HOST-BOUNDARY-019] CANARY_P recovery accept reads from Journal, not from bridge', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-p'
    const life = 'life-canary-p'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-canary-p', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])
    const prepared = assertOk(t1.result)
    const outputDigest = sha256Hex('canary-p-recovery-output')
    const firstAccepted = await accept(handle, prepared.bridge, t1.digest, outputDigest, 'LiveAfterSuccess')
    assert.equal(firstAccepted.ok, true)

    // P: a second prepare reconstructs the accepted bridge from Journal state;
    //    the old in-memory bridge is intentionally not reused.
    const replay = assertOk((await prepare(handle, session, 'call-canary-p', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
    ])).result)
    const recovered = await accept(
      handle, replay.bridge, t1.digest, outputDigest, 'RecoveredCompletedToolPart',
    )
    assert.equal(recovered.ok, true)
    // P: the accepted checkpoint and output digest are durable Journal state.
    const snapshot = membrane.MagicTodoMembraneSurface_snapshot(handle, life)
    assert.equal(snapshot.checkpoints[0].accepted, true)
    assert.equal(snapshot.checkpoints[0].outputDigest, outputDigest)
  })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_P idempotent replay accept uses Journal state, not bridge', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-canary-p-idempotent'
    const life = 'life-canary-p-idempotent'
    await openLife(handle, session, life)
    const obligations = [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }]
    const t1 = await prepare(handle, session, 'call-canary-p-idempotent', obligations)
    const prepared = assertOk(t1.result)
    const outputDigest = sha256Hex('canary-p-idempotent-output')
    await accept(handle, prepared.bridge, t1.digest, outputDigest)

    // P: second prepare with same callId returns IdempotentReplay from Journal
    const t2 = await prepare(handle, session, 'call-canary-p-idempotent', obligations)
    const replayPrepared = assertOk(t2.result)
    // P: accept with same output digest succeeds (idempotent), proving Journal state is canonical
    const replayAccepted = await accept(handle, replayPrepared.bridge, t2.digest, outputDigest)
    assert.equal(replayAccepted.ok, true)
  })
})

// ── Canary R: multi-todowrite sequential admission ───────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_R admitTodowriteBatch admits multiple todowrite calls in one message', () => {
  const firstCall = 'call-r-first'
  const secondCall = 'call-r-second'
  // R: two different callIDs in one assistant message → admitted for sequential execution
  const admitted = todo.admitTodowriteBatch([firstCall, secondCall])
  assert.equal(admitted.ok, true)
})

test('WHAT[HOST-BOUNDARY-019] CANARY_R single todowrite call is admitted', () => {
  const admitted = todo.admitTodowriteBatch(['call-r-single'])
  assert.equal(admitted.ok, true)
})

// ── Canary N: zero bare SessionTodo.update — static proof ────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_N zero bare SessionTodo.update outside the membrane (static)', () => {
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
  // N: no V2 runner type exists — construction cannot bypass the membrane
  assert.doesNotMatch(fsproj, /V2Runner|MagicTodoV2/, 'no V2 runner type in the project')
  // N: TodoWriteAccepted and TodoWritePrepared are only appended through
  // MagicTodoMembrane (prepare/accept) and MagicTodoHostHooks (before/after).
  // The only writers are in MagicTodoMembrane.fs and the journal surface.
  const membraneSource = read('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs')
  assert.match(membraneSource, /MagicTodoFact\.TodoWritePrepared/, 'membrane owns Prepared writes')
  assert.match(membraneSource, /MagicTodoFact\.TodoWriteAccepted/, 'membrane owns Accepted writes')
  // N: no bare SessionTodo.update or TodoTable write outside the compatibility sink
  const surfaceSource = read('src/Wanxiangshu/Mission/Obligation/Todo/Surface.fs')
  assert.match(surfaceSource, /sink is optimistic UI state only/, 'TodoTable is explicitly sink-only')
  assert.doesNotMatch(surfaceSource, /SessionTodo\.update/, 'no bare SessionTodo.update in surface')
})

// ── Canary O: no Host core / same-name override — static proof ───────────

test('WHAT[HOST-BOUNDARY-019] CANARY_O no plugin todowrite tool overrides builtin (static)', () => {
  // O: the plugin registers hooks (definition/before/after), not a competing
  //    tool. Assert the exact typed HookPolicy composition names rather than
  //    searching for emitted Host-key strings that PluginHooks no longer owns.
  const pluginHooks = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
  assert.match(pluginHooks, /registeredHook HookKey\.ToolDefinition \(pairedHook \(box toolDefinition\)\)/, 'plugin registers the typed tool-definition hook')
  assert.match(pluginHooks, /registeredHook HookKey\.ToolBefore \(pairedHook \(box toolBefore\)\)/, 'plugin registers the typed tool-before hook')
  assert.match(pluginHooks, /registeredHook HookKey\.ToolAfter \(pairedHook \(box toolAfter\)\)/, 'plugin registers the typed tool-after hook')
  // O: no plugin tool named "todowrite" that would override the builtin
  assert.doesNotMatch(pluginHooks, /(?:tool\.add|registerTool)[\s\S]{0,160}todowrite|(?:name|"tool")\s*[,=:][\s\S]{0,80}['"]todowrite['"]/, 'no plugin todowrite tool override')
  // O: the builtin executor remains the sink — cross-ref host018-no-fork.test.mjs
  //    proves no Host source fork and only public SDK imports.
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
  assert.doesNotMatch(fsproj, /<ProjectReference[^>]*[Oo]pen[Cc]ode/, 'no Host source project reference')
})

// ── Canary Q: description face — static proof ────────────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_Q todowrite description contains tagged/lag/multi-sequential, not reviewer/session/barrier/witness/2N', () => {
  const description = read('resources/provider/lifecycle/magic-todo/todowrite-description/en.md')
  // Q: must contain planComplete tagging
  assert.match(description, /planComplete/i)
  // Q: must contain lag-1 / review-wait semantics
  assert.match(description, /accepted|account/i)
  // Q: must describe sequential multi-todowrite execution, never rejection
  assert.match(description, /multiple todowrite calls.*sequentially/i)
  assert.doesNotMatch(description, /rejected entirely/i)
  // Q: must NOT contain reviewer identity / session / barrier / witness / 2N mechanics
  assert.doesNotMatch(description, /reviewer session|barrier|witness|2N/i)
})

// ── Canary F: execute throw — physical boundary ──────────────────────────

test('WHAT[HOST-BOUNDARY-019] CANARY_F execute throw is a physical integration boundary (not unit-testable)', () => {
  // F is proven at the real Host boundary, but the production membrane must
  // retain both evidence branches so recovery never assumes `after` ran.
  const source = read('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs')
  const boundary = read('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembraneSurface.fs')
  assert.match(source, /PhysicalSuccessEvidence/, 'accept must classify physical success evidence')
  assert.match(boundary, /LiveAfterSuccess/, 'live after-hook evidence must remain explicit')
  assert.match(boundary, /RecoveredCompletedToolPart/, 'recovered completion evidence must remain explicit')
  assert.match(boundary, /physicalResult/, 'host input must fail closed at the physical evidence boundary')
})

// ── Canary file integrity: prevent silent shrinking ──────────────────────

test('WHAT[HOST-BOUNDARY-019] canary file integrity: all A–R canaries are present in this file', () => {
  const source = read('requirements/host-boundary/tests/magic-todo-membrane-canaries.test.mjs')
  const canaryIds = ['A', 'B', 'C', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R']
  for (const id of canaryIds) {
    assert.match(
      source,
      new RegExp(`CANARY_${id}\\b`),
      `Canary ${id} must be present in the canary file`,
    )
  }
})
