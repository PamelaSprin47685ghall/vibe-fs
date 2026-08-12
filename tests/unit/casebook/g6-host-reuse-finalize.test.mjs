// G6-G Host path: Meditator → same reusable Inspector → Q1/Q2/Q3 → ReuseScope
// close → exactly one CaseFinalize → cold fetch. Real SyncDelegateRuntime +
// CasebookLifecycle (not helper-only notePrompt, not Long Stroke LLM).

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
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
  SyncDelegateRuntime__Dispose as disposeRuntime,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  collector,
  setEnabled,
  notePrompt,
  noteAnswer,
  tryFinalizeInspector,
  cleanupInspector,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  listItems,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
} from '../support/domain.mjs'
import {
  CANONICAL_A,
  CANONICAL_Q,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'


const SYNC_RETURN_COMPLETION = 'Sync delegate answer returned to caller.'

const QUESTIONS = [
  ['Who owns PromptAuthority?', 'Host owns PromptAuthority.'],
  ['Where do Case facts live?', 'Unified EventStore only.'],
  ['When does CaseFinalize run?', 'ReuseScope close, once.'],
]

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const completionTurn = (delegateKey, role) =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_turn'),
    authorityRoot('msg_root_turn'),
    providerRun('asst_turn'),
    role,
    undefined,
    [reconcileSupervisor.textPart(SYNC_RETURN_COMPLETION)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_turn') => {
  const handled = await handleTurn(
    runtime,
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
    ),
    undefined,
  )
  assert.equal(handled, true)
}

const withHarness = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-g6-host-reuse-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')

  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  let physicalSeq = 0

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
      const child = sessionId(`g6-reuse-insp-${createCalls.length + 1}`)
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
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier('Fast'),
    (_delegateSession, _agent) => {},
    createQuiescenceGate(),
    undefined,
  )

  setEnabled(dir)
  try {
    await fn({
      dir,
      runtime,
      createCalls,
      prompts,
    })
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

test('G6_G_host_reusable_inspector_one_finalize_then_cold_fetch', async () => {
  await withHarness(async ({ dir, runtime, createCalls, prompts }) => {
    const owner = 'ses_meditator_g6'
    const inspectorRole = roles.of('Inspector')
    let delegateId

    for (let i = 0; i < QUESTIONS.length; i += 1) {
      const [q, a] = QUESTIONS[i]
      const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, q)
      await waitFor(
        () => prompts.length === i + 1 && createCalls.length === 1,
        `Inspector Q${i + 1} did not reuse a single child`,
      )
      if (i === 0) {
        delegateId = createCalls[0].child
        assert.equal(createCalls[0].agent, 'fast-inspector')
      } else {
        assert.equal(createCalls[0].child, delegateId, 'GetOrCreate must reuse Inspector session')
        assert.equal(prompts[i].session, delegateId)
      }

      notePrompt(delegateId, q)
      await settlePendingInvoke(runtime, delegateId, inspectorRole, a, `asst_q${i + 1}`)
      const done = resultOf(await pending)
      assert.equal(done.ok, true, done.ok ? '' : done.error)
      assert.equal(done.value, a)
      noteAnswer(delegateId, a)
    }

    assert.equal(createCalls.length, 1, 'createChild once for reusable Inspector')
    assert.equal(prompts.length, 3)
    assert.equal(prompts.every((p) => p.session === delegateId), true)

    collect(collector, delegateId, 'read', { path: 'a.txt' }, 'hello')

    // ReuseScope close: exactly one CaseFinalize child (separate Bookkeeper port).
    const bookkeeper = scriptedBookkeeperPort()
    setSessionPort(bookkeeper.port)
    const first = resultOf(await tryFinalizeInspector(dir, delegateId))
    assert.equal(first.ok, true, `exactly one finalize ok: ${JSON.stringify(first.error)}`)
    assert.equal(bookkeeper.createCalls.length, 1, 'exactly one Bookkeeper CreateChildSession')
    assert.equal(bookkeeper.programCalls.length >= 1, true, 'js-bookkeeper invoked')

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const published = resultOf(fetchCase(store, raw, 10, delegateId))
    assert.equal(published.ok, true)
    assert.equal(published.value !== undefined && published.value !== null, true, 'Case exists after ReuseScope close')
    assert.equal(published.value.SessionId, delegateId)
    assert.equal(published.value.Q, CANONICAL_Q)
    assert.equal(published.value.A, CANONICAL_A)
    assert.equal(published.value.A.includes('evidence:'), false)
    assert.equal(listItems(published.value.Observations).length, 1)

    cleanupInspector(delegateId)

    const [rawCold, storeCold] = acquire(gitCommonDir(dir))
    const cold = resultOf(fetchCase(storeCold, rawCold, 10, delegateId))
    assert.equal(cold.ok, true)
    assert.equal(cold.value !== undefined && cold.value !== null, true, 'cleanup must not delete published Case (cold reuse)')
    assert.equal(cold.value.SessionId, delegateId)

    notePrompt(delegateId, 'second finalize must not publish')
    noteAnswer(delegateId, 'should be refused')
    const second = resultOf(await tryFinalizeInspector(dir, delegateId))
    assert.equal(second.ok, false, 'finalize twice is refused')
    assert.equal(String(second.error).includes('already finalized'), true)

    const still = resultOf(fetchCase(store, raw, 10, delegateId))
    assert.equal(still.value.SessionId, delegateId, 'original Case retained after refused second finalize')
    assert.equal(createCalls.length, 1, 'createChild stays once after scope close')
  })
})
