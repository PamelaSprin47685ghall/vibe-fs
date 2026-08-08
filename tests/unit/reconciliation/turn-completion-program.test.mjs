// TCP_: TurnCompletionProgram.applyWithContinuation — the terminal decision
// table driven through a REAL journal (seeded facts) plus fake ports, exactly
// like the runtime reaches it. Every branch of the match on turn.Outcome is
// exercised: repair paths (TurnUnknown / TurnNeedsContinuation / interaction
// repair), probe-run hijack protection, reviewer confirmation completion,
// abort bridging, provider-failure continuation, and the TurnCompleted
// sub-decisions (join, planning, finality, manager hand-off, encouragement).

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  authorityRoot,
  commitHash,
  fact,
  gitTreeHash,
  handleId,
  handleOwnership,
  idValue,
  logicalRunId,
  managerJobId,
  payloadOf,
  physicalUser,
  promptDispatcher,
  promptKey,
  providerRun,
  reviewBarrierId,
  roles,
  sealDigest,
  sessionId,
  stream,
  toolCallId,
  transportReceipt,
  xTraceCapture,
} from '../support/domain.mjs'
import { buildTurn } from '../../../dist/Application/Reconciliation/CompletedTurnClassifier.js'
import { SessionMessage } from '../../../dist/Infrastructure/OpenCode/Host/SessionSnapshotPort.js'
import { applyWithContinuation } from '../../../dist/Application/Reconciliation/TurnCompletionProgram.js'
import { LoopSensor, LoopSensor__IsArmed_Z31B28506, LoopSensor__TryArm_Z31B28506 } from '../../../dist/Infrastructure/OpenCode/Host/LoopSensor.js'
import {
  SessionQuiescenceGate,
  SessionQuiescenceGate__BeginProviderAttempt_Z31B28506 as beginProviderAttempt,
  SessionQuiescenceGate__ObserveIdle_Z31B28506 as observeIdle,
} from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { AgentJournalModule_appendManagerLifecycle } from '../../../dist/Journal/AgentJournal.js'

const text = (value) => xTraceCapture.text(value)

const assistant = ({
  id = 'asst-1',
  agent = undefined,
  finish = undefined,
  errorName = undefined,
  completed = false,
  parts = [],
} = {}) => new SessionMessage(id, 'assistant', agent, finish, errorName, undefined, undefined, completed, false, undefined, parts)

const SESSION = 'ses_tcp'

const turn = ({
  session = SESSION,
  physical = 'user-1',
  root = 'user-1',
  id = 'asst-1',
  roleAgent,
  finish,
  errorName,
  completed,
  parts,
}) =>
  buildTurn(
    sessionId(session),
    physicalUser(physical),
    authorityRoot(root),
    assistant({ id, agent: roleAgent, finish, errorName, completed, parts }),
    undefined,
    '/repo/dir',
  )

