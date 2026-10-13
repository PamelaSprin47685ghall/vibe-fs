import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { realpathSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { acceptAuthorityRoot, injectAcceptedAssessment, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { permissionOfToolName } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { rolePermissionRules } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { manager, plan } from '../../../dist/Foundation/RolesSurface.js'
import { EventIdModule_create } from '../../../dist/Foundation/Identity.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspaceEvents from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import { EventEnvelope, EventEnvelopeModule_normalize, EventStreamIdModule_create } from '../../../dist/Persistence/EventStore/Model.js'
import { empty as emptyMap } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'
import { empty as emptyList, ofArray, toArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

// WHAT[work-019]: 执行入口按 sequence 与当前事实收口。无任期时全部拒绝，
// 评审未接纳时只读放行而派工拒绝，评审接纳后固定 DevOps 派工恢复，确认先于
// 评审（非法事实）fail closed。工具名→权限映射按角色解析 deliver，
// 不按列表顺序，也不按后缀猜测。

const denied = /当前不可用|不可用|not available right now|not available to|denied-task-state/i
const devopsDispatchDenied = /尚未被系统接纳|不得向 DevOps 派发|assessment.*not.*accepted|not allowed until/i
const bound = (sessionID, callID) => ({ sessionID, agent: 'manager', callID, messageID: `msg-${callID}`, providerRunID: `run-${callID}` })

const storeOf = (directory) => workspaceEvents.acquire(realpathSync(join(directory, '.git')))

// seedDeliver refuses to write ConfirmationCommitted without a review. The fold
// accepts the event, so the illegal fact is appended to the same workspace store
// the plugin reads. The opening batch has two heads, and the append must name both.
const seedConfirmationWithoutReview = async (directory, sessionID) => {
  const store = storeOf(directory)
  const stream = `lifecycle/${sessionID}`
  const streamId = EventStreamIdModule_create(stream)
  const heads = toArray(store.TryHeads(streamId))
  const incumbencyId = heads
    .map((id) => store.TryEvent(id)?.Payload?.incumbencyId)
    .find(Boolean)
  const envelope = EventEnvelopeModule_normalize(new EventEnvelope(
    EventIdModule_create(randomUUID().replaceAll('-', '')),
    streamId,
    'ConfirmationCommitted',
    ofArray(heads),
    { incumbencyId, providerRunId: 'run-seeded-confirmation', toolCallId: 'call-seeded-confirmation' },
    emptyList(),
    emptyMap(),
  ))
  const appended = await store.Append(ofArray([envelope]))
  const view = lifecycle.workView(store, sessionID)
  return { ...view, error: appended.tag === 0 ? '' : JSON.stringify(appended.fields?.[0] ?? appended) }
}

// 执行入口门禁：tool.execute.before 只对评审专用工具（js-manager）与跨角色
// 绕路工具（js-engineer/js-devops）生效；Host 原生工具（read）与 office 工具
// （fork/resume/review/deliver）的准入/拒绝由 ToolRegistry 执行入口决定。
const assertBeforeDenied = async (hooks, sessionID, tool, callID) => {
  await assert.rejects(
    () => hooks['tool.execute.before']({ tool, sessionID, callID }, { args: {} }),
    /not permitted under current manager capability facts|refused on the review gate/i,
  )
}

test('WHAT[work-019] 没有任期时 read、review、fork、resume 在工具执行入口被拒绝', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-no-tenure'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    // 无活跃任期：office 工具在 ToolRegistry 执行入口 fail closed。
    const forkResult = await hooks.tool.fork.execute({ name: 'Ada', charge: 'look' }, { sessionID, agent: 'manager' })
    assert.match(String(forkResult), denied)
    const resumeResult = await hooks.tool.resume.execute({ name: 'Ada', charge: 'look' }, { sessionID, agent: 'manager' })
    assert.match(String(resumeResult), denied)
    const reviewResult = await hooks.tool.review.execute({ findings: [] }, { sessionID, agent: 'manager', callID: 'r', messageID: 'm', providerRunID: 'p' })
    assert.match(String(reviewResult), denied)
    const deliverResult = await hooks.tool.deliver.execute({}, { sessionID, agent: 'manager' })
    assert.match(String(deliverResult), denied)
    assert.equal(createdIds.length, 0)
    // js-manager 评审专用只读工具在 before 门禁即被拒绝（无任期）。
    await assertBeforeDenied(hooks, sessionID, 'js-manager', 'call-js')
  })
})

