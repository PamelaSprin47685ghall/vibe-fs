import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ACTION-AFFORDANCE-011] AA_critical_role_boundaries_are_mirrored_on_caller_facing_tool_descriptions', () => {
  // 1. Fork description mirrors that Engineer is for investigation/source, and DevOps cannot be forked
  const forkEn = read('resources/provider/tool/fork/description/en.md')
  const forkZh = read('resources/provider/tool/fork/description/zh-CN.md')

  assert.match(forkEn, /Fork Engineer to establish local facts and change repository source|Engineer.*local facts.*source/is)
  assert.match(forkZh, /Engineer (?:托付本地事实调查与仓库源码工作|调查本地事实并修改源码)/i)
  assert.match(forkEn, /DevOps cannot be forked|Do not create an executor/i)
  assert.match(forkZh, /DevOps 不能 fork|不能创建执行角色/i)

  // 2. Resume description mirrors fixed DevOps road boundary and forbidden calling
  const resumeEn = read('resources/provider/tool/resume/description/en.md')
  const resumeZh = read('resources/provider/tool/resume/description/zh-CN.md')

  assert.match(resumeEn, /fixed DevOps/i)
  assert.match(resumeZh, /固定 DevOps/i)
  assert.match(resumeEn, /not calling/i)
  assert.match(resumeZh, /不传 calling/i)

  // 3. Bash-honeypot mirrors Engineer command denial and return to Manager for DevOps
  const honeypotEn = read('resources/provider/tool/bash-honeypot/description/en.md')
  const honeypotZh = read('resources/provider/tool/bash-honeypot/description/zh-CN.md')

  assert.match(honeypotEn, /Denied command entry for Engineer/i)
  assert.match(honeypotZh, /Engineer 的命令拒绝入口/i)
  assert.match(honeypotEn, /returns to the Manager for DevOps/i)
  assert.match(honeypotZh, /把事项交回 Manager，由它安排 DevOps/i)

  // Mutation test: caller description lacking mirrored boundary fails
  assert.throws(() => {
    const unmirroredFork = 'fork creates a subagent with arbitrary tools'
    if (!/Engineer/i.test(unmirroredFork) || !/DevOps cannot be forked/i.test(unmirroredFork)) {
      throw new Error('Boundary mirror missing')
    }
  }, /Boundary mirror missing/)
})
