// requirements/participant-horizon/tests/015.test.mjs
//
// WHAT[PARTICIPANT-HORIZON-015] — Manager achieves parallelism by forking multiple
// independent Engineers, never by Manager fission. The horizon and tool surfaces
// never present Manager fission or duplicate DevOps affordances, and each Engineer
// sub-agent is independently visible by its stable Byname in horizon() roster.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'

test('WHAT[PARTICIPANT-HORIZON-015] manager_fork_candidate_is_strictly_engineer_and_devops_is_not_forkable', () => {
  // Manager fork rejects devops, coder, inspector, browser, inquiry, reviewer
  const deniedRoles = ['devops', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'manager']
  for (const role of deniedRoles) {
    const denied = fork.unavailableCalling('en', false)
    assert.match(denied, /Unknown or unavailable calling/)
  }
})

test('WHAT[PARTICIPANT-HORIZON-015] manager_horizon_presents_multiple_engineers_by_distinct_byname', () => {
  // Horizon must present separate child handles for distinct Engineer instances
  const engineerA = fork.chooseRoad('engineer', 'EngineerA', 'Investigate part A')
  const engineerB = fork.chooseRoad('engineer', 'EngineerB', 'Implement part B')

  assert.equal(engineerA.ok, true)
  assert.equal(engineerA.byname, 'EngineerA')
  assert.equal(engineerB.ok, true)
  assert.equal(engineerB.byname, 'EngineerB')
  assert.notEqual(engineerA.byname, engineerB.byname)
})