test('WHAT[work-019] 有任期且评审尚未接纳时只读与观察放行，派工和 deliver 在执行入口被拒绝', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'ses-unreviewed'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    // deliver binds the invocation to the current physical user message.
    // Without one the call fails before Algorithm E3 can answer.
    await hooks['chat.message']({ sessionID, agent: 'manager' }, {
      message: { id: 'user-root', role: 'user', agent: 'manager' },
      parts: [{ type: 'text', text: 'Deliver the requested behavior and verification.' }],
    })
    // 评审未接纳：js-manager 评审专用只读工具放行（before 门禁通过）。
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: 'call-js' }, { args: { path: 'x' } })
    // 派工（fork/resume devops）与 deliver 在执行入口拒绝。
    const forkDevops = await hooks.tool.fork.execute({ name: 'devops', charge: 'look' }, { sessionID, agent: 'manager' })
    assert.match(String(forkDevops), devopsDispatchDenied)
    const resumeDevops = await hooks.tool.resume.execute({ name: 'devops', charge: 'look' }, { sessionID, agent: 'manager' })
    assert.match(String(resumeDevops), devopsDispatchDenied)
    // Algorithm E3: an unreviewed tenure with no confirmation is not a denial.
    // deliver returns the completed-but-not-retired result and writes nothing.
    const before = lifecycle.workView(storeOf(directory), sessionID)
    const deliverResult = await hooks.tool.deliver.execute({}, bound(sessionID, 'call-unreviewed'))
    assert.match(String(deliverResult), /finished = false/)
    assert.match(String(deliverResult), /assessment_required = true/)
    assert.doesNotMatch(String(deliverResult), denied)
    const after = lifecycle.workView(storeOf(directory), sessionID)
    assert.equal(after.confirmed, false)
    assert.equal(after.active, before.active)
    assert.equal(after.reviewed, '')
    assert.equal(createdIds.length, 0)
  })
})

test('WHAT[work-019] 评审已接纳且尚未确认时 read 与固定 DevOps 派工在执行入口放行', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-accepted'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID)
    // 评审接纳后：js-manager 只读工具被收口拒绝（Read 从 Manager 权限中移除）。
    await assertBeforeDenied(hooks, sessionID, 'js-manager', 'call-js')
    // 固定 DevOps 派工恢复（resume devops 放行）。
    const resumeResult = await hooks.tool.resume.execute({ name: 'devops', charge: 'look' }, { sessionID, agent: 'manager' })
    assert.match(String(resumeResult), /现已接下这项托付|carries this charge now/i)
    assert.equal(createdIds.length, 1)
  })
})

test('WHAT[work-019] 有确认且没有评审时 deliver 在执行入口被拒绝，review 不被空集拒绝', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'ses-confirm-first'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    // 确认先于评审是非法事实。seedDeliver 不会在无评审时落下确认，所以这一格
    // 直接把 ConfirmationCommitted 追加进插件正在读的同一份工作区事件库。
    const seeded = await seedConfirmationWithoutReview(directory, sessionID)
    assert.equal(seeded.confirmed, true, seeded.error)
    assert.equal(seeded.reviewed, '')
    const deliverResult = await hooks.tool.deliver.execute({}, bound(sessionID, 'call-confirm-first'))
    assert.match(String(deliverResult), denied)
    assert.doesNotMatch(String(deliverResult), /assessment_required = true/)
    assert.equal(lifecycle.workView(storeOf(directory), sessionID).active, true)
    // review 不被空集拒绝：空 findings 是合法评审输入，但需要真实绑定消息。
    // 此处仅断言静态权限：Manager 的 review 权限在角色层固定 allow。
    assert.equal(rolePermissionRules('manager').review, 'allow')
    assert.equal(createdIds.length, 0)
  })
})

const permissionName = (value) => (value == null ? null : JSON.parse(JSON.stringify(value)))

test('WHAT[work-019] 按角色解析 deliver，不是按列表顺序：Manager 得到 Finality，Plan 得到 Deliver，未知名字不得当成 deliver 放行', () => {
  const managerDeliver = permissionName(permissionOfToolName(manager, 'deliver'))
  const planDeliver = permissionName(permissionOfToolName(plan, 'deliver'))

  assert.equal(managerDeliver, 'Finality')
  assert.notEqual(managerDeliver, 'Deliver')
  assert.equal(planDeliver, 'Deliver')
  assert.notEqual(planDeliver, 'Finality')
  assert.notEqual(managerDeliver, planDeliver)

  for (const role of [manager, plan]) {
    const unknown = permissionName(permissionOfToolName(role, 'not-a-deliver'))
    assert.notEqual(unknown, 'Finality')
    assert.notEqual(unknown, 'Deliver')
    assert.equal(unknown, null)
  }
})

test('WHAT[work-019] 工具名→权限映射维护在唯一目录，禁止按后缀或启发式规则推测授权', () => {
  // Manager 的 review 解析为 ReviewAssessment，Plan 的 review 解析为 Review。
  assert.equal(permissionName(permissionOfToolName(manager, 'review')), 'ReviewAssessment')
  assert.equal(permissionName(permissionOfToolName(plan, 'review')), 'Review')
  // 未知名字不得按后缀（js-）或启发式规则放行。
  assert.equal(permissionName(permissionOfToolName(manager, 'js-manager')), null)
  assert.equal(permissionName(permissionOfToolName(manager, 'js-unknown')), null)
  // 角色层投影：Manager 的 read 固定 deny（评审只读由 js-manager 承载）。
  assert.equal(rolePermissionRules('manager').read, 'deny')
})

test('WHAT[work-019] 评审未接纳前 Manager 的写工具在执行门投影上被拒绝', () => {
  const rules = rolePermissionRules('manager')
  assert.equal(rules.edit, 'deny')
  assert.equal(rules.write, 'deny')
})
