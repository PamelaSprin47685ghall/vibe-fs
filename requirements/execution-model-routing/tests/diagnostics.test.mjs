import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { createCounters, queryReliability, snapshot } from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'

const target = { model: 'provider/shared', reasoning: 'none' }
const identity = { sessionId: 'session-a', physicalUserMessageId: 'message-a', effectiveAgent: 'coder' }

test('WHAT[EMR-015] diagnostic query reuses capacity snapshot queue and fence counters without duplicate formula', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await routing.acquireExecutionAdmission(
    runtime,
    identity.sessionId,
    identity.physicalUserMessageId,
    identity.effectiveAgent,
  )
  assert.equal(acquired.kind, 'Acquired')
  routing.releaseExecutionAdmissionBeforeProvider(runtime, acquired.lease, { ...identity, target })
  routing.releaseExecutionAdmissionBeforeProvider(runtime, acquired.lease, { ...identity, target })

  const capacity = routing.capacitySnapshot(runtime)
  const counters = createCounters()
  const result = queryReliability(
    counters,
    [],
    capacity,
    { resumes: [], requeues: [], manualInterventions: [] },
  )

  assert.deepEqual(result.capacity, {
    queueDepth: capacity.waiters.length,
    activeLeases: capacity.activeCount,
    duplicateFences: capacity.counters.duplicate,
    staleFences: capacity.counters.stale,
    conflictingFences: capacity.counters.conflict,
  })
  assert.equal(capacity.counters.duplicate, 1)
  assert.equal('duplicateFences' in snapshot(counters), false)
  assert.equal(Object.isFrozen(result.capacity), true)
})
