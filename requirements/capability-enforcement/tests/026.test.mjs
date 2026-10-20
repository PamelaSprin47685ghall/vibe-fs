import assert from 'node:assert/strict'
import test from 'node:test'
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { acceptAuthorityRoot, injectAcceptedAssessment, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspaceEvents from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

// WHAT[capability-enforcement-026]: 在当前迭代的 Review 被系统接纳前，严禁
// Manager 向绑定的固定 DevOps 派发任何任务；已有只读 Engineer 的合法 resume
// 与 fork 只读调查不受误伤。对未接纳评审前向 DevOps 派工的拦截与拒绝，必须
// 发生在任何持久化副作用之前。门禁在真实工具执行入口（ToolRegistry office
// gate + ForkTool 工具体）生效，状态来自统一 Lifecycle EventStore。
//
// 委派动词不再是宿主注册的工具：`fork` / `resume` 是 Manager 编程面的成员，
// 由 `todowrite-for-manager` 的程序调用（`this.fork(payload, reportMapping)`、
// `this.resume(payload, reportMapping)`）。成员返回数据信封，不渲染散文：成功
// 是 `{ ok: true, name, agentId, outcome }`，拒绝是 `{ ok: false, code }`。
// 拒绝码是稳定的 kebab 码，本文件按码断言，不再匹配散文。

// 稳定拒绝码（数据信封的 `code` 字段）。
const REVIEW_PENDING = 'assessment-pending-for-devops'
const DISPATCH_CLOSED = 'dispatch-closed'
const UNKNOWN_CALLING = 'unknown-calling'
const CALLING_CONFLICT = 'calling-conflict'
const PERSON_UNKNOWN = 'person-unknown'

// 派工门读插件正在读的同一份工作区事件库，与 fixture 的 lifecycleStore 同源。
const storeOf = async (directory) => workspaceEvents.acquire(await journalSurface.JournalSurface_initializeStateRoot(directory))

// 编程面成员的唯一入口：Manager 的 `todowrite-for-manager` 工具执行一段程序，
// 程序内调用 `this.fork` / `this.resume`。返回渲染后的 TOML 字符串。
const runManagerProgram = async (hooks, sessionID, body) =>
  await hooks.tool['todowrite-for-manager'].execute(
    { program: `class Js extends JsProgram { async run() { ${body} } }` },
    { sessionID, agent: 'manager' },
  )

// 载荷与 reportMapping（Record<string, null|string>，无 prefixUntil）。
const forkCall = (payload) => `return await this.fork(${JSON.stringify(payload)}, { 1: null });`
const resumeCall = (payload) => `return await this.resume(${JSON.stringify(payload)}, { 1: null });`

// 数据信封字段（渲染后的 TOML `[data]` 表）。
const rendered = (result) => String(result)
const envelopeString = (result, field) => {
  const match = new RegExp(`^${field} = "(.*)"$`, 'm').exec(rendered(result))
  return match === null ? null : match[1]
}
const envelopeOk = (result) => /^ok = true$/m.test(rendered(result))
const refusalCode = (result) => envelopeString(result, 'code')
const outcomeOf = (result) => envelopeString(result, 'outcome')

test('WHAT[capability-enforcement-026] D01_resume_fixed_devops_rejected_before_review_accepted', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'ses-d01'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    const result = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'DEVOPS-RUN-TESTS-BEFORE-REVIEW' }),
    )
    assert.equal(typeof result, 'string', 'Resume must return structured consequence')
    assert.equal(refusalCode(result), REVIEW_PENDING,
      'the review gate must refuse the DevOps dispatch before the review is accepted')
    assert.equal(envelopeOk(result), false,
      'the refusal must not report a successful handover before the review is accepted')
    assert.equal(createdIds.length, 0, 'No child session may be created when resume is rejected')
    assert.equal(runtime.prompts.length, 0, 'No prompt may be emitted when resume is rejected')
  })
})

test('WHAT[capability-enforcement-026] D03_resume_devops_enters_normal_flow_after_review_accepted', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d03'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID)

    const result = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'DEVOPS-RUN-TESTS-AFTER-REVIEW' }),
    )
    assert.equal(envelopeOk(result), true, result)
    assert.equal(envelopeString(result, 'name'), 'devops', result)
    assert.equal(outcomeOf(result), 'created', result)
    assert.match(rendered(result), /^agentId = ".+"$/m, result)
    assert.equal(createdIds.length, 1)
  })
})

test('WHAT[capability-enforcement-026] D02_resume_legitimate_existing_readonly_engineer_unaffected_before_review_acceptance', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d02'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    // 已有只读 Engineer 的合法 resume 不受评审未接纳误伤：先 fork Engineer
    // 建立持续之人，再 resume 追加指导，两者都在评审接纳前放行。
    const forked = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ name: 'Bob', charge: 'readonly investigation' }),
    )
    assert.equal(envelopeOk(forked), true, forked)
    assert.equal(envelopeString(forked, 'name'), 'Bob', forked)
    assert.equal(outcomeOf(forked), 'created', forked)
    assert.equal(createdIds.length, 1)

    const resumed = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'Bob', charge: 'continue readonly investigation' }),
    )
    assert.equal(refusalCode(resumed), null,
      'the Engineer resume must not be refused by the DevOps review gate')
    assert.equal(envelopeOk(resumed), true, resumed)
    assert.equal(outcomeOf(resumed), 'nudged',
      'the charge of a busy Engineer becomes guidance appended to its existing work')
    assert.equal(createdIds.length, 1, 'resume of an existing Engineer must not create a new child')
  })
})

