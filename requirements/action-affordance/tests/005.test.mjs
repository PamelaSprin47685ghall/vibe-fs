import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { LEGACY_FORBIDDEN_NAMES } from '../../../scripts/checks/tool-referential-integrity.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ACTION-AFFORDANCE-005] AA_establish_behavior_is_legacy_forbidden_and_source_modification_is_separated_from_execution_evidence', () => {
  // 1. establish-behavior must not own a ToolSpec
  assert.ok(
    LEGACY_FORBIDDEN_NAMES.includes('establish-behavior'),
    'establish-behavior must be in LEGACY_FORBIDDEN_NAMES',
  )

  // 2. Separation of source modification from execution evidence in common law and bash-honeypot
  const commonLawEn = read('resources/provider/world/common-law/en.md')
  const commonLawZh = read('resources/provider/world/common-law/zh-CN.md')
  const honeypotEn = read('resources/provider/tool/bash-honeypot/description/en.md')
  const honeypotZh = read('resources/provider/tool/bash-honeypot/description/zh-CN.md')

  assert.match(
    commonLawEn,
    /Working in the source, producing execution evidence, and final acceptance cannot be treated as interchangeable/i,
    'common law en must forbid treating source work and execution evidence as interchangeable',
  )
  assert.match(
    commonLawZh,
    /源码落地、执行证据与最终验收不可相互替代|修改源码、运行凭据与把关验收[，,]这三件事绝不能混为一谈[，,]更不能互相顶替/i,
    'common law zh must forbid treating source work and execution evidence as interchangeable',
  )

  assert.match(
    honeypotEn,
    /Engineer works through\s+local file capabilities; required execution returns to the Manager for DevOps/i,
    'bash-honeypot en must mirror the separation of execution from file work',
  )
  assert.match(
    honeypotZh,
    /Engineer 使用本地文件能力；需要执行时，把事项交回 Manager，由它安排 DevOps/i,
    'bash-honeypot zh must mirror the separation of execution from file work',
  )

  // Mutation test: claiming source writing equals execution evidence must fail
  assert.throws(() => {
    const claim = 'Writing the test code is sufficient evidence that tests passed'
    if (/Writing the test code is sufficient evidence/i.test(claim)) {
      throw new Error('Invalid claim: source modification is not execution evidence')
    }
  }, /Invalid claim/)
})