/** Real journal in a temp dir; facts appended through the fold (appendAgent). */
const liveJournal = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-tcp-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  const append = (factValue, run = undefined) => {
    const result = agentJournal.appendAgent(stream.session(sessionId(SESSION)), run, factValue, opened.journal)
    assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  }
  return {
    journal: opened.journal,
    append,
    snapshot: () => agentJournal.snapshot(opened.journal),
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const seedAuthority = (append, { root = 'user-1', agent = 'fast-coder', role = 'coder' } = {}) => {
  append(
    agentFact('AuthorityRootAccepted', {
      SessionId: sessionId(SESSION),
      LogicalRunId: logicalRunId('run-1'),
      AuthorityRootUserMessageId: authorityRoot(root),
      AuthorityKind: 'HumanRoot',
      SelectedAgent: agent,
      PeerAgent: 'deep-coder',
      CanonicalRole: role,
      SelectedTier: 'fast',
    }),
  )
}

/** PROMPT-003: mark the physical message as accepted as a continuation kind. */
const seedAcceptedContinuation = (append, { physical = 'user-1', root = 'user-1', kind = 'ProviderRetryAttempt' } = {}) => {
  append(
    agentFact('PluginPromptClaimed', {
      PromptKey: promptKey('key-probe'),
      SessionId: sessionId(SESSION),
      ContinuationKind: kind,
      LogicalRunId: logicalRunId('run-1'),
      AuthorityRootUserMessageId: authorityRoot(root),
      EffectiveAgent: 'fast-coder',
      PayloadDigest: 'digest-1',
    }),
  )
  append(
    agentFact('PluginPromptSubmitted', {
      PromptKey: promptKey('key-probe'),
      Receipt: transportReceipt('rcpt-probe'),
      SessionId: sessionId(SESSION),
    }),
  )
  append(
    agentFact('PluginPromptPhysicalAccepted', {
      PromptKey: promptKey('key-probe'),
      PhysicalUserMessageId: physicalUser(physical),
      SessionId: sessionId(SESSION),
    }),
  )
}

const seedFallbackCursor = (append) => {
  append(
    agentFact('FallbackCursorAdvanced', {
      SessionId: sessionId(SESSION),
      LogicalRunId: logicalRunId('run-1'),
      AuthorityRootUserMessageId: authorityRoot('user-1'),
      ProviderRun: providerRun('asst-0'),
      PreviousOffset: 0,
      NextOffset: 1,
      ConsecutiveFailureCount: 1,
      Reason: 'provider_error',
    }),
  )
}

/** A confirmed reviewer: ConfirmedReviewWitness folds the witness onto the reviewer. */
const seedConfirmedReviewer = (append) => {
  append(
    agentFact('ConfirmedReviewWitness', {
      BarrierId: reviewBarrierId('bar-1'),
      ChallengeResultDigest: sealDigest('chal-1'),
      SecondProviderInputDigest: sealDigest('in-1'),
      FirstProviderRun: providerRun('rev-1'),
      FirstToolCallId: toolCallId('tc-1'),
      GitTreeHash: gitTreeHash('tree-1'),
      ReviewerSessionId: sessionId(SESSION),
      SecondProviderRun: providerRun('rev-2'),
      SecondToolCallId: toolCallId('tc-2'),
      ManagerSessionId: sessionId(SESSION),
    }),
  )
}

const seedManagerJob = (append, { progress = 'CandidateReady' } = {}) => {
  append(
    agentFact('ManagerJobCreated', {
      ManagerJobId: managerJobId('mj-1'),
      ManagerSessionId: sessionId(SESSION),
      ManagerAgent: 'fast-manager',
      WorktreeIdentity: undefined,
      WorktreePath: undefined,
      TargetRef: undefined,
      TargetBranchFrozen: false,
    }),
  )
  if (progress === 'CandidateReady') {
    append(
      agentFact('CandidateReady', {
        ManagerJobId: managerJobId('mj-1'),
        CandidateCommit: commitHash('cafe01'),
        PreRebaseReviewBarrierId: reviewBarrierId('bar-9'),
      }),
    )
  } else if (progress === 'ConflictPending') {
    append(
      agentFact('ConflictDetected', {
        ManagerJobId: managerJobId('mj-1'),
        CandidateCommit: commitHash('cafe01'),
        ConflictFiles: [],
      }),
    )
  }
}

/** GLORY-029 Labor Life: opened + activated so idle encouragement is in scope. */
const seedActivatedManagerLife = (journal, { life = 'life-1' } = {}) => {
  const opened = AgentJournalModule_appendManagerLifecycle(
    stream.session(sessionId(SESSION)),
    {
      tag: 0, // LifeOpened
      fields: [
        {
          SessionId: sessionId(SESSION),
          LifeId: { tag: 0, fields: [life] },
          OpeningUserMessageId: physicalUser('user-1'),
          OpeningTextRef: { tag: 0, fields: ['blob-open'] },
          OpeningTextDigest: { tag: 0, fields: ['sha-open'] },
          OpeningCursorSequence: 1,
        },
      ],
    },
    journal,
  )
  assert.equal(opened.tag, 0, opened.tag === 0 ? '' : JSON.stringify(opened.fields))

  const activated = AgentJournalModule_appendManagerLifecycle(
    stream.session(sessionId(SESSION)),
    {
      tag: 1, // WorkActivated
      fields: [
        {
          SessionId: sessionId(SESSION),
          LifeId: { tag: 0, fields: [life] },
          ActivationPromptKey: promptKey('key-activate'),
          ProtectedPrefixEndSequence: 2,
        },
      ],
    },
    journal,
  )
  assert.equal(activated.tag, 0, activated.tag === 0 ? '' : JSON.stringify(activated.fields))
}

const sendCount = (portCalls) =>
  portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync').length

const harness = ({ journal, loopSensor = undefined, hasLivePty = () => false, gate = new SessionQuiescenceGate() } = {}) => {
  const events = []
  const portCalls = []
  const sessionPort = {
    SendPrompt: async (sid, prompt, options) => {
      portCalls.push(['SendPrompt', idValue.session(sid), prompt])
      return { tag: 0, fields: [promptDispatcher.admittedWithReceipt(transportReceipt('rcpt-send'))] }
    },
    SendPromptAsync: async (...args) => {
      portCalls.push(['SendPromptAsync', ...args])
      return { tag: 0, fields: [promptDispatcher.admittedWithReceipt(transportReceipt('rcpt-send'))] }
    },
    SubscribeTerminal: (sid, callback) => {
      portCalls.push(['SubscribeTerminal', idValue.session(sid)])
      return { Dispose: () => {} }
    },
    AbortChildren: async (sid) => {
      portCalls.push(['AbortChildren', idValue.session(sid)])
    },
  }
  const eventPort = {
    NotifyTerminal: (sid, outcome) => {
      events.push([idValue.session(sid), outcome])
    },
  }
  const disposed = []
  const verdictSessions = new Set()
  const nudgeSent = new Set()
  const joinGuardNudges = new Set()
  const sessionParents = new Map()
  const abortedSessions = new Set()

  /** A fresh idle permit minted by THIS harness's gate. */
  const freshPermit = () => {
    beginProviderAttempt(gate, sessionId(SESSION))
    return observeIdle(gate, sessionId(SESSION))
  }

  const run = (turnValue, quiescence = undefined) =>
    applyWithContinuation(
      sessionPort,
      eventPort,
      journal,
      undefined,
      verdictSessions,
      nudgeSent,
      joinGuardNudges,
      sessionParents,
      (key) => disposed.push(key),
      hasLivePty,
      abortedSessions,
      loopSensor,
      gate,
      { Turn: turnValue, Quiescence: quiescence },
    )

  return {
    events,
    portCalls,
    disposed,
    verdictSessions,
    nudgeSent,
    abortedSessions,
    gate,
    freshPermit,
    run,
  }
}

const terminalEvent = (events) => events[0]?.[1]
const eventCase = (events, index = 0) => terminalEvent(events)?.tag
const eventPayload = (events) => payloadOf(terminalEvent(events))

// ── TurnUnknown: repair only with a fresh idle permit ───────────────────────

test('TCP_unknown_no_journal_repairs_and_fails_closed', async () => {
  const h = harness({})
  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }), h.freshPermit())

  assert.deepEqual(h.disposed, ['ses_tcp'], 'disposeExecutorRuntime always runs first')
  assert.equal(h.events.length, 1)
  assert.equal(eventCase(h.events), 2, 'Failed')
  assert.equal(eventPayload(h.events), 'MISSING_FINAL_REPORT')
})

