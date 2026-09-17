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

test('WHAT[OFF-015] predictor_is_internal_mechanism_role_not_forkable_or_scheduled', () => {
  const forkEn = read('tool/fork/description/en.md')
  const forkZh = read('tool/fork/description/zh-CN.md')
  assert.doesNotMatch(forkEn, /\bpredictor\b/i)
  assert.doesNotMatch(forkZh, /predictor/)
})
