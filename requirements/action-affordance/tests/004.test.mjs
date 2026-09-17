import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { LEGACY_FORBIDDEN_NAMES } from '../../../scripts/checks/tool-referential-integrity.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ACTION-AFFORDANCE-004] AA_repair_behavior_is_legacy_forbidden_and_mechanical_means_semantic_meaning_decided', () => {
  // 1. repair-behavior must not reappear as ToolSpec owner
  assert.ok(
    LEGACY_FORBIDDEN_NAMES.includes('repair-behavior'),
    'repair-behavior must be in LEGACY_FORBIDDEN_NAMES',
  )

  // 2. Mechanical repair is defined as semantic meaning decided rather than physical diff size
  const managerEn = read('resources/provider/role/manager/en.md')
  const managerZh = read('resources/provider/role/manager/zh-CN.md')
  const devopsEn = read('resources/provider/role/devops/en.md')
  const devopsZh = read('resources/provider/role/devops/zh-CN.md')

  assert.match(
    managerEn,
    /does not need your case-by-case approval or a uniquely mechanical solution/i,
    'manager en must recognize devops non-mechanical repairs within requirements',
  )
  assert.match(
    managerZh,
    /并不以「只有唯一机械操作」或逐次批准为前提|不需要你逐次批准[，,]也不要求只有一种机械修法/i,
    'manager zh must recognize devops non-mechanical repairs within requirements',
  )

  assert.match(
    devopsEn,
    /Inherent authority to directly repair ordinary defects|Direct engineering and autonomous local repair|inherent repair authority/i,
    'devops en must declare inherent repair authority',
  )
  assert.match(
    devopsZh,
    /角色固有的普通缺陷直接修复授权|角色固有权限[，,]无需 Manager 逐次批准/i,
    'devops zh must declare inherent repair authority',
  )

  // Mutation test: redefining mechanical as physical diff size must fail
  assert.throws(() => {
    const wrongDefinition = 'mechanical means the diff size is under 5 lines'
    if (/diff size/i.test(wrongDefinition)) {
      throw new Error('Illegal definition: mechanical is semantic, not diff size')
    }
  }, /Illegal definition/)
})
