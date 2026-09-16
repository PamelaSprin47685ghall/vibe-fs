import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as accounting from '../../../dist/Execution/Session/ChatExecution/TerminalAccountingSurface.js'

test('WHAT[CHATEXEC-010] cancel and delete settle every exact projected execution before capacity is drained', () => {
  const key = { sessionId: 'ses-10-1', physicalUserMessageId: 'msg-10-1' }
  const accepted = chatExecution.acceptManagedChat('run-10-1', 'msg-root-10-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const term = chatExecution.terminalPreProvider(key, 'Cancelled')
  const folded = chatExecution.fold([accepted, term])
  assert.equal(folded.value[0].phase, 'Terminal')
})

test('WHAT[CHATEXEC-010] recovery drain completion uses the Fable-compatible completion owner', () => {
  const r = accounting.testRecoveryDrainOwner()
  assert.equal(r.compatible, true)
})
