import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems, orchestratorProgram } from '../support/domain.mjs'

test('ORCHESTRATOR_PROGRAM_001: trace of empty program', () => {
  const trace = listItems(orchestratorProgram.interpret(orchestratorProgram.empty))
  assert.deepEqual(trace, ['Return(None)'])
})
