import test from 'node:test'
import assert from 'node:assert/strict'
import * as cognitive from '../../../dist/Participant/Cognition/RuntimeSurface.js'
import * as cognitiveFold from '../../../dist/Participant/Cognition/FoldSurface.js'

const deferred = () => {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}

function cognitiveJournalPort({ writeGate, blobFailure, appendFailure } = {}) {
  const trace = []
  const blobs = new Map()
  const projections = new Map()
  const writeStarted = deferred()
  const port = {
    writeBlob: async content => {
      trace.push('blob-requested')
      writeStarted.resolve()
      if (writeGate) await writeGate
      if (blobFailure) return { ok: false, error: blobFailure }
      const snapshotRef = `snapshot-${blobs.size + 1}`
      blobs.set(snapshotRef, content)
      trace.push('blob-durable')
      return { ok: true, snapshotRef, snapshotDigest: `digest-${snapshotRef}` }
    },
    appendCommit: async commit => {
      trace.push('commit-requested')
      assert.ok(blobs.has(commit.snapshotRef), 'commit references a completed blob write')
      if (appendFailure) return { ok: false, error: appendFailure }
      const folded = cognitiveFold.CognitiveFactFold_fold(projections.get(commit.ownerKey) ?? null, {
        case: 'AssumePhaseCommitted', fields: commit,
      })
      assert.equal(folded.ok, true, folded.error)
      projections.set(commit.ownerKey, folded.value[0].projection)
      trace.push('commit-accepted')
      return { ok: true }
    },
    readProjection: ownerKey => projections.get(ownerKey) ?? null,
    readBlob: async snapshotRef => blobs.has(snapshotRef)
      ? { ok: true, value: blobs.get(snapshotRef) }
      : { ok: false, error: 'missing snapshot' },
  }
  return { port, trace, blobs, projections, writeStarted: writeStarted.promise }
}

const cognitiveOwner = { sessionId: 'effect-008-assume' }
const assumeCall = {
  toolCallId: 'assume-call-1', inputDigest: 'input-1', canvasJson: '{"decision":"ship"}',
  todos: [{ content: 'validate result', status: 'pending', priority: 'high' }],
}

test('WHAT[effect-accounting-008] assume runtime waits for snapshot persistence before committing canvas and todos', async () => {
  const gate = deferred()
  const journalPort = cognitiveJournalPort({ writeGate: gate.promise })
  const runtime = cognitive.CognitiveRuntime_create(journalPort.port)
  const pending = cognitive.CognitiveRuntime_commit(runtime, cognitiveOwner, assumeCall)
  await journalPort.writeStarted
  try {
    assert.deepEqual(journalPort.trace, ['blob-requested'])
    assert.equal(journalPort.projections.size, 0)
    const before = await cognitive.CognitiveRuntime_currentCanvas(runtime, cognitiveOwner)
    assert.equal(before.ok, true)
    assert.equal(before.canvasJson, '{}')
  } finally {
    gate.resolve()
  }
  const result = await pending
  assert.equal(result.ok, true, result.error)
  assert.equal(result.state, 'committed')
  assert.deepEqual(journalPort.trace, ['blob-requested', 'blob-durable', 'commit-requested', 'commit-accepted'])
  const current = await cognitive.CognitiveRuntime_currentCanvas(runtime, cognitiveOwner)
  assert.equal(current.canvasJson, assumeCall.canvasJson)
  assert.deepEqual(current.todos, assumeCall.todos)
  const recovered = await cognitive.CognitiveRuntime_currentCanvas(
    cognitive.CognitiveRuntime_create(journalPort.port), cognitiveOwner,
  )
  assert.deepEqual(recovered, current)
  assert.equal((await cognitive.CognitiveRuntime_commit(runtime, cognitiveOwner, assumeCall)).state, 'replayed')
  assert.equal(journalPort.blobs.size, 1, 'exact replay does not persist another snapshot')
})

