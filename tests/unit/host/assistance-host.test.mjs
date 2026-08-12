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
  providerRun,
  roles,
  sessionId,
  toList,
} from '../support/domain.mjs'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { forJournal, Runtime__AcceptHumanRoot } from '../../../dist/Application/Prompting/PromptDispatcher.js'
import { captureOpening, captureTerminalText } from '../../../dist/Application/Reconciliation/XTraceCapture.js'
import * as NeedHelpSensorModule from '../../../dist/Infrastructure/OpenCode/Host/NeedHelpSensor.js'
import * as AssistanceHostModule from '../../../dist/Infrastructure/OpenCode/Host/AssistanceHost.js'

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

const arm = sensorMethod('TryArm')
const handleTurn = assistanceMethod('HandleTurn')
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

const completedTurn = (session, root, run, role) =>
  new ReconciledTurn(
    sessionId(session),
    physicalUser(root),
    authorityRoot(root),
    providerRun(run),
    roles.of(role),
    undefined,
    [],
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
  const opened = agentJournal.create({ directory, runtime: `rt_${selectedAgent}` })
  assert.equal(opened.ok, true, opened.ok ? '' : opened.error)
  const journal = opened.journal
  const dispatcher = forJournal(journal)
  const owner = `ses_${selectedAgent.replaceAll('-', '_')}`
  const root = 'msg_root'
  const accepted = Runtime__AcceptHumanRoot(dispatcher, sessionId(owner), physicalUser(root), selectedAgent)
  assert.equal(accepted.tag, 0, accepted.fields?.[0])

  captureOpening(journal, sessionId(owner), `original ${selectedAgent} charge`, toList([]))

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

  const host = new AssistanceHostModule.AssistanceHost(
    sessions,
    journal,
    sensor,
    snapshotPort,
    (child) => ownedChildren.push(idValue.session(child)),
  )

  const bindRun = (run, agent = selectedAgent) => {
    snapshotRuns.set(owner, [{ Id: run, Role: 'assistant', Agent: agent }])
  }

  try {
    await fn({ journal, dispatcher, owner, root, sends, creates, ownedChildren, sensor, host, bindRun })
  } finally {
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

    const childSession = sessionId('ses_consult_1')
    captureTerminalText(journal, childSession, 'independent perspective', providerRun('asst_consult_done'))

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

    dropSession(host, sessionId(owner))
    captureTerminalText(journal, sessionId('ses_consult_1'), 'late advice', providerRun('asst_late'))
    const late = await handleTurn(host, completedTurn('ses_consult_1', 'msg_assistance_1', 'asst_late', 'Inquiry'))
    assert.notEqual(outcomeName(late), 'NotAssistance')
    assert.equal(sends.length, 1, 'late consultation result must not send anything back to dropped owner')
  })
})