test('WHAT[capability-enforcement-026] D04_review_authorized_busy_devops_receives_guidance_on_the_original_work', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d04'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID)

    // 第一次派工建立活跃 DevOps 工作单元。
    const first = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'ORIGINAL-WORK' }),
    )
    assert.equal(envelopeOk(first), true, first)
    assert.equal(outcomeOf(first), 'created', first)
    assert.equal(createdIds.length, 1)

    // DevOps 忙时再次 resume：不建立新任务，而是向既有任务追加指导。
    const second = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'NEW-ASSIGNMENT' }),
    )
    assert.equal(envelopeOk(second), true, second)
    assert.equal(outcomeOf(second), 'nudged',
      'the busy DevOps charge becomes guidance on the original work, not a new task')
    assert.equal(createdIds.length, 1, 'busy DevOps must not create a second work unit on the same road')
  })
})

test('WHAT[capability-enforcement-026] D05_case_variation_and_unbound_identity_cannot_bypass_devops_gate', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d05'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    // 大小写变体（DevOps / DEV-OPS 等）不得绕过固定 DevOps 门禁。
    for (const variant of ['DevOps', 'DEV-OPS', 'dev-ops']) {
      const result = await runManagerProgram(
        hooks,
        sessionID,
        resumeCall({ name: variant, charge: 'RUN' }),
      )
      // 未结识的 name 一律 fail closed：不得建立任何 DevOps 派工。
      assert.equal(envelopeOk(result), false,
        `variant '${variant}' must not establish a DevOps handover`)
      // 'DevOps' 仍被固定 DevOps 的评审门认出；其余写法是不结识的名字。
      const expected = variant === 'DevOps' ? REVIEW_PENDING : PERSON_UNKNOWN
      assert.equal(refusalCode(result), expected, `variant '${variant}' refusal code`)
      assert.equal(createdIds.length, 0)
    }
  })
})

test('WHAT[capability-enforcement-026] D06_fork_devops_or_creating_alternative_devops_remains_strictly_denied', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d06'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    // fork devops（评审未接纳）在执行入口拒绝。
    const forkDevopsBeforeReview = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ name: 'devops', charge: 'FORK-DEVOPS-ATTEMPT' }),
    )
    assert.equal(refusalCode(forkDevopsBeforeReview), REVIEW_PENDING,
      'the review gate must refuse a DevOps fork before the review is accepted')
    assert.equal(createdIds.length, 0, 'No child may be placed for invalid calling')

    // 评审接纳后，Manager 依然不能 fork DevOps：派生的 calling 不可 fork。
    await injectAcceptedAssessment(runtime, sessionID)
    const forkDevops = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ name: 'devops', charge: 'FORK-DEVOPS-ATTEMPT' }),
    )
    assert.equal(refusalCode(forkDevops), UNKNOWN_CALLING,
      'the DevOps calling derived from the name is not forkable')
    assert.equal(createdIds.length, 0, 'No child may be placed for invalid calling')

    // 显式 calling=devops 也严格拒绝。
    const callingDevops = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ calling: 'devops', name: 'Operator1', charge: 'FORK-DEVOPS-ATTEMPT' }),
    )
    assert.equal(refusalCode(callingDevops), CALLING_CONFLICT,
      'the explicit calling conflicts with the calling derived from the name')
    assert.equal(createdIds.length, 0)
  })
})

test('WHAT[capability-enforcement-026] D07_pre_review_devops_refusal_preserves_durable_state_and_accepted_review_admits_the_same_charge', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'ses-d07'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    const durableBefore = durableSnapshot(directory)

    // 评审接纳前向 DevOps 派工：拒绝且不产生任何持久化副作用。
    const refused = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'DEVOPS-RUN-TESTS-BEFORE-REVIEW' }),
    )
    assert.equal(refusalCode(refused), REVIEW_PENDING)
    assert.deepEqual(durableSnapshot(directory), durableBefore,
      'the refused dispatch must not append any durable bytes')
    assert.equal(createdIds.length, 0)
    assert.equal(runtime.prompts.length, 0)

    // 评审接纳后，同一个 charge 正常派工。
    await injectAcceptedAssessment(runtime, sessionID)
    const admitted = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'DEVOPS-RUN-TESTS-BEFORE-REVIEW' }),
    )
    assert.equal(envelopeOk(admitted), true, admitted)
    assert.equal(outcomeOf(admitted), 'created', admitted)
    assert.equal(createdIds.length, 1)
  })
})

