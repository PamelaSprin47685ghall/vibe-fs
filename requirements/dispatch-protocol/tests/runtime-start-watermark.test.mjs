// Split from tests/unit/journal/envelope.test.mjs (cutover Wave 2a); owner: dispatch-protocol
//
// DISPATCH-PROTOCOL-002 (budget 派生): PROMPT-011 counts plugin starts as
// recovery attempts, but the start is a workspace fact — rewriting every
// session map would be O(starts × sessions) on replay. Claims store the
// watermark at registration; later starts only move the workspace counter.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  authority,
  authorityRoot,
  envelope,
  fact,
  fold,
  logicalRunId,
  mapTryFind,
  promptKey,
  runtimeStartedFact,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

test('PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session', () => {
  const sesA = sessionId('ses_a')
  const sesB = sessionId('ses_b')
  const keyA = promptKey('pk_a')
  const keyB = promptKey('pk_b')
  const keyLate = promptKey('pk_late')

  const claimed = (session, key, seq) =>
    envelope({
      seq,
      stream: stream.session(session),
      fact: fact('PluginPromptClaimed', {
        PromptKey: key,
        SessionId: session,
        ContinuationKind: 'ManagerGuard',
        LogicalRunId: logicalRunId(`run-${seq}`),
        AuthorityRootUserMessageId: authorityRoot(`root-${seq}`),
        EffectiveAgent: 'fast-coder',
        PayloadDigest: `pd-${seq}`,
      }),
    })

  const started = (seq, runtime) =>
    envelope({
      seq,
      runtime,
      stream: stream.workspace(),
      fact: runtimeStartedFact({ runtime }),
    })

  const folded = fold.apply(fold.empty, [
    claimed(sesA, keyA, 1),
    claimed(sesB, keyB, 2),
    started(3, 'rt-1'),
    started(4, 'rt-2'),
    claimed(sesA, keyLate, 5),
    started(6, 'rt-3'),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const projections = folded.value.AgentProjections
  assert.equal(projections.RuntimeStartCount, 3)

  const sessionA = fold.session(folded.value, 'ses_a')
  const sessionB = fold.session(folded.value, 'ses_b')
  const earlyA = mapTryFind(keyA, sessionA.PromptAuthority.PendingClaims)
  const earlyB = mapTryFind(keyB, sessionB.PromptAuthority.PendingClaims)
  const lateA = mapTryFind(keyLate, sessionA.PromptAuthority.PendingClaims)

  assert.equal(earlyA.ClaimedAtRuntimeStartCount, 0)
  assert.equal(earlyB.ClaimedAtRuntimeStartCount, 0)
  assert.equal(lateA.ClaimedAtRuntimeStartCount, 2)

  assert.deepEqual(
    [authority.recoveryAttempts(3, earlyA), authority.recoveryBudgetSpent(3, earlyA)],
    [3, true],
  )
  assert.deepEqual(
    [authority.recoveryAttempts(3, earlyB), authority.recoveryBudgetSpent(3, earlyB)],
    [3, true],
  )
  assert.deepEqual(
    [authority.recoveryAttempts(3, lateA), authority.recoveryBudgetSpent(3, lateA)],
    [1, false],
  )
})
