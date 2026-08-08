import assert from 'node:assert/strict'
import test from 'node:test'
import { buildTurn } from '../../../dist/Application/Reconciliation/CompletedTurnClassifier.js'
import { shouldActivate } from '../../../dist/Application/Reconciliation/ManagerLifecycleGate.js'
import {
  authorityRoot,
  blobDigest,
  blobRef,
  envelope,
  fold,
  managerLifecycleFact,
  managerLifeId,
  physicalUser,
  reconcileSupervisor,
  roles,
  sessionId,
  stream,
} from '../support/domain.mjs'

const session = sessionId('manager-lifecycle-gate')

const planningLife = () => {
  const opened = managerLifecycleFact('LifeOpened', {
    SessionId: session,
    LifeId: managerLifeId('life-1'),
    OpeningUserMessageId: physicalUser('user-1'),
    OpeningTextRef: blobRef('opening-1'),
    OpeningTextDigest: blobDigest('digest-1'),
    OpeningCursorSequence: 1n,
  })
  const projection = fold.apply(fold.empty, [envelope({ stream: stream.session(session), fact: opened })])
  assert.equal(projection.ok, true, JSON.stringify(projection.error))
  return { projection: projection.value }
}

test('GLORY_018_in_progress_manager_turn_never_activates', () => {
  const turn = buildTurn(
    session,
    physicalUser('user-1'),
    authorityRoot('root-1'),
    reconcileSupervisor.message({
      id: 'assistant-1',
      role: 'assistant',
      finish: 'tool-calls',
      parts: [reconcileSupervisor.textPart('planning continues')],
    }),
    roles.of('Manager'),
    undefined,
  )

  assert.equal(shouldActivate(planningLife(), turn), false)
})