test('Q01_unknown_with_fresh_idle_permit_sends_exactly_one_repair', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }), h.freshPermit())

  assert.deepEqual(h.events, [], 'repair send succeeds: no terminal')
  const sends = h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  assert.equal(sends.length, 1, 'exactly one missing-final-report poke')
  live.cleanup()
})

test('Q02_unknown_with_stale_permit_sends_nothing', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  const h = harness({ journal: live.journal })

  // The core race: the idle permit was minted, then attempt B's transform
  // began (BeginProviderAttempt) before the old reconcile's side effect ran.
  const permit = h.freshPermit()
  beginProviderAttempt(h.gate, sessionId(SESSION))

  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }), permit)

  assert.deepEqual(h.events, [], 'stale permit: zero PromptClaimed, zero terminal')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

test('Q08_probe_run_with_stale_permit_is_not_repaired', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1' })
  const h = harness({ journal: live.journal })

  // CTX-012: ProviderRetryAttempt owns the recovery slot. Even without the
  // stale-permit path, the durable continuation kind alone suppresses repair.
  // Stale permit is an independent HOST-004 gate; both must hold.
  const permit = h.freshPermit()
  beginProviderAttempt(h.gate, sessionId(SESSION))

  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }), permit)

  assert.deepEqual(h.events, [], 'the probe turn must not be hijacked by a repair (CTX-012)')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

