// Split from tests/unit/session/sync-delegate-runtime.test.mjs (cutover Wave 2a); owner: delegation.
//
// DELEG-007..012：SyncDelegate batch 合并/overlap fail-closed/tier 确定性映射/
// G2 serial reuse/EXEC-031 bounded WorkRecord（无 return 通道、canonical 得
// WorkRecord、siblings 引用）。reuse/retire/cancel scope 断言已随 SPLIT@cutover 迁
// requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { ToolCallIdModule_create as toolCallId } from '../../../dist/Kernel/Identity.js'
import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Session/SessionPersona.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { SyncDelegateBatch, SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__Invoke_FCBDD42 as invoke,
  SyncDelegateRuntime__InvokeBatchPrepared_Z2E60ED39 as invokeBatchPrepared,
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
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
  toList,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'

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

const captureLastAssistant = async (delegateKey, answer) => {
  const key = String(delegateKey)
  const messages = transcripts.get(key) ?? []
  messages.push({ role: 'assistant', parts: [xTraceCapture.text(answer)] })
  transcripts.set(key, messages)
  await xTraceCapture.captureProjection(
    activeJournal,
    sessionId(delegateKey),
    xTraceCapture.semantic({ messages }),
  )
}

const withHarness = async (fn, { tier = 'Fast', project = true } = {}) => {
  SessionPersona_clearAllForTests()
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-'))
  const opened = await agentJournal.create({ directory: base })
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
  await captureLastAssistant(delegateKey, answer)
  const handled = await handleTurn(runtime, completionTurn(delegateKey, role, answer, runId), undefined)
  assert.equal(handled, true)
}

test('EXEC_026_sync_delegate_provider_batch_coalesces_without_race_and_returns_once', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_batch'
    const run = providerRun('asst_batch')
    const firstCall = toolCallId('call_first')
    const secondCall = toolCallId('call_second')
    const callOrder = toList([firstCall, secondCall])

    // Arrival order is deliberately reversed. Provider order, not scheduler
    // timing, defines prompt concatenation and canonical result ownership.
    const second = invokeBatchPrepared(
      runtime,
      owner,
      SyncDelegateRole.Inspector,
      'inspect second',
      new SyncDelegateBatch(run, callOrder, secondCall),
      async () => 'prepared second',
    )
    const first = invokeBatchPrepared(
      runtime,
      owner,
      SyncDelegateRole.Inspector,
      'inspect first',
      new SyncDelegateBatch(run, callOrder, firstCall),
      async () => 'prepared first',
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'semantic batch did not send')
    assert.equal(createCalls.length, 1)
    assert.equal(prompts.length, 1)
    assert.equal(prompts[0].text, 'prepared first\n\nprepared second')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'combined answer')
    const firstDone = resultOf(await first)
    const secondDone = resultOf(await second)

    assert.equal(firstDone.ok, true, firstDone.error)
    assert.equal(firstDone.value.tag, 0, 'provider-first call owns the WorkRecord')
    assert.match(firstDone.value.fields[0], /combined answer/)
    assert.match(firstDone.value.fields[0], /Recent work/)
    assert.doesNotMatch(firstDone.value.fields[0], /Closing report/)

    assert.equal(secondDone.ok, true, secondDone.error)
    assert.equal(secondDone.value.tag, 1, 'sibling must reference the canonical result')
    assert.equal(idValue.toolCall(secondDone.value.fields[0]), 'call_first')
  })
})

test('EXEC_026_sync_delegate_different_run_overlap_is_rejected_not_queued', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_inflight'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'first in flight')

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'first Invoke did not send')

    const secondDone = resultOf(await invoke(runtime, owner, SyncDelegateRole.Inspector, 'second arrival'))
    assert.equal(secondDone.ok, false)
    assert.match(secondDone.error, /active batch/)
    assert.equal(prompts.length, 1, 'overlap must not enqueue or dispatch a second prompt')
    assert.equal(createCalls.length, 1)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'first answer', 'asst_turn1')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true, firstDone.error)
    assert.match(firstDone.value, /first answer/)
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
// requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs
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
    await captureLastAssistant(delegateId, 'inspector working body')

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
