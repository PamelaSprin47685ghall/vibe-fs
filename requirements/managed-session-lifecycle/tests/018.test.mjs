import assert from 'node:assert/strict'
import test from 'node:test'
import * as interrupt from '../../../dist/Execution/Session/InterruptBoundarySurface.js'
import * as shutdown from '../../../dist/Execution/Session/ShutdownDrainContractSurface.js'

test('WHAT[MANAGED-SESSION-018] TurnAborted has no logical child-cancel authority', () => {
  assert.equal(interrupt.testTurnAbortedNoCancelAuthority(), true)
})

test('WHAT[MANAGED-SESSION-018] shutdown detaches session runtimes before journal release without logical cancel', () => {
  assert.equal(shutdown.testShutdownDetachesBeforeRelease(), true)
})

test('WHAT[MANAGED-SESSION-018] fork terminal callbacks drain before either detach or authorized parent cancel', () => {
  assert.equal(shutdown.testForkTerminalCallbacksDrain(), true)
})

test('WHAT[MANAGED-SESSION-018] TurnAborted publishes attempt terminal without child cascade', () => {
  assert.equal(shutdown.testTurnAbortedNoChildCascade(), true)
})