// Measured: probe transform BeginProviderAttempt + SessionIdle of the *same*
// attempt mints a valid permit. Stale-permit gating alone cannot suppress the
// race — the durable ProviderRetryAttempt identity must.
test('Q08b_probe_run_with_fresh_idle_permit_is_not_repaired', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }), h.freshPermit())

  assert.deepEqual(h.events, [], 'fresh idle on ProviderRetryAttempt must not hijack the probe')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

test('Q08c_probe_needs_continuation_with_fresh_permit_is_not_repaired', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: 'length', completed: false, parts: [text('truncated')] }), h.freshPermit())

  assert.deepEqual(h.events, [])
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

test('Q08_probe_run_with_no_idle_wake_is_not_repaired', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1' })
  const h = harness({ journal: live.journal })

  // A pass without idle evidence (retry/failure wake) carries no permit:
  // no idle-derived continuation, regardless of the run's category.
  await h.run(turn({ finish: undefined, completed: false, parts: [text('streaming')] }))

  assert.deepEqual(h.events, [])
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

test('TCP_unknown_accepted_continuation_of_other_kind_still_repairs', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1', kind: 'InteractionRepair' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: undefined, completed: false }), h.freshPermit())

  assert.deepEqual(h.events, [], 'repair send succeeds: no terminal')
  assert.equal(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync').length, 1)
  live.cleanup()
})

test('TCP_needs_continuation_repairs_missing_report', async () => {
  const h = harness({})
  await h.run(turn({ finish: 'length', completed: false, parts: [text('truncated')] }), h.freshPermit())

  assert.equal(eventCase(h.events), 2)
  assert.equal(eventPayload(h.events), 'MISSING_FINAL_REPORT')
})

test('TCP_needs_continuation_probe_run_with_stale_permit_skips_repair', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedAcceptedContinuation(live.append, { physical: 'user-1' })
  const h = harness({ journal: live.journal })

  const permit = h.freshPermit()
  beginProviderAttempt(h.gate, sessionId(SESSION))

  await h.run(turn({ finish: 'length', completed: false, parts: [text('truncated')] }), permit)

  assert.deepEqual(h.events, [])
  live.cleanup()
})

test('TCP_needs_continuation_without_permit_sends_nothing', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: 'length', completed: false, parts: [text('truncated')] }))

  assert.deepEqual(h.events, [], 'no idle evidence: no idle-derived continuation')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

// ── TurnInProgress: interaction repair vs confirmed-reviewer completion ────

test('TCP_in_progress_coder_repairs_interaction', async () => {
  const h = harness({})
  await h.run(
    turn({ roleAgent: 'fast-coder', finish: 'tool-calls', completed: false, parts: [text('working')] }),
    h.freshPermit(),
  )

  assert.equal(eventCase(h.events), 2)
  assert.equal(eventPayload(h.events), 'MISSING_FINAL_REPORT')
})

test('TCP_in_progress_without_permit_sends_no_interaction_repair', async () => {
  const h = harness({})
  await h.run(turn({ roleAgent: 'fast-coder', finish: 'tool-calls', completed: false, parts: [text('working')] }))

  assert.deepEqual(h.events, [], 'interaction repair is idle-derived: no permit, no send')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
})

test('TCP_in_progress_confirmed_reviewer_completes_with_fallback_text', async () => {
  const live = liveJournal()
  seedConfirmedReviewer(live.append)
  const h = harness({ journal: live.journal })

  // A second PERFECT is often tool-only: empty parts, InProgress.
  await h.run(turn({ roleAgent: 'fast-reviewer', finish: 'tool-calls', completed: false, parts: [] }))

  assert.equal(eventCase(h.events), 0, 'Completed')
  const runResult = eventPayload(h.events)
  assert.equal(runResult.TerminalText, 'Review confirmed.', 'empty terminal falls back to the witness text')
  assert.equal(runResult.Role.tag, roles.of('Reviewer').tag)
  live.cleanup()
})

