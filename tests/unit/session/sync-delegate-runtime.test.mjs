// tests/unit/session/sync-delegate-runtime.test.mjs — EXEC-026 / EXEC-028
//
// SyncDelegateRuntime without full Host: real runtime + fake ISessionHostPort /
// AttachedSessionRuntime / PromptDispatcher journal, mirroring satellite-runtime
// and prompt fire-and-forget unit patterns.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { SessionPersona_clearAllForTests } from '../../../dist/Domain/PersonaCatalog.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__Invoke_1B1DD6DD as invoke,
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__CancelSession_Z31B28506 as cancelSession,
  SyncDelegateRuntime__StageDeletedInspector_59B1A0C0 as stageDeletedInspector,
  SyncDelegateRuntime__TryFind_636E3F87 as tryFind,
  SyncDelegateRuntime__TryFindForScopeClose_636E3F87 as tryFindForScopeClose,
  SyncDelegateRuntime__Dispose as disposeRuntime,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  lifecycleWorkRecordProjection,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
  xTraceCapture,
} from '../support/domain.mjs'

const completionTurn = (delegateKey, role, answer, runId = 'asst_turn') =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_turn'),
    authorityRoot('msg_root_turn'),
    providerRun(runId),
    role,
    undefined,
    [reconcileSupervisor.textPart(answer)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

let activeJournal
const transcripts = new Map()

const captureLastAssistant = (delegateKey, answer) => {
  const key = String(delegateKey)
  const messages = transcripts.get(key) ?? []
  messages.push({ role: 'assistant', parts: [xTraceCapture.text(answer)] })
  transcripts.set(key, messages)
  xTraceCapture.captureProjection(
    activeJournal,
    sessionId(delegateKey),
    xTraceCapture.semantic({ messages }),
  )
}

const withHarness = async (fn, { tier = 'Fast', project = true } = {}) => {
  SessionPersona_clearAllForTests()
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-'))
  const opened = agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  activeJournal = opened.journal
  transcripts.clear()

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  let physicalSeq = 0
  const ownerTier = { current: tier }

  const sessions = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async (session, text, options) => {
      physicalSeq += 1
      prompts.push({
        session: idValue.session(session),
        text,
        agent: options?.Agent,
      })
      return promptDispatcher.admittedWithPhysicalMessage(`msg_phys_${physicalSeq}`)
    },
    CreateChildSession: async (parentId, options) => {
      const child = sessionId(`delegate-${createCalls.length + 1}`)
      createCalls.push({
        parent: idValue.session(parentId),
        agent: options?.Agent,
        title: options?.Title,
        child: idValue.session(child),
      })
      return okResult(child)
    },
  }

  const attached = createAttached()
  const ready = []
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(ownerTier.current),
    (delegateSession, agent) => {
      ready.push({ session: idValue.session(delegateSession), agent })
    },
    createQuiescenceGate(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    // EXEC-031: per-invocation bounded WorkRecord via the real journal
    // projector. Session-mechanics tests must assert the answer flows through
    // Recent work as last assistant text — never re-encode partsText.
    project
      ? (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range)
      : undefined,
  )

  try {
    await fn({
      runtime,
      createCalls,
      prompts,
      ready,
      sessions,
      journal: opened.journal,
      ownerTier,
    })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_return') => {
  captureLastAssistant(delegateKey, answer)
  const handled = await handleTurn(runtime, completionTurn(delegateKey, role, answer, runId), undefined)
  assert.equal(handled, true)
}

test('EXEC_026_sync_delegate_concurrent_invokes_are_coalesced_into_single_prompt', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_flight'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect first')
    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect second')

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'coalesced Invoke did not send')
    assert.equal(createCalls.length, 1)
    assert.equal(prompts.length, 1)
    assert.equal(prompts[0].text, 'inspect first\n\ninspect second')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'combined answer')
    const firstDone = resultOf(await first)
    const secondDone = resultOf(await second)
    assert.equal(firstDone.ok, true, firstDone.error)
    assert.match(firstDone.value, /combined answer/)
    assert.match(firstDone.value, /Recent work/)
    assert.doesNotMatch(firstDone.value, /Closing report/)
    assert.equal(secondDone.ok, true, secondDone.error)
    assert.match(secondDone.value, /combined answer/)
  })
})

test('EXEC_026_sync_delegate_in_flight_invoke_prompts_directly_and_queues_on_session', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_inflight'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'first in flight')

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'first Invoke did not send')

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'second arrival')
    await waitFor(() => prompts.length === 2, 'second Invoke did not send prompt directly')

    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse child session')
    assert.equal(prompts[0].text, 'first in flight')
    assert.equal(prompts[1].text, 'second arrival')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'first answer', 'asst_turn1')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true, firstDone.error)
    assert.match(firstDone.value, /first answer/)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'second answer', 'asst_turn2')
    const secondDone = resultOf(await second)
    assert.equal(secondDone.ok, true, secondDone.error)
    assert.match(secondDone.value, /second answer/)
  })
})

