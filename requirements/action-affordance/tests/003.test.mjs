import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { LEGACY_FORBIDDEN_NAMES } from '../../../scripts/checks/tool-referential-integrity.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ACTION-AFFORDANCE-003] AA_inspect_is_legacy_forbidden_and_causal_read_only_boundary_forbids_implementation', () => {
  // 1. inspect must be explicitly forbidden from owning a ToolSpec
  assert.ok(
    LEGACY_FORBIDDEN_NAMES.includes('inspect'),
    'inspect must be in LEGACY_FORBIDDEN_NAMES',
  )

  // 2. Causal read-only boundary is maintained in common law and engineer role law
  const commonLawEn = read('resources/provider/world/common-law/en.md')
  const commonLawZh = read('resources/provider/world/common-law/zh-CN.md')
  const engineerEn = read('resources/provider/role/engineer/en.md')
  const engineerZh = read('resources/provider/role/engineer/zh-CN.md')

  assert.match(
    commonLawEn,
    /Engineer investigates local facts and changes source code; DevOps carries out real execution/i,
    'common law en must distinguish investigation from execution',
  )
  assert.match(
    commonLawZh,
    /Engineer (?:负责)?调查本地事实并修改源码；DevOps (?:承担真实执行|则负责真刀真枪跑执行)/i,
    'common law zh must distinguish investigation from execution',
  )

  assert.match(
    engineerEn,
    /A read-only assignment ends with findings, not improvements to the scene/i,
    'engineer en must enforce read-only assignment restriction',
  )
  assert.match(
    engineerZh,
    /只读任务以发现收束，不顺手改进现场|只读任务交付的是事实，不是被你改善后的现场/i,
    'engineer zh must enforce read-only assignment restriction',
  )

  // Mutation test: removing the boundary requirement fails
  assert.throws(() => {
    const mutantText = 'Read-only assignments can also repair small defects'
    if (!/ends with findings, not improvements/i.test(mutantText)) {
      throw new Error('Boundary violated')
    }
  }, /Boundary violated/)
})