test('TCP_needs_continuation_confirmed_reviewer_completes', async () => {
  const live = liveJournal()
  seedConfirmedReviewer(live.append)
  const h = harness({ journal: live.journal })

  await h.run(turn({ roleAgent: 'fast-reviewer', finish: 'length', completed: false, parts: [] }))

  assert.equal(eventCase(h.events), 0)
  assert.equal(eventPayload(h.events).TerminalText, 'Review confirmed.')
  live.cleanup()
})

// ── TurnAborted: loop-kill bridges into the provider-failure path ──────────

test('TCP_aborted_without_loop_sensor_reports_aborted', async () => {
  const h = harness({})
  await h.run(turn({ finish: 'aborted', completed: false, parts: [text('partial')] }))

  assert.equal(eventCase(h.events), 1, 'Aborted')
  assert.equal(eventPayload(h.events), 'finish=aborted')
  assert.ok(h.abortedSessions.has('ses_tcp'), 'aborted session is accumulated')
  assert.ok(h.portCalls.some(([name]) => name === 'AbortChildren'), 'children are aborted')
})

test('TCP_aborted_with_armed_loop_sensor_bridges_to_failure', async () => {
  const sensor = new LoopSensor(false, () => {})
  LoopSensor__TryArm_Z31B28506(sensor, sessionId(SESSION))
  const h = harness({ loopSensor: sensor })

  await h.run(turn({ finish: 'aborted', completed: false, parts: [text('partial')] }))

  assert.equal(eventCase(h.events), 2, 'Failed')
  assert.equal(eventPayload(h.events), 'loop-kill', 'our own kill is provider failure (LOOP-006)')
  assert.equal(LoopSensor__IsArmed_Z31B28506(sensor, sessionId(SESSION)), false, 'arming consumed')
  assert.ok(!h.abortedSessions.has('ses_tcp'), 'a loop-kill is not a user abort')
})

// ── TurnFailed: continue only with a proven fallback state ─────────────────

test('TCP_failed_no_journal_notifies_error', async () => {
  const h = harness({})
  await h.run(turn({ finish: 'error', errorName: 'ProviderBoom', completed: true }))

  assert.equal(eventCase(h.events), 2)
  assert.equal(eventPayload(h.events), 'ProviderBoom')
})

test('TCP_failed_with_fallback_cursor_sends_continuation', async () => {
  const live = liveJournal()
  seedAuthority(live.append)
  seedFallbackCursor(live.append)
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: 'error', errorName: 'ProviderBoom', completed: true }))

  assert.deepEqual(h.events, [], 'an armed continuation succeeds without a terminal')
  const sends = h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  assert.equal(sends.length, 1, 'ProviderRetryAttempt continuation is sent')
  live.cleanup()
})

// ── TurnCompleted: the sub-decision table ──────────────────────────────────

test('TCP_completed_coder_notifies_terminal', async () => {
  const h = harness({})
  await h.run(turn({ roleAgent: 'fast-coder', finish: 'stop', completed: true, parts: [text('the answer')] }))

  assert.equal(eventCase(h.events), 0)
  const runResult = eventPayload(h.events)
  assert.equal(runResult.TerminalText, 'the answer')
  assert.equal(runResult.TurnFormalText, 'the answer')
  assert.equal(runResult.Role.tag, roles.of('Coder').tag)
  assert.equal(idValue.session(runResult.SessionId), 'ses_tcp')
  assert.equal(idValue.providerRun(runResult.ProviderRun), 'asst-1')
  assert.equal(runResult.Directory, '/repo/dir')
})

test('TCP_completed_manager_idle_encouragement_deduped', async () => {
  // Process-local HashSet: same ProviderRun re-reconcile does not resend even
  // before durable ClaimSequences is consulted again.
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal })
  const managerTurn = turn({
    roleAgent: 'fast-manager',
    finish: 'stop',
    completed: true,
    parts: [text('progress')],
    id: 'asst-a',
  })

  await h.run(managerTurn, h.freshPermit())
  await h.run(managerTurn, h.freshPermit())

  assert.deepEqual(h.events, [], 'successful idle sends do not NotifyTerminal')
  assert.equal(sendCount(h.portCalls), 1, 'encouragement claimed once; replay is deduped by encouragementKey')
  live.cleanup()
})