for (const [boundary, failure] of [
  ['snapshot', { blobFailure: 'snapshot write refused' }],
  ['commit', { appendFailure: 'commit append refused' }],
]) {
  test(`WHAT[effect-accounting-008] assume ${boundary} rejection leaves both canvas and todos uncommitted`, async () => {
    const journalPort = cognitiveJournalPort(failure)
    const runtime = cognitive.CognitiveRuntime_create(journalPort.port)
    const result = await cognitive.CognitiveRuntime_commit(runtime, cognitiveOwner, assumeCall)
    assert.equal(result.ok, false)
    assert.equal(result.error, failure.blobFailure ?? failure.appendFailure)
    assert.equal(journalPort.projections.size, 0)
    assert.deepEqual(journalPort.trace, boundary === 'snapshot'
      ? ['blob-requested']
      : ['blob-requested', 'blob-durable', 'commit-requested'])
    const current = await cognitive.CognitiveRuntime_currentCanvas(runtime, cognitiveOwner)
    assert.equal(current.ok, true)
    assert.equal(current.canvasJson, '{}')
    assert.deepEqual(current.todos, [])
  })
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const cycle = await import("../../../dist/Context/Companion/Blogger/Runtime/CycleSurface.js");

const materialize = (over = {}) => ({ kind: 'materialize', requestId: 'req-1', blogger: 'ses-blogger', digest: 'ctx-1', ...over })
const entry = (over = {}) => ({ kind: 'entry', requestId: 'req-1', run: 'msg-e1', ...over })
const squash = (over = {}) => ({ kind: 'squash', requestId: 'req-s1', run: 'msg-s1', ...over })
const state = (...actions) => cycle.scenario(actions)
const ok = (...actions) => {
  const result = state(...actions)
  assert.equal(result.ok, true, result.error ?? '')
  return result.state
}

test('WHAT[effect-accounting-008] C5_materialize_opens_request_queryable_by_blogger', () => {
  const result = cycle.scenario([materialize({ requestId: 'req-open' })])
  assert.equal(result.ok, true, result.error ?? '')
  assert.deepEqual(result.state, {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})
test('WHAT[effect-accounting-008] C5_entry_commit_records_receipt_and_clears_open_request', () => {
  assert.deepEqual(ok(materialize(), entry()), {
    openRequests: 0,
    openBloggers: 0,
    receipts: 1,
    requestBindings: 1,
  })
})
test('WHAT[effect-accounting-008] C5_same_provider_run_cannot_be_both_entry_and_squash', () => {
  const result = state(entry({ run: 'msg-same' }), squash({ run: 'msg-same' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /already has/i)
})
test('WHAT[effect-accounting-008] C5_materialize_prompt_key_fill_in_after_send', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-key' }), materialize({ requestId: 'req-key', promptKey: 'pk-blog-1' })), {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})
test('WHAT[effect-accounting-008] C5_materialize_prompt_key_cannot_rebind', () => {
  const result = state(
    materialize({ requestId: 'req-rebind', promptKey: 'pk-a' }),
    materialize({ requestId: 'req-rebind', promptKey: 'pk-b' }),
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /different PromptKey/i)
})
test('WHAT[effect-accounting-008] C5_duplicate_request_materialize_different_context_rejected', () => {
  const result = state(materialize({ requestId: 'req-dup', digest: 'ctx-1' }), materialize({ requestId: 'req-dup', digest: 'ctx-2' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /different context/i)
})
test('WHAT[effect-accounting-008] C5_abandon_clears_open_request', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-ab' }), { kind: 'abandon', requestId: 'req-ab', blogger: 'ses-blogger' }), {
    openRequests: 0,
    openBloggers: 0,
    receipts: 0,
    requestBindings: 0,
  })
})
test('WHAT[effect-accounting-008] C5_request_id_cannot_rebind_to_different_provider_run', () => {
  const result = state(entry({ requestId: 'req-bind', run: 'msg-a' }), entry({ requestId: 'req-bind', run: 'msg-b' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /RequestId.*rebind/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const pluginHooks = await import("../../../dist/OpenCode/Host/PluginHooksSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const recovery = await import("../../../dist/Interaction/Dispatch/RecoverySurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')
const ownerRootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const acceptOwnerLogicalRun = async (handle, ownerSession) => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    ownerSession,
    `msg-root-${ownerSession}`,
    ownerRootSelection,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  assert.ok(
    dispatch.projectionObservation(handle, ownerSession).activeLogicalRun,
    'the owner must hold an active durable Logical Run before its Companion leaf is dispatched',
  )
}
const withJournal = async (prefix, writer, runtime, body) => {
  const directory = mkdtempSync(join(tmpdir(), prefix))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, writer, runtime, 4242, '2026-08-30T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    return await body(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[effect-accounting-008] Adapter Blogger Coordinator submits one receipt and recovers exact physical acceptance without resend', async () => {
  await withJournal('wxs-blogger-coordinator-', 'writer-blogger-coordinator', 'rt-blogger-coordinator', async (handle) => {
    const mainSession = 'ses-main-effect-008'
    const bloggerSession = 'ses-blogger-effect-008'
    const physicalUserMessageId = 'msg-blogger-effect-008'
    const submissions = []
    let childListCalls = 0
    const childCreates = []
    const host = {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async (session, text, options) => {
        submissions.push({ session, text, options })
        return dispatch.admittedWithReceipt('accepted-host-blogger-008')
      },
      AbortSession: async () => ({ ok: true }),
      InterruptAttempt: async () => ({ ok: true }),
      IsManagedChild: () => true,
      AbortChildren: async () => {},
      CreateSiblingSession: async () => ({ ok: false, error: 'unexpected sibling creation' }),
      TryGetParentSession: async () => ({ ok: true, value: undefined }),
      CreateChildSession: async (parent, options) => {
        childCreates.push({ parent, options })
        return { ok: true, value: bloggerSession }
      },
      ListChildren: async () => {
        childListCalls += 1
        return dispatch.acceptedChild(bloggerSession, 'blogger', 'blogger')
      },
      FamilyRootOf: () => mainSession,
    }

    await acceptOwnerLogicalRun(handle, mainSession)

    const decision = await pluginHooks.coordinateBloggerUnresolvedTwice(
      host,
      handle,
      mainSession,
      bloggerSession,
      'request-blogger-effect-008',
    )
    assert.equal(pluginHooks.firstBloggerEffect(decision), 'StartedSquash')
    assert.equal(pluginHooks.secondBloggerEffect(decision), 'SkippedInFlight')
    assert.equal(childListCalls, 2, 'SatelliteRuntime must inspect root and owner child listings')
    assert.equal(childCreates.length, 0, 'the exact restored Blogger child must be reused')
    assert.equal(submissions.length, 1)
    assert.equal(submissions[0].session, bloggerSession)

    const submitted = dispatch.projectionObservation(handle, bloggerSession)
    assert.equal(submitted.pendingClaims.length, 1)
    const claim = submitted.pendingClaims[0]
    assert.equal(claim.promptKey, submissions[0].options.Metadata.wanxiangshu_prompt_key)
    assert.equal(claim.receipt, `accepted-detached-${claim.promptKey}`)

    const unresolved = await recovery.reconcile(handle, [])
    assert.deepEqual(unresolved.map(({ promptKey, outcome }) => ({ promptKey, outcome })), [
      { promptKey: claim.promptKey, outcome: 'StillPending' },
    ])
    assert.equal(submissions.length, 1)

    const proven = await recovery.reconcile(handle, [{
      id: physicalUserMessageId,
      role: 'user',
      metadata: { wanxiangshu_prompt_key: claim.promptKey },
    }])
    assert.deepEqual(proven.map(({ promptKey, outcome, physicalMessageId }) => ({ promptKey, outcome, physicalMessageId })), [
      { promptKey: claim.promptKey, outcome: 'Proven', physicalMessageId: physicalUserMessageId },
    ])
    assert.equal(dispatch.projectionObservation(handle, bloggerSession).pendingClaims.length, 0)
    assert.equal(submissions.length, 1)
  })
})
}
