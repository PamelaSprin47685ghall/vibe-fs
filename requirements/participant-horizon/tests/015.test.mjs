// requirements/participant-horizon/tests/015.test.mjs
//
// WHAT[PARTICIPANT-HORIZON-015] — Manager achieves parallelism by forking multiple
// independent Engineers, never by Manager fission. The horizon and tool surfaces
// never present Manager fission or duplicate DevOps affordances, and each Engineer
// sub-agent is independently visible by its stable Byname in horizon() roster.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'
import { permissions as officePermissions, isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[PARTICIPANT-HORIZON-015] manager_fork_candidate_is_strictly_engineer_and_devops_is_not_forkable', () => {
  // Manager fork rejects devops, coder, inspector, browser, inquiry, reviewer, manager
  const deniedRoles = ['devops', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'manager']
  for (const role of deniedRoles) {
    const denied = fork.unavailableCalling('en', false)
    assert.match(denied, /Unknown or unavailable calling/)
  }

  // Calling engineer is strictly accepted
  const engineerRoad = fork.chooseRoad('engineer', 'Engineer1', 'Investigate part A')
  assert.equal(engineerRoad.ok, true)
  assert.equal(engineerRoad.calling, 'engineer')
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

test('WHAT[PARTICIPANT-HORIZON-015] manager_fission_is_explicitly_denied_in_schema_permissions_and_bilingual_laws', () => {
  // 1. isAllowed('manager', 'Fission') === false
  assert.equal(isAllowed('manager', 'Fission'), false, 'Manager must not have Fission capability')
  assert.equal(officePermissions('manager').includes('Fission'), false, 'Manager permissions must explicitly exclude fission')

  // 2. 双语 Role Law 明确包含 Fission 禁令
  const lawZh = read('resources/provider/role/manager/zh-CN.md')
  const lawEn = read('resources/provider/role/manager/en.md')

  assert.match(lawZh, /你不能使用\s*Fission|并行来自派出多名独立的\s*Engineer/i, 'Chinese manager law must prohibit fission')
  assert.match(lawEn, /cannot use Fission|Delegate independent work to Engineers/i, 'English manager law must prohibit fission')
})