// Four-step causal: occasion = Session + Life + TriggerProviderRun.
// Pending Detached claim for A must not suppress independent occasion B.
test('TCP_completed_manager_idle_encouragement_occasion_dedupe', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal })

  const turnA = turn({
    roleAgent: 'fast-manager',
    finish: 'stop',
    completed: true,
    parts: [text('progress-a')],
    id: 'asst-a',
  })
  const turnB = turn({
    roleAgent: 'fast-manager',
    finish: 'stop',
    completed: true,
    parts: [text('progress-b')],
    id: 'asst-b',
  })

  // A. ProviderRun A idle → exactly one encouragement A
  await h.run(turnA, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 1, 'A: exactly one encouragement')
  assert.deepEqual(h.events, [])

  // Detached leave claim A pending (no PhysicalAccepted) — the bug was a
  // session-wide PendingClaims scan that blocked B while A stayed open.
  const pendingAfterA =
    agentJournal.snapshot(live.journal).AgentProjections.Sessions.get(sessionId(SESSION))?.PromptAuthority
      ?.PendingClaims
  assert.ok(pendingAfterA && pendingAfterA.size >= 1, 'A claim stays pending under Detached')

  // B. re-reconcile A → no duplicate (process-local + durable)
  await h.run(turnA, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 1, 'B: re-reconcile A sends nothing')

  // C. keep A claim pending + new ProviderRun B idle → exactly one B
  await h.run(turnB, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 2, 'C: pending A must not suppress B')

  // D. re-reconcile B → no duplicate
  await h.run(turnB, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 2, 'D: re-reconcile B sends nothing')
  assert.deepEqual(h.events, [])
  live.cleanup()
})

// Durable: ClaimSequences alone still at-most-once after clearing process-local HashSet.
test('TCP_completed_manager_idle_encouragement_durable_claim_sequences', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal })
  const managerTurn = turn({
    roleAgent: 'fast-manager',
    finish: 'stop',
    completed: true,
    parts: [text('progress')],
    id: 'asst-a',
  })

  await h.run(managerTurn, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 1)

  // Simulate process restart: process-local nudgeSent is empty, ClaimSequences remains.
  h.nudgeSent.clear()
  await h.run(managerTurn, h.freshPermit())
  assert.equal(sendCount(h.portCalls), 1, 'ClaimSequences keeps occasion at-most-once after restart')
  assert.deepEqual(h.events, [])
  live.cleanup()
})

test('Q09_manager_idle_encouragement_without_permit_sends_nothing', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal })
  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('progress')] }))

  assert.deepEqual(h.events, [], 'no idle evidence: ManagerIdleEncouragement must not fire')
  assert.equal(sendCount(h.portCalls), 0)
  live.cleanup()
})

test('Q09_manager_idle_encouragement_with_stale_permit_sends_nothing', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal })
  const permit = h.freshPermit()
  beginProviderAttempt(h.gate, sessionId(SESSION))

  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('progress')] }), permit)

  assert.deepEqual(h.events, [], 'stale permit: no stale IdleEncouragement')
  assert.equal(sendCount(h.portCalls), 0)
  live.cleanup()
})

test('TCP_completed_manager_job_handed_off_completes_run', async () => {
  const live = liveJournal()
  seedManagerJob(live.append, { progress: 'CandidateReady' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('job done')] }))

  assert.equal(eventCase(h.events), 0, 'the manager run completes once the Orchestrator owns the job (ORCH-006)')
  assert.equal(eventPayload(h.events).TerminalText, 'job done')
  assert.deepEqual(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync'), [])
  live.cleanup()
})

test('TCP_completed_manager_conflict_pending_notifies_terminal', async () => {
  const live = liveJournal()
  seedManagerJob(live.append, { progress: 'ConflictPending' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('conflict turn')] }))

  assert.equal(eventCase(h.events), 0, 'ConflictPending must NotifyTerminal so ResumeManager can finalizeWorktree')
  assert.equal(eventPayload(h.events).TerminalText, 'conflict turn')
  live.cleanup()
})

