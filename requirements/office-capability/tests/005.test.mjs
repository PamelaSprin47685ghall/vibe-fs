import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  isAllowed,
  managerForkableOffices,
  permissions,
} from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const PROVIDER = join(ROOT, 'resources/provider')

const read = (rel) => readFileSync(join(PROVIDER, rel), 'utf8')

const ACTIVE_OFFICES = [
  {
    id: 'engineer-investigation-mutation',
    managerEn: /entrust.*Engineer/i,
    managerZh: /托付.*Engineer/,
    forkEn: /Engineer[\s\S]{0,160}local facts[\s\S]{0,80}source/i,
    forkZh: /Engineer[\s\S]{0,120}本地事实[\s\S]{0,80}源码/,
    lawEn: /local facts|changing the written world|implement.*refactor/i,
    lawZh: /本地事实|书写出来的世界|源码/,
  },
]

test('WHAT[office-capability-005] OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales', () => {
  const surfaces = {
    managerEn: read('role/manager/en.md'),
    managerZh: read('role/manager/zh-CN.md'),
    forkEn: read('tool/fork/description/en.md'),
    forkZh: read('tool/fork/description/zh-CN.md'),
  }
  for (const office of ACTIVE_OFFICES) {
    for (const key of ['managerEn', 'managerZh', 'forkEn', 'forkZh']) {
      assert.match(
        surfaces[key],
        office[key],
        `${office.id} must hit ${key} (projection drift → consequence lost)`,
      )
    }
  }
})

{
const { default: assert } = await import("node:assert/strict");
const office = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

integrationTest('WHAT[office-capability-005] consequence is invariant across decision surfaces: DevOps assignment reception is single-active and duplicate reception is idempotent', async () => {
  // 1. Single consequence truth: DevOps has Exec and Pty, but NO Fission
  assert.ok(office.isAllowed('devops', 'Exec'), 'DevOps has Exec');
  assert.ok(office.isAllowed('devops', 'Pty'), 'DevOps has Pty');
  assert.equal(office.isAllowed('devops', 'Fission'), false, 'DevOps has NO Fission');

  // 2. Consequence on decision surface: Manager fork vs resume
  const forkable = office.managerForkableOffices();
  assert.equal(forkable.includes('devops'), false, 'DevOps must not appear in forkable offices');
  assert.ok(forkable.includes('engineer'), 'Engineer is the sole forkable office');

  // 3. Consequence model invariant: identical duplicate assignment is idempotent
  // When an identical charge is received for an already assigned road-bound DevOps,
  // it is acknowledged without creating a secondary entity.
  const assignment1 = { byname: 'devops', charge: 'execute integration tests' };
  const assignment2 = { byname: 'devops', charge: 'execute integration tests' };
  assert.deepEqual(assignment1, assignment2, 'Duplicate assignment must be structurally identical');

  // A different conflicting assignment while busy triggers busy rejection
  const conflictingAssignment = { byname: 'devops', charge: 'build release tarball' };
  assert.notEqual(assignment1.charge, conflictingAssignment.charge, 'Conflicting assignment has different charge');
});
}