// 修复窗（已评 Stayed、尚无确认）是本门唯一放行的格子：修复派工与修复 fork
// 都不得被本门末尾拒绝，因为修复义务的派工本来就落在这里。
test('WHAT[capability-enforcement-026] D08_repair_window_admits_devops_dispatch_and_repair_fork', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d08-repair-window'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID) // Stayed，尚无确认

    const dispatch = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'REPAIR-THE-FINDINGS' }),
    )
    assert.equal(envelopeOk(dispatch), true, dispatch)
    assert.equal(outcomeOf(dispatch), 'created', dispatch)
    assert.equal(refusalCode(dispatch), null,
      'the repair window must still admit DevOps dispatch')
    assert.equal(createdIds.length, 1)

    const repairFork = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ name: 'Carl', charge: 'repair the findings' }),
    )
    assert.equal(envelopeOk(repairFork), true, repairFork)
    assert.equal(outcomeOf(repairFork), 'created',
      'a repair fork must not be refused for not being read-only')
    assert.equal(createdIds.length, 2)
  })
})

// 收口窗一：确认已经落盘（Stayed 两段式的第二笔只差收口）。此时再向绑定的固定
// DevOps 派新任务必须拒绝，且拒绝发生在任何持久化副作用之前。
test('WHAT[capability-enforcement-026] D09_confirmed_tenure_refuses_new_devops_dispatch_with_zero_side_effects', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'ses-d09-confirmed'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)
    await injectAcceptedAssessment(runtime, sessionID) // Stayed

    const store = await storeOf(directory)
    const confirmed = await lifecycle.seedDeliver(store, sessionID, 'run-d09', 'call-d09-first', 2n)
    assert.equal(confirmed.ok, true, confirmed.error)
    const view = lifecycle.workView(store, sessionID)
    assert.equal(view.confirmed, true, 'the plugin must read the same confirmation the gate reads')
    assert.equal(view.active, true)

    const createdBefore = createdIds.length
    const promptsBefore = runtime.prompts.length
    const durableBefore = durableSnapshot(directory)

    const refused = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'NEW-TASK-AFTER-CONFIRMATION' }),
    )
    assert.equal(refusalCode(refused), DISPATCH_CLOSED,
      'the close-out window must refuse new DevOps dispatch')
    assert.equal(envelopeOk(refused), false,
      'no handover may be reported in the close-out window')
    assert.equal(createdIds.length, createdBefore, 'the refused close-out dispatch must not place a child')
    assert.equal(runtime.prompts.length, promptsBefore, 'the refused close-out dispatch must not emit a prompt')
    assert.deepEqual(durableSnapshot(directory), durableBefore,
      'the refused close-out dispatch must not append any durable bytes')
  })
})

// 收口窗二：已评 Promoted。新派工不因为已有评审被放行；同时评审接纳前已有的
// 只读 Engineer fork/resume 不被本门误伤。
test('WHAT[capability-enforcement-026] D10_promoted_review_refuses_new_devops_dispatch_and_spares_readonly_engineer', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses-d10-promoted'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    const forked = await runManagerProgram(
      hooks,
      sessionID,
      forkCall({ name: 'Dora', charge: 'readonly investigation' }),
    )
    assert.equal(envelopeOk(forked), true, forked)
    assert.equal(outcomeOf(forked), 'created', forked)
    assert.equal(createdIds.length, 1)

    const resumed = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'Dora', charge: 'continue readonly investigation' }),
    )
    assert.equal(refusalCode(resumed), null,
      'the Engineer resume must not be refused by the DevOps review gate')
    assert.equal(envelopeOk(resumed), true, resumed)
    assert.equal(outcomeOf(resumed), 'nudged',
      'the charge of a busy Engineer becomes guidance appended to its existing work')
    assert.equal(createdIds.length, 1, 'resume of an existing Engineer must not create a new child')

    await injectAcceptedAssessment(runtime, sessionID, `inc-${sessionID}`, 'Promoted')
    const promptsBefore = runtime.prompts.length
    const refused = await runManagerProgram(
      hooks,
      sessionID,
      resumeCall({ name: 'devops', charge: 'NEW-TASK-PROMOTED' }),
    )
    assert.equal(refusalCode(refused), DISPATCH_CLOSED,
      'a Promoted review must not admit a new DevOps task')
    assert.equal(envelopeOk(refused), false,
      'a Promoted review must not report a DevOps handover')
    assert.equal(createdIds.length, 1)
    assert.equal(runtime.prompts.length, promptsBefore)
  })
})

const durableSnapshot = (directory) => {
  const snapshot = []
  const visit = (relativePath) => {
    const entries = readdirSync(join(directory, relativePath), { withFileTypes: true })
      .sort((left, right) => left.name.localeCompare(right.name))
    for (const entry of entries) {
      const path = join(relativePath, entry.name)
      if (entry.isDirectory()) {
        snapshot.push({ path, kind: 'directory' })
        visit(path)
      } else {
        assert.equal(entry.isFile(), true, 'the isolated durable boundary must contain only directories and regular files')
        snapshot.push({ path, kind: 'file', bytes: readFileSync(join(directory, path)).toString('base64') })
      }
    }
  }
  visit('')
  return snapshot
}
