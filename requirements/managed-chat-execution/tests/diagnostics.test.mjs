import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'

import { fold } from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import { createCounters, queryReliability } from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'

const acceptedFact = fs.readFileSync(new URL('./fixtures/chat-execution-v1.json', import.meta.url), 'utf8')

test('WHAT[CHATEXEC-013] diagnostic query derives nonterminal and physical-attempt counts from canonical projection', () => {
  const projected = fold([acceptedFact])
  assert.equal(projected.ok, true)

  const result = queryReliability(
    createCounters(),
    projected.value,
    { waiters: [], activeCount: 0, counters: { duplicate: 0, stale: 0, conflict: 0 } },
    { resumes: [], requeues: [], manualInterventions: [] },
  )

  assert.deepEqual(result.execution, {
    acceptedWithoutTerminal: 1,
    providerStartedWithoutTerminal: 0,
    physicalAttemptsByLogicalRun: [{ logicalRunId: 'run-chat-fixture', physicalAttempts: 1 }],
  })
  assert.equal(Object.isFrozen(result.execution), true)
})
