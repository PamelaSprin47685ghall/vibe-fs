// Split from the lifecycle proof; owner: obligation-ledger.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as envelope from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'

const SESSION = 'ses_a'
const LIFE = 'life-1'
const lifecycle = (caseName) => ({
  caseName,
  payload: caseName === 'LifeOpened'
    ? {
        sessionId: SESSION,
        lifeId: LIFE,
        openingUserMessageId: 'msg-open-1',
        openingTextRef: 'blob-1',
        openingTextDigest: 'd-1',
        openingCursorSequence: 1,
      }
    : {
        sessionId: SESSION,
        lifeId: LIFE,
        activationPromptKey: 'key-1',
        protectedPrefixEndSequence: 42,
      },
})

test('WHAT[OBLIGATION-LEDGER-017] WorkActivated is an inert legacy fact: it fixes ProtectedPrefixEnd once but never re-decides work eligibility', () => {
  const once = envelope.foldLifecycleSequence(SESSION, [lifecycle('LifeOpened'), lifecycle('WorkActivated')])
  assert.equal(once.ok, true, JSON.stringify(once.error))
  assert.equal(once.protectedPrefixEnd, 42)

  const replay = envelope.foldLifecycleSequence(SESSION, [
    lifecycle('LifeOpened'),
    lifecycle('WorkActivated'),
    lifecycle('WorkActivated'),
  ])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(replay.protectedPrefixEnd, 42)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 revelation hook wraps the accepted result with entrustment', () => {
  const wrapped = todo.wrapT1AcceptedResult(SESSION, 'checkpoint body')
  assert.ok(wrapped.startsWith('# The account has been accepted.'))
  assert.ok(wrapped.includes('The Manager who will carry it is you.'))
  assert.ok(wrapped.includes('checkpoint body'))
})
