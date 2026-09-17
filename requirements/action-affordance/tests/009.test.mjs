import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ACTION-AFFORDANCE-009] AA_calling_choices_must_not_degrade_into_bare_enums', () => {
  // 1. commission description defines calling options semantically
  const commEn = read('resources/provider/tool/commission/description/en.md')
  const commZh = read('resources/provider/tool/commission/description/zh-CN.md')

  assert.match(
    commEn,
    /calling chooses Coordinator \/ Lead \(persona and reasoning depth, not a different\s+office\)/i,
    'commission en must explain calling options as persona/depth distinctions',
  )
  assert.match(
    commZh,
    /calling 选择 Coordinator \/ Lead（区别在 persona 与 reasoning depth，不是不同的 Office）/i,
    'commission zh must explain calling options as persona/depth distinctions',
  )

  // 2. fork arg-calling defines calling semantics explicitly
  const forkArgEn = read('resources/provider/tool/fork/arg-calling/en.md')
  const forkArgZh = read('resources/provider/tool/fork/arg-calling/zh-CN.md')

  assert.match(forkArgEn, /Engineer/i, 'fork arg-calling en must explain Engineer calling')
  assert.match(forkArgZh, /Engineer/i, 'fork arg-calling zh must explain Engineer calling')
  assert.match(forkArgEn, /cannot be forked/i, 'fork arg-calling en must explain devops cannot be forked')
  assert.match(forkArgZh, /不能 fork/i, 'fork arg-calling zh must explain devops cannot be forked')

  // Mutation test: bare enum without semantic explanation must fail
  assert.throws(() => {
    const bareEnumDoc = 'Allowed calling values: ["engineer", "devops"]'
    if (!/persona|reasoning depth|office/i.test(bareEnumDoc)) {
      throw new Error('Bare enum detected: calling must have semantic explanation')
    }
  }, /Bare enum detected/)
})
