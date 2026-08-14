// Split from tests/unit/host/assistance-host.test.mjs (cutover Wave 2a);
// owner: delegation. DELEG-018 consultation 委托/advice 路由半边：
// NEEDHELP consultation 是真实 child 委托 —— 一个 inquiry consultation parent/child、
// Commissioner LWR 携带 parent opening、child→parent advice 路由、每 help occasion
// 至多一个 consultation、owner drop 放弃 active consultation 且 late child terminal
// 不得向已 drop 的 owner 回发 advice。
// fast 升级/fallback 不动等 authority 语义断言归 interaction-authority。

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
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import {
  ReconciledTurn,
  ReconciledTurnContext,
  ReconciledTurnDelivery,
} from '../../../dist/Composition/Turn/Observation.js'
import { forJournal, Runtime__AcceptHumanRoot } from '../../../dist/Application/Prompting/PromptDispatcher.js'
import { captureOpening } from '../../../dist/Context/Trace/Capture.js'

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
const recover = assistanceMethod('Recover')
const dropSession = assistanceMethod('DropSession')

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

const completedTurn = (session, root, run, role, text = 'independent perspective') =>
  new ReconciledTurn(
    sessionId(session),
    physicalUser(root),
    authorityRoot(root),
    providerRun(run),
    roles.of(role),
    undefined,
    [reconcileSupervisor.textPart(text)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
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

test('AGENT_031_deep_needhelp_uses_one_real_inquiry_consultation_parent_and_child_lwr_then_returns_to_same_deep_binding', async () => {
  await withHarness('deep-coder', async ({ journal, owner, root, sends, creates, ownedChildren, sensor, host, bindRun }) => {
    const run = 'asst_deep_help'
    bindRun(run)
    assert.equal(arm(sensor, sessionId(owner), providerRun(run)), true)
    const before = fallbackState(journal, owner)

    const first = await handleTurn(host, abortedTurn(owner, root, run, 'Coder'))
    assert.equal(outcomeName(first), 'Handled')
    assert.equal(creates.length, 1)
    assert.equal(creates[0].parent, owner)
    assert.equal(creates[0].options.Agent, 'deep-inquiry')
    assert.deepEqual(ownedChildren, ['ses_consult_1'])
    assert.equal(sends.length, 1)
    assert.equal(sends[0].session, 'ses_consult_1')
    assert.equal(sends[0].agent, 'deep-inquiry')
    assert.match(sends[0].text, /如何解决这个 agent 的当前困难？/)
    assert.match(sends[0].text, /original deep-coder charge/, 'Commissioner LWR must carry parent opening')

    xTraceCapture.captureProjection(
      journal,
      sessionId('ses_consult_1'),
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text(sends[0].text)] },
          { role: 'assistant', parts: [xTraceCapture.text('independent perspective')] },
        ],
      }),
    )

    const childDone = completedTurn('ses_consult_1', 'msg_assistance_1', 'asst_consult_done', 'Inquiry')
    const returned = await handleTurn(host, childDone)
    assert.equal(outcomeName(returned), 'Handled')
    assert.equal(sends.length, 2)
    assert.equal(sends[1].session, owner)
    assert.equal(sends[1].agent, 'deep-coder')
    assert.match(sends[1].text, /consultation_record/)
    assert.match(sends[1].text, /independent perspective/)
    assert.doesNotMatch(sends[1].text, /如何解决这个 agent 的当前困难？/, 'child→parent LWR must exclude child Opening')

    const after = fallbackState(journal, owner)
    assert.deepEqual(
      { offset: after.offset, failures: after.failures, exhausted: after.exhausted },
      { offset: before.offset, failures: before.failures, exhausted: before.exhausted },
    )

    const repeated = await handleTurn(host, childDone)
    assert.equal(outcomeName(repeated), 'Handled')
    assert.equal(sends.length, 2, 'same consultation completion must not deliver advice twice')
    assert.equal(creates.length, 1, 'same help occasion must never create a second consultation')

    await recover(host)
    assert.equal(sends.length, 2, 'restart recovery must not redeliver accepted advice')
    assert.equal(creates.length, 1)

    const secondHelp = 'asst_deep_help_again'
    bindRun(secondHelp, 'deep-coder')
    assert.equal(arm(sensor, sessionId(owner), providerRun(secondHelp)), true)
    assert.equal(outcomeName(await handleTurn(host, abortedTurn(owner, root, secondHelp, 'Coder'))), 'Handled')
    assert.equal(creates.length, 1, 'finite per-run guard must not mint a second consultation')
    assert.equal(sends.length, 3, 'spent allowance still needs one deterministic owner continuation')
    assert.equal(sends[2].agent, 'deep-coder')
    assert.match(sends[2].text, /consultation_failure/)
    assert.doesNotMatch(sends[2].text, /allowance|budget|remaining|\b\d+\s*(?:helps?|consultations?)\b/i, 'provider text must not expose help budget mechanics')
  })
})

test('AGENT_031_owner_drop_abandons_active_consultation_and_late_child_terminal_cannot_resurrect_owner', async () => {
  await withHarness('deep-coder', async ({ journal, owner, root, sends, creates, sensor, host, bindRun }) => {
    const run = 'asst_cancel_help'
    bindRun(run, 'deep-coder')
    assert.equal(arm(sensor, sessionId(owner), providerRun(run)), true)
    assert.equal(outcomeName(await handleTurn(host, abortedTurn(owner, root, run, 'Coder'))), 'Handled')
    assert.equal(creates.length, 1)
    assert.equal(sends.length, 1)

    const dropped = dropSession(host, sessionId(owner))
    assert.equal(typeof dropped?.then, 'function', 'owner drop must return an awaitable durable-cleanup Task')
    await dropped
    const late = await handleTurn(
      host,
      completedTurn('ses_consult_1', 'msg_assistance_1', 'asst_late', 'Inquiry', 'late advice'),
    )
    assert.notEqual(outcomeName(late), 'NotAssistance')
    assert.equal(sends.length, 1, 'late consultation result must not send anything back to dropped owner')
  })
})
