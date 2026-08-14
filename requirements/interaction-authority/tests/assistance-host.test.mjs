// Split from tests/unit/host/assistance-host.test.mjs (cutover Wave 2a);
// owner: interaction-authority. AGENT-031 authority 语义半边：
// fast NEEDHELP 是同 session 的 deep-peer continuation（不动 fallback cursor、
// AcceptedContinuationIds 含 NeedHelpEscalation 不含 ProviderRetryAttempt，
// INTERACTION-AUTHORITY-012 R10）；snapshot agent 绑定把 fast 升级转为 deep
// consultation（同 Session 续推，INTERACTION-AUTHORITY-013 R11）。
// consultation child 委托/advice 路由断言归 delegation。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  authorityRoot,
  fallbackProjection,
  fold,
  idValue,
  okResult,
  physicalUser,
  promptDispatcher,
  providerLanguage,
  providerRun,
  reconcileSupervisor,
  roles,
  sessionId,
  toList,
} from '../../verification-system/tests/support/domain.mjs'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import {
  ReconciledTurn,
  ReconciledTurnContext,
  ReconciledTurnDelivery,
} from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { forJournal, Runtime__AcceptHumanRoot } from '../../../dist/Application/Prompting/PromptDispatcher.js'
import { captureOpening } from '../../../dist/Application/Reconciliation/XTraceCapture.js'

import * as NeedHelpSensorModule from '../../../dist/Infrastructure/OpenCode/Host/NeedHelpSensor.js'
import * as AssistanceHostModule from '../../../dist/Infrastructure/OpenCode/Host/AssistanceHost.js'
import * as QuiescenceModule from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'

const sensorMethod = (name) => {
  const prefix = `NeedHelpSensor__${name}`
  const key = Object.keys(NeedHelpSensorModule).find((entry) => entry === prefix || entry.startsWith(`${prefix}_`))
  if (!key) throw new Error(`NeedHelpSensor method ${name} not found`)
  return NeedHelpSensorModule[key]
}
const assistanceMethod = (name) => {
  const prefix = `AssistanceHost__${name}`
  const key = Object.keys(AssistanceHostModule).find((entry) => entry === prefix || entry.startsWith(`${prefix}_`))
  if (!key) throw new Error(`AssistanceHost method ${name} not found`)
  return AssistanceHostModule[key]
}
const quiescenceMethod = (name) => {
  const prefix = `SessionQuiescenceGate__${name}`
  const key = Object.keys(QuiescenceModule).find((entry) => entry === prefix || entry.startsWith(`${prefix}_`))
  if (!key) throw new Error(`SessionQuiescenceGate method ${name} not found`)
  return QuiescenceModule[key]
}

const arm = sensorMethod('TryArm')
const rawHandleTurn = assistanceMethod('HandleTurn')
const beginAttempt = quiescenceMethod('BeginProviderAttempt')
const observeIdle = quiescenceMethod('ObserveIdle')
const quiescenceByHost = new WeakMap()
const handleTurn = async (host, turn) => {
  const gate = quiescenceByHost.get(host)
  assert.ok(gate, 'assistance harness must own the quiescence gate')

  if (turn.Outcome.tag === 3) beginAttempt(gate, turn.SessionId)

  const observed = await rawHandleTurn(
    host,
    new ReconciledTurnContext(turn, undefined, ReconciledTurnDelivery.Observation),
  )
  if (turn.Outcome.tag !== 3) return observed

  // Production NEEDHELP owns its typed abort, so HostSignalBootstrap does not
  // revoke this attempt. The real SessionIdle then mints the permit used by the
  // IdleRevisit; no synthetic new provider attempt occurs between abort and idle.
  const permit = observeIdle(gate, turn.SessionId)
  return rawHandleTurn(host, new ReconciledTurnContext(turn, permit, ReconciledTurnDelivery.IdleRevisit))
}

const outcomeName = (value) => value.cases()[value.tag]

const abortedTurn = (session, root, run, role) =>
  new ReconciledTurn(
    sessionId(session),
    physicalUser(root),
    authorityRoot(root),
    providerRun(run),
    roles.of(role),
    undefined,
    [],
    'abort',
    undefined,
    undefined,
    new TurnOutcome(3, ['needhelp-interrupt']),
    undefined,
  )

const fallbackState = (journal, session) => {
  const snapshot = promptDispatcher.journalSnapshot(journal)
  return fallbackProjection.read(fold.session(snapshot, session).Fallback)
}

