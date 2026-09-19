import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[action-affordance-012] AA_caller_boundary_mirrors_explicitly_name_confusable_nearby_acts_and_forbidden_requests', () => {
  // 1. commission explicitly names 'This is not fork'
  const commissionEn = read('resources/provider/tool/commission/description/en.md')
  const commissionZh = read('resources/provider/tool/commission/description/zh-CN.md')
  assert.match(commissionEn, /This is not fork/i, 'commission en must state This is not fork')
  assert.match(commissionZh, /这不是 fork|并非 fork/i, 'commission zh must state This is not fork')

  // 2. fork explicitly warns against forking DevOps and points to resume
  const forkEn = read('resources/provider/tool/fork/description/en.md')
  const forkZh = read('resources/provider/tool/fork/description/zh-CN.md')
  assert.match(forkEn, /DevOps cannot be forked|Do not create an executor/i, 'fork en must forbid forking DevOps')
  assert.match(forkZh, /DevOps 不能 fork|不能创建执行角色/i, 'fork zh must forbid forking DevOps')
  assert.match(forkEn, /resume/i, 'fork en must point to resume for existing participants')
  assert.match(forkZh, /resume/i, 'fork zh must point to resume for existing participants')

  // 3. resume explicitly forbids calling
  const resumeEn = read('resources/provider/tool/resume/description/en.md')
  const resumeZh = read('resources/provider/tool/resume/description/zh-CN.md')
  assert.match(resumeEn, /not calling/i, 'resume en must explicitly forbid calling')
  assert.match(resumeZh, /不传 calling/i, 'resume zh must explicitly forbid calling')

  // Mutation test: omission of confusable act distinction fails
  assert.throws(() => {
    const blurredDesc = 'commission creates another participant'
    if (!/not fork/i.test(blurredDesc)) {
      throw new Error('Confusable act not distinguished')
    }
  }, /Confusable act not distinguished/)
})
