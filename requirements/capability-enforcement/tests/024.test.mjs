import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, injectAcceptedAssessment, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { rolePermissionRules } from '../../../dist/OpenCode/Tools/ToolSurface.js'

// WHAT[capability-enforcement-024]: fork 与 resume 权能分离 — Manager fork
// 仅准入 Engineer，严禁 fork DevOps；固定 DevOps 仅允许经 resume 续做且同一
// 道路至多一个活跃工作单元。门禁在真实工具执行入口（ToolRegistry office gate）
// 与工具体内部双重生效，不依赖任何 relay 私有表面。

const devopsDispatchDenied = /尚未被系统接纳|不得向 DevOps 派发|assessment.*not.*accepted|not allowed until/i

test('WHAT[capability-enforcement-024] FORK_manager_fork_rejects_devops_calling', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-fork-devops', 'manager')
    await openIncumbency(runtime, 'ses-fork-devops')
    const result = await hooks.tool.fork.execute(
      { calling: 'devops', name: 'Op', charge: 'run tests' },
      { sessionID: 'ses-fork-devops', agent: 'manager' },
    )
    // 显式 calling=devops：Manager 只能 fork Engineer，不能 fork DevOps。
    assert.match(result, /cannot fork DevOps|fork only targets Engineer|只能 fork Engineer|不能 fork DevOps/i)
    assert.equal(createdIds.length, 0)
  })
})

test('WHAT[capability-enforcement-024] FORK_devops_dispatch_denied_until_review_accepted', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-fork-pre'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    // 评审未接纳前：fork name=devops 与 resume devops 都在执行入口拒绝，
    // 零子会话、零 prompt（WHAT[026]：拦截发生在任何持久化副作用之前）。
    const forkDevops = await hooks.tool.fork.execute(
      { name: 'devops', charge: 'run tests' },
      { sessionID, agent: 'manager' },
    )
    assert.match(forkDevops, devopsDispatchDenied)
    assert.equal(createdIds.length, 0)

    const resumeDevops = await hooks.tool.resume.execute(
      { name: 'devops', charge: 'run tests' },
      { sessionID, agent: 'manager' },
    )
    assert.match(resumeDevops, devopsDispatchDenied)
    assert.equal(createdIds.length, 0)

    // 评审接纳后，固定 DevOps 经 resume 正常派工（同一道路一个活跃单元）。
    await injectAcceptedAssessment(runtime, sessionID)
    const admitted = await hooks.tool.resume.execute(
      { name: 'devops', charge: 'run tests' },
      { sessionID, agent: 'manager' },
    )
    assert.match(admitted, /现已接下这项托付|carries this charge now/i)
    assert.equal(createdIds.length, 1)
  })
})

test('WHAT[capability-enforcement-024] FORK_manager_fork_engineer_admitted_after_review', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-fork-eng'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID)

    const result = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'Op', charge: 'run tests' },
      { sessionID, agent: 'manager' },
    )
    assert.match(result, /现已接下这项托付|carries this charge now/i)
    assert.equal(createdIds.length, 1)
  })
})

// 静态门禁：fork/resume 在 Manager 角色层固定 allow，Plan 角色 deny；
// js-devops 对 Manager 的跨角色绕路调用在角色层 deny。
test('WHAT[capability-enforcement-024] role_permission_rules_fork_separation', () => {
  const managerRules = rolePermissionRules('manager')
  assert.equal(managerRules.fork, 'allow')
  assert.equal(managerRules.resume, 'allow')
  assert.equal(managerRules['js-devops'], 'deny')
  const planRules = rolePermissionRules('plan')
  assert.equal(planRules.fork, 'deny')
  assert.equal(planRules.resume, 'deny')
})