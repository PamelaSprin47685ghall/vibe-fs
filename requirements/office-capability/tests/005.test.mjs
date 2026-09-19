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