test('EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_tier'
    const inspectorP = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect please')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'inspector child not created')
    assert.equal(createCalls[0].agent, 'fast-inspector')
    assert.equal(createCalls[0].title, 'fast-inspector')
    assert.equal(prompts[0].agent, 'fast-inspector')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'inspector done', 'asst_insp')
    assert.equal((resultOf(await inspectorP)).ok, true)

    const coderP = invoke(runtime, owner, SyncDelegateRole.Coder, 'code please')
    await waitFor(() => createCalls.length === 2 && prompts.length === 2, 'coder child not created')
    assert.equal(createCalls[1].agent, 'fast-coder')
    assert.equal(createCalls[1].title, 'fast-coder')
    assert.equal(prompts[1].agent, 'fast-coder')

    await settlePendingInvoke(runtime, createCalls[1].child, roles.of('Coder'), 'coder done', 'asst_coder')
    assert.equal((resultOf(await coderP)).ok, true)
  })
})

test('EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_reuse'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass one')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'first create/send missing')
    const delegateId = createCalls[0].child

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer one', 'asst_one')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true, firstDone.error)
    assert.match(firstDone.value, /answer one/)

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass two')
    await waitFor(() => prompts.length === 2, 'second send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; createChild once')

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer two', 'asst_two')
    const secondDone = resultOf(await second)
    assert.equal(secondDone.ok, true, secondDone.error)
    assert.match(secondDone.value, /answer two/)
    assert.equal(createCalls[0].child, delegateId)
  })
})

test('EXEC_026_sync_delegate_reuse_keeps_deep_inspector_when_owner_later_fast', async () => {
  await withHarness(async ({ runtime, prompts, createCalls, ownerTier }) => {
    const owner = 'ses_owner_keep_deep'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect deep')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'deep inspector not created')
    assert.equal(createCalls[0].agent, 'deep-inspector')
    assert.equal(prompts[0].agent, 'deep-inspector')

    const delegateId = createCalls[0].child
    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer one', 'asst_deep1')
    assert.equal((resultOf(await first)).ok, true)

    ownerTier.current = 'Fast'
    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect again')
    await waitFor(() => prompts.length === 2, 'reuse send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; createChild once')
    assert.equal(prompts[1].agent, 'deep-inspector')
    assert.notEqual(prompts[1].agent, 'fast-inspector')

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer two', 'asst_deep2')
    assert.equal((resultOf(await second)).ok, true)
  }, { tier: 'Deep' })
})

// ProviderWire(Q1) prefix-of Q2 prefix-of Q3 (ARCH-004) is proved in
// tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs
// (`G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`). This harness's
// fake SendPrompt records text/agent only and does not prove ProviderWire.
test('G2_inspector_Q1_Q2_Q3_same_session_serial_reuse', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_g2'
    const inspector = roles.of('Inspector')
    const answers = ['answer Q1', 'answer Q2', 'answer Q3']

    const q1 = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q1')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'Q1 create/send missing')
    const delegateId = createCalls[0].child
    assert.equal(createCalls[0].agent, 'fast-inspector')
    assert.equal(createCalls[0].title, 'fast-inspector')
    assert.equal(prompts[0].agent, 'fast-inspector')
    assert.equal(prompts[0].session, delegateId)
    assert.equal(prompts[0].text, 'Q1')

    await settlePendingInvoke(runtime, delegateId, inspector, answers[0], 'asst_q1')
    const q1Done = resultOf(await q1)
    assert.equal(q1Done.ok, true, q1Done.error)
    assert.match(q1Done.value, /answer Q1/)
    assert.match(q1Done.value, /Recent work/)
    assert.doesNotMatch(q1Done.value, /Closing report/)

    const q2 = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q2')
    await waitFor(() => prompts.length === 2, 'Q2 send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; CreateChildSession once')
    assert.equal(createCalls[0].child, delegateId)
    assert.equal(prompts[1].agent, 'fast-inspector')
    assert.equal(prompts[1].session, delegateId)
    assert.equal(prompts[1].text, 'Q2')

    await settlePendingInvoke(runtime, delegateId, inspector, answers[1], 'asst_q2')
    const q2Done = resultOf(await q2)
    assert.equal(q2Done.ok, true, q2Done.error)
    assert.match(q2Done.value, /answer Q2/)
    // EXEC-031 / COMPANION-015: a reused child's second record must be bounded
    // to its own invocation — it must not leak the first invocation's body.
    assert.doesNotMatch(q2Done.value, /answer Q1/)

    const q3 = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q3')
    await waitFor(() => prompts.length === 3, 'Q3 send missing')
    assert.equal(createCalls.length, 1, 'CreateChildSession must stay exactly once through Q3')
    assert.equal(createCalls[0].child, delegateId)
    assert.equal(prompts[2].agent, 'fast-inspector')
    assert.equal(prompts[2].session, delegateId)
    assert.equal(prompts[2].text, 'Q3')

    await settlePendingInvoke(runtime, delegateId, inspector, answers[2], 'asst_q3')
    const q3Done = resultOf(await q3)
    assert.equal(q3Done.ok, true, q3Done.error)
    assert.match(q3Done.value, /answer Q3/)
    assert.doesNotMatch(q3Done.value, /answer Q1/)
    assert.doesNotMatch(q3Done.value, /answer Q2/)

    assert.deepEqual(
      prompts.map((p) => p.agent),
      ['fast-inspector', 'fast-inspector', 'fast-inspector'],
    )
    assert.deepEqual(
      [q1Done.value, q2Done.value, q3Done.value].map((value) => value.match(/answer Q[123]/)?.[0]),
      answers,
    )
    assert.equal(new Set(answers).size, 3)
    assert.equal(createCalls.length, 1)
  })
})

