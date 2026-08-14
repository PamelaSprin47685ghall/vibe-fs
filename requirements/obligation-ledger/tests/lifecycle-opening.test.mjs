// Split from tests/unit/glory/lifecycle.test.mjs (cutover Wave 2a); owner: obligation-ledger.
//
// GLORY_021 (WorkActivated fixes the protected prefix end once — O-17 inert
// decode anchor) and GLORY_074 (T1 revelation hook — O-16 Opening-floor
// anchor). The remaining lifecycle fact algebra / finality narrative golden
// bytes moved to requirements/finality/tests/lifecycle.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  envelope,
  fold,
  managerLifecycleFact,
  managerLifeId,
  physicalUser,
  promptKey,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

const SESSION = sessionId('ses_a')
const LIFE = managerLifeId('life-1')
const OPENING_MSG = physicalUser('msg-open-1')
const BLOB = blobRef('blob-1')
const DIGEST = blobDigest('d-1')
const KEY = promptKey('key-1')

const lifecycleEnv = (fact) => envelope({ stream: stream.session(SESSION), fact })

const lifeOpened = () =>
  managerLifecycleFact('LifeOpened', {
    SessionId: SESSION,
    LifeId: LIFE,
    OpeningUserMessageId: OPENING_MSG,
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })

const workActivated = () =>
  managerLifecycleFact('WorkActivated', {
    SessionId: SESSION,
    LifeId: LIFE,
    ActivationPromptKey: KEY,
    ProtectedPrefixEndSequence: 42n,
  })

const life = (session) => fold.session(session, 'ses_a')?.ManagerLife

test('GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once', () => {
  const once = fold.apply(fold.empty, [lifecycleEnv(lifeOpened()), lifecycleEnv(workActivated())])
  assert.equal(once.ok, true, JSON.stringify(once.error))
  const end = life(once.value).CurrentLife.ProtectedPrefixEnd
  assert.equal(Number(end.Sequence), 42)

  // Replay of the same activation is idempotent (PERSIST-010).
  const replay = fold.apply(once.value, [lifecycleEnv(workActivated())])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(Number(life(replay.value).CurrentLife.ProtectedPrefixEnd.Sequence), 42)
})

test('GLORY_074_t1_revelation_hook', async () => {
  const { managerNarrative } = await import('../../verification-system/tests/support/glory.mjs')
  const wrapped = managerNarrative.wrapT1AcceptedResult('checkpoint body')
  assert.ok(wrapped.startsWith('# The account has been accepted.'))
  assert.ok(wrapped.includes('The Manager who will carry it is you.'))
  assert.ok(wrapped.includes('checkpoint body'))
})
