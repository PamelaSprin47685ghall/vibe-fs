import assert from 'node:assert/strict'
import test from 'node:test'
import { buildTurn } from '../../../dist/Application/Reconciliation/CompletedTurnClassifier.js'
import { ensureAccepted } from '../../../dist/Application/Manager/ManagerActivation.js'
import {
  agentJournal,
  authorityRoot,
  blobDigest,
  blobRef,
  caseOf,
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

test('GLORY_018_in_progress_manager_turn_never_activates', async () => {
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

  let sent = false
  const fakeSessionPort = {
    SendPrompt: async () => { sent = true; return { ok: true } },
    SubscribeTerminal: () => ({ Dispose: () => {} }),
  }
  const fakeEventPort = {
    NotifyTerminal: () => {},
  }

  const result = await ensureAccepted(fakeSessionPort, fakeEventPort, undefined, turn)
  assert.equal(sent, false, 'in-progress turn must not send activation')
  assert.equal(caseOf(result), 'Deferred')
})