const withHarness = async (selectedAgent, fn) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-assistance-'))
  const opened = await agentJournal.create({ directory, runtime: `rt_${selectedAgent}` })
  assert.equal(opened.ok, true, opened.ok ? '' : opened.error)
  const journal = opened.journal
  const dispatcher = forJournal(journal)
  const owner = `ses_${selectedAgent.replaceAll('-', '_')}`
  const root = 'msg_root'
  providerLanguage.clearAllForTests()
  assert.equal(providerLanguage.bindOnce(sessionId(owner), providerLanguage.simplifiedChinese).ok, true)
  const accepted = await Runtime__AcceptHumanRoot(dispatcher, sessionId(owner), physicalUser(root), selectedAgent)
  assert.equal(accepted.tag, 0, accepted.fields?.[0])

  await captureOpening(journal, sessionId(owner), `original ${selectedAgent} charge`, toList([]))

  let seq = 0
  const sends = []
  const creates = []
  const ownedChildren = []
  const sessions = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async (sid, text, options) => {
      seq += 1
      sends.push({
        session: idValue.session(sid),
        text,
        agent: options?.Agent,
        tools: options?.Tools,
      })
      return promptDispatcher.admittedWithPhysicalMessage(`msg_assistance_${seq}`)
    },
    CreateChildSession: async (parent, options) => {
      const child = sessionId(`ses_consult_${creates.length + 1}`)
      const inherited = providerLanguage.inheritFromOwner(providerLanguage.simplifiedChinese, child)
      assert.equal(inherited.ok, true, inherited.error)
      creates.push({ parent: idValue.session(parent), child: idValue.session(child), options })
      return okResult(child)
    },
    AbortSession: async () => okResult(undefined),
    AbortChildren: async () => {},
    ListChildren: async () => okResult(toList([])),
    FamilyRootOf: (sid) => sid,
  }

  const sensor = new NeedHelpSensorModule.NeedHelpSensor(
    () => true,
    async () => okResult(undefined),
  )

  const active = promptDispatcher.projectionFor(dispatcher, owner).ActiveLogicalRun
  assert.ok(active, 'authority root must be active')
  const snapshotRuns = new Map()
  const snapshotPort = {
    GetMessages: async (sid) => okResult(toList(snapshotRuns.get(idValue.session(sid)) ?? [])),
  }

  const quiescence = new QuiescenceModule.SessionQuiescenceGate()
  const host = new AssistanceHostModule.AssistanceHost(
    sessions,
    journal,
    sensor,
    snapshotPort,
    (child) => ownedChildren.push(idValue.session(child)),
  )
  quiescenceByHost.set(host, quiescence)

  const bindRun = (run, agent = selectedAgent) => {
    snapshotRuns.set(owner, [{ Id: run, Role: 'assistant', Agent: agent }])
  }

  try {
    await fn({ journal, dispatcher, owner, root, sends, creates, ownedChildren, sensor, host, bindRun })
  } finally {
    providerLanguage.clearAllForTests()
    opened.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

test('AGENT_031_fast_needhelp_continues_same_session_as_deep_peer_without_moving_fallback', async () => {
  await withHarness('fast-coder', async ({ journal, owner, root, sends, creates, sensor, host, bindRun }) => {
    const run = 'asst_fast_help'
    bindRun(run)
    assert.equal(arm(sensor, sessionId(owner), providerRun(run)), true)
    const before = fallbackState(journal, owner)

    const disposition = await handleTurn(host, abortedTurn(owner, root, run, 'Coder'))
    assert.equal(outcomeName(disposition), 'Handled')
    assert.equal(creates.length, 0)
    assert.equal(sends.length, 1)
    assert.equal(sends[0].session, owner)
    assert.equal(sends[0].agent, 'deep-coder')
    assert.match(sends[0].text, /同一角色的更强推理/)

    const after = fallbackState(journal, owner)
    assert.deepEqual(
      { offset: after.offset, failures: after.failures, exhausted: after.exhausted },
      { offset: before.offset, failures: before.failures, exhausted: before.exhausted },
    )

    const authorityProjection = promptDispatcher.projectionFor(forJournal(journal), owner)
    const acceptedKinds = [...authorityProjection.AcceptedContinuationIds.values()].map((kind) => kind.cases()[kind.tag])
    assert.ok(acceptedKinds.includes('NeedHelpEscalation'))
    assert.ok(!acceptedKinds.includes('ProviderRetryAttempt'))
  })
})

test('AGENT_031_snapshot_agent_binding_turns_fast_escalation_into_deep_consultation_even_while_fallback_stays_fast', async () => {
  await withHarness('fast-coder', async ({ journal, owner, root, sends, creates, sensor, host, bindRun }) => {
    const firstRun = 'asst_fast_first'
    bindRun(firstRun, 'fast-coder')
    assert.equal(arm(sensor, sessionId(owner), providerRun(firstRun)), true)
    assert.equal(outcomeName(await handleTurn(host, abortedTurn(owner, root, firstRun, 'Coder'))), 'Handled')
    assert.equal(sends.at(-1).agent, 'deep-coder')
    assert.equal(creates.length, 0)

    const secondRun = 'asst_now_deep'
    bindRun(secondRun, 'deep-coder')
    assert.equal(arm(sensor, sessionId(owner), providerRun(secondRun)), true)
    assert.equal(outcomeName(await handleTurn(host, abortedTurn(owner, root, secondRun, 'Coder'))), 'Handled')
    assert.equal(creates.length, 1, 'deep binding must consult instead of escalating from fallback cursor')
    assert.equal(sends.at(-1).agent, 'deep-inquiry')

    const state = fallbackState(journal, owner)
    assert.deepEqual({ offset: state.offset, failures: state.failures }, { offset: 0, failures: 0 })
  })
})
