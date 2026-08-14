// CTX-016 / TODO-001 — the Opening floor is a structural cursor derived from
// LifeOpened + XTrace, never from WorkActivated / planning-stage business.
//
// This file pins the context-compression side: Blogger/Y effective start is
// `max(RecordCoverage, WorkRecordStart)`, the floor comes from the XTrace
// head while Opening is open (Pre-T1), and the legacy WorkActivated fact is
// inert — folding it must not move the floor.

import assert from 'node:assert/strict'
import test from 'node:test'
import { envelope, stream, sessionId, physicalUser, managerLifeId, blobRef, blobDigest, providerRun, fact, fold, managerLifecycleFact } from '../../verification-system/tests/support/domain.mjs'

const { floorSequence } = await import('../../../dist/Journal/ManagerOpeningFloor.js')
const { bloggerEffectiveStart } = await import('../../../dist/Domain/MagicTodo.js')

const SESSION = sessionId('ses_floor')
const LIFE = managerLifeId('life-1')

let seq = 0
const env = (factValue, run) => envelope({ seq: (seq += 1), stream: stream.session(SESSION), run, fact: factValue })

const lifeOpened = () =>
  env(
    managerLifecycleFact('LifeOpened', {
      SessionId: SESSION,
      LifeId: LIFE,
      OpeningUserMessageId: physicalUser('msg-open-1'),
      OpeningTextRef: blobRef('blob-open'),
      OpeningTextDigest: blobDigest('d-open'),
      OpeningCursorSequence: 1n,
    }),
    'msg_open',
  )

const workActivated = () =>
  env(
    managerLifecycleFact('WorkActivated', {
      SessionId: SESSION,
      LifeId: LIFE,
      ActivationPromptKey: 'key-1',
      ProtectedPrefixEndSequence: 42n,
    }),
    'msg_activated',
  )

const partFact = ({ sequence, role = 'user', kind = 'text', run = `msg_p${sequence}` } = {}) =>
  env(
    fact('XTracePartAppended', {
      SessionId: SESSION,
      CursorSequence: BigInt(sequence),
      Role: role,
      Turn: 0,
      PartIndex: 0,
      Kind: kind,
      ToolName: undefined,
      TextRef: blobRef(`blob-p${sequence}`),
      TextDigest: blobDigest(`sha-p${sequence}`),
      Provenance: `g:0/turn:0/part:0`,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = fold.apply(fold.empty, envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value.AgentProjections
}

test('CTX_016_pre_t1_floor_is_the_xtrace_head_not_an_activation_cursor', () => {
  // LifeOpened + two XTrace parts; no todowrite accepted yet → Opening still
  // open, Blogger must not start before the XTrace head (structural floor).
  const projections = foldOk([lifeOpened(), partFact({ sequence: 1 }), partFact({ sequence: 2 })])
  assert.equal(Number(floorSequence(SESSION, projections)), 2, 'Pre-T1 floor = XTrace head (exclusive)')
})

test('CTX_016_work_activated_is_inert_and_does_not_move_the_floor', () => {
  const base = [lifeOpened(), partFact({ sequence: 1 }), partFact({ sequence: 2 })]
  const without = foldOk(base)
  const withLegacy = foldOk([...base, workActivated()])

  const before = Number(floorSequence(SESSION, without))
  const after = Number(floorSequence(SESSION, withLegacy))
  assert.equal(after, before, 'WorkActivated (inert legacy) must not change the structural floor')
  assert.notEqual(after, 42, 'the legacy ProtectedPrefixEndSequence (42) must never be read')
})

test('CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor', () => {
  const floor = { Sequence: 3n }

  const coverageBehind = { IngestedThrough: { Sequence: 1n } }
  assert.equal(
    Number(bloggerEffectiveStart(coverageBehind, floor).Sequence),
    3,
    'coverage behind floor → effective start = floor',
  )

  const coverageAhead = { IngestedThrough: { Sequence: 5n } }
  assert.equal(
    Number(bloggerEffectiveStart(coverageAhead, floor).Sequence),
    5,
    'coverage ahead of floor → effective start = record coverage',
  )
})