test('TCP_in_progress_manager_conflict_pending_repairs_interaction', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  seedManagerJob(live.append, { progress: 'ConflictPending' })
  const h = harness({ journal: live.journal })

  await h.run(turn({ roleAgent: 'fast-manager', finish: 'tool-calls', completed: false, parts: [text('guard round')] }), h.freshPermit())

  assert.deepEqual(h.events, [], 'ConflictPending is not handed off for an in-progress manager: repair, not completion')
  assert.equal(h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync').length, 1)
  live.cleanup()
})

test('TCP_completed_manager_planning_sends_work_activation', async () => {
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  const result = AgentJournalModule_appendManagerLifecycle(
    stream.session(sessionId(SESSION)),
    {
      tag: 0,
      fields: [
        {
          SessionId: sessionId(SESSION),
          LifeId: { tag: 0, fields: ['life-1'] },
          OpeningUserMessageId: physicalUser('user-1'),
          OpeningTextRef: { tag: 0, fields: ['blob-open'] },
          OpeningTextDigest: { tag: 0, fields: ['sha-open'] },
          OpeningCursorSequence: 1,
        },
      ],
    },
    live.journal,
  )
  assert.equal(result.tag, 0, result.tag === 0 ? '' : JSON.stringify(result.fields))
  const h = harness({ journal: live.journal })

  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('planning done')] }))

  assert.deepEqual(h.events, [], 'a planning terminal defers completion (GLORY-018)')
  const sends = h.portCalls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  assert.equal(sends.length, 1, 'exactly one ManagerWorkActivation continuation')
  live.cleanup()
})

test('TCP_completed_manager_deferred_encouragement_fails_closed_without_profile', async () => {
  // Life is present so idle is in scope; missing authority profile fails closed on send.
  const live = liveJournal()
  seedActivatedManagerLife(live.journal)
  const h = harness({ journal: live.journal, hasLivePty: () => true })
  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('done')] }), h.freshPermit())

  assert.equal(h.events.length, 1, 'completion deferred (GLORY-070), encouragement attempt fails closed with a terminal')
  assert.equal(eventCase(h.events), 2)
  assert.equal(eventPayload(h.events), 'No active authority profile')
  assert.equal(
    sendCount(h.portCalls),
    0,
    'encouragement fails closed without an authority profile (no physical send)',
  )
  live.cleanup()
})

test('TCP_completed_manager_idle_without_life_skips', async () => {
  // No CurrentLife: skip rather than claim an unscoped idle encouragement.
  const live = liveJournal()
  seedAuthority(live.append, { agent: 'fast-manager', role: 'manager' })
  const h = harness({ journal: live.journal })
  await h.run(turn({ roleAgent: 'fast-manager', finish: 'stop', completed: true, parts: [text('done')] }), h.freshPermit())

  assert.deepEqual(h.events, [], 'no open Life: idle encouragement is skipped closed')
  assert.equal(sendCount(h.portCalls), 0)
  live.cleanup()
})
// ── linked child: EXEC-009 / PROMPT-008 authority registration ─────────────

test('TCP_linked_child_registers_agent_owner_authority', async () => {
  const live = liveJournal()
  live.append(
    agentFact('HandleLinked', {
      ParentSessionId: sessionId('ses_parent'),
      Handle: handleId.agent('ag-1'),
      ChildSessionId: sessionId(SESSION),
      TargetAgent: 'fast-coder',
      CanonicalRole: roles.of('Coder'),
      Ownership: handleOwnership.durableParentHandle(),
    }),
  )
  const h = harness({ journal: live.journal })

  await h.run(turn({ finish: undefined, completed: false }))

  const projection = agentJournal.snapshot(live.journal).AgentProjections
  const authorityState = projection.Sessions.get(sessionId(SESSION))?.PromptAuthority
  assert.ok(authorityState?.ActiveLogicalRun, 'the child gains a durable AgentOwner root (EXEC-009)')
  assert.equal(authorityState.ActiveLogicalRun.SelectedAgent, 'fast-coder')
  live.cleanup()
})