test('G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_g6_cascade'
    const inspectorRole = roles.of('Inspector')
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q1 before owner cascade')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Q1 did not send')
    const deletedChild = createCalls[0].child

    await settlePendingInvoke(runtime, deletedChild, inspectorRole, 'A1', 'asst_g6_q1')
    assert.equal(resultOf(await first).ok, true)
    assert.equal(idValue.session(tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector)), deletedChild)

    assert.equal(
      stageDeletedInspector(runtime, sessionId(owner), sessionId(deletedChild)),
      true,
      'child SessionDeleted must stage the attached Inspector for owner scope close',
    )
    assert.equal(tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector), undefined, 'dead child is not reusable')
    assert.equal(
      idValue.session(tryFindForScopeClose(runtime, sessionId(owner), SyncDelegateRole.Inspector)),
      deletedChild,
      'owner SessionDeleted can still resolve the retired Inspector for CaseFinalize',
    )

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q2 after unexpected child delete')
    await waitFor(() => prompts.length === 2 && createCalls.length === 2, 'continued owner did not replace dead Inspector')
    assert.notEqual(createCalls[1].child, deletedChild)
    assert.equal(
      tryFindForScopeClose(runtime, sessionId(owner), SyncDelegateRole.Inspector)?.fields[0],
      createCalls[1].child,
      'continued owner must discard the staged child and use the replacement binding',
    )

    cancelSession(runtime, sessionId(owner))
    assert.equal(resultOf(await second).ok, false)
  })
})

test('G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_g2_cancel'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect then cancel')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    assert.equal(createCalls.length, 1)

    let invokeSettled = false
    let invokeResult
    pending.then((value) => {
      invokeSettled = true
      invokeResult = value
    })

    cancelSession(runtime, sessionId(owner))
    await waitFor(() => invokeSettled, 'Invoke did not fail after owner CancelSession')

    const done = resultOf(invokeResult)
    assert.equal(done.ok, false)
    assert.match(done.error, /cancelled/i)
    assert.equal(createCalls.length, 1, 'cancel must not CreateChildSession again')
  })
})

// EXEC-031: a Completed turn without a bounded WorkRecord fails closed — the
// last-message fallback must not count as success (residual OneShot analog).
test('EXEC_031_completed_without_bounded_work_record_fails_closed', async () => {
  await withHarness(async ({ runtime, createCalls, prompts }) => {
    const owner = 'ses_owner_failclosed'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect without projector')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'some answer', 'asst_fc')

    const done = resultOf(await pending)
    assert.equal(done.ok, false, 'last-message fallback must not count as success')
    assert.match(done.error, /EXEC-031|WorkRecord/)
  }, { project: false })
})

test('EXEC_031_bounded_work_record_answers_in_recent_work_not_raw_message', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, journal }) => {
    const owner = 'ses_owner_bounded'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect module')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    const delegateId = createCalls[0].child

    // Append this invocation's trace parts to the delegate session (Recent work).
    // captureProjection syncs a full semantic transcript; isolated single-message
    // calls collide on g:0/turn:0/part:0 and drop later parts.
    captureLastAssistant(delegateId, 'inspector working body')

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'formal inspector answer', 'asst_bounded')
    const done = resultOf(await pending)
    assert.equal(done.ok, true, done.error)

    assert.match(done.value, /Recent work/)
    assert.match(done.value, /inspector working body/)
    assert.match(done.value, /formal inspector answer/)
    assert.doesNotMatch(done.value, /Closing report/)
    assert.doesNotMatch(done.value, /^Opening\n/m, 'includeOpening=false must not echo the charge')
    assert.doesNotMatch(done.value, /# # /)

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect again')
    await waitFor(() => prompts.length === 2, 'second inspect did not send')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse the same child')
    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'second inspect last part', 'asst_bounded2')
    const secondDone = resultOf(await second)
    assert.equal(secondDone.ok, true, secondDone.error)
    assert.match(secondDone.value, /Recent work/)
    assert.match(secondDone.value, /second inspect last part/)
    assert.doesNotMatch(secondDone.value, /formal inspector answer/, 'reuse must not leak the previous invocation last part')
    assert.doesNotMatch(secondDone.value, /inspector working body/, 'reuse must not leak the previous invocation body')
    assert.doesNotMatch(secondDone.value, /Closing report/)
    assert.doesNotMatch(secondDone.value, /^Opening\n/m)
  })
})
