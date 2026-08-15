import assert from 'node:assert/strict'
import test from 'node:test'
import { StrictMockProvider } from './e2e/support/strict-mock-provider.js'

test('VERIFY_afterExpectation_observation_preserves_physical_session_for_early_and_late_barriers', () => {
  const provider = new StrictMockProvider()
  let early = null
  provider.afterExpectation('orch.2', (observation) => { early = observation })

  provider._signals.consume({ id: 'orch.2', permanent: true })
  provider._runAfterExpectation('orch.2', { sessionId: 'ses-orch', parentSessionId: 'ses-parent' })

  assert.deepEqual(early, {
    id: 'orch.2',
    attempt: 1,
    sessionId: 'ses-orch',
    parentSessionId: 'ses-parent',
  })

  let late = null
  provider.afterExpectation('orch.2', (observation) => { late = observation })
  assert.deepEqual(late, early, 'late barrier must recover the observation from the matched physical attempt')
})
