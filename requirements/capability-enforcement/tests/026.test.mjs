import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const forkTool = await import('../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js')
const toolModule = await import('@opencode-ai/plugin/tool')

const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

test('WHAT[capability-enforcement-026] D01_resume_fixed_devops_rejected_before_review_accepted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d01-'))
  const owner = 'manager-devops-d01'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-RUN-TESTS-BEFORE-REVIEW',
    )
    const result = await resumed
    // WHAT[capability-enforcement-026]: 在当前迭代的 Review 被系统接纳前，严禁 Manager 向绑定的固定 DevOps 派发任何任务。
    // 依据 harness 暴露的计数与状态观察能力断言拒绝效果（零副作用、未派工、未发 prompt、非承接）：
    assert.equal(typeof result, 'string', 'Resume must return structured consequence')
    assert.doesNotMatch(
      result,
      /carries this charge now|现已接下这项托付/i,
      'Resume response must not establish successful handover before review is accepted',
    )
    assert.equal(forkTool.childCount(runtime), 0, 'No child session may be created when resume is rejected')
    assert.equal(forkTool.promptCount(runtime), 0, 'No prompt may be emitted when resume is rejected')
    assert.equal(forkTool.child(runtime), null, 'No child handle may exist when resume is rejected')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[capability-enforcement-026] D03_resume_devops_enters_normal_flow_after_review_accepted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d03-'))
  const owner = 'manager-devops-d03'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 注入已接纳评审事实（AcceptedAssessment）
    await forkTool.injectAcceptedAssessment(runtime, owner)

    setTimeout(() => {
      forkTool.acceptPrompt(runtime, 0)
    }, 50)

    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-RUN-TESTS-AFTER-REVIEW',
    )
    const result = await resumed
    // 接纳评审后，Manager 恢复向固定 DevOps 派工的正常流程，交接成功
    assert.match(result, /devops/)
    assert.match(result, /carries this charge now|现已接下这项托付/i)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[capability-enforcement-026] D02_resume_legitimate_existing_readonly_engineer_unaffected_before_review_acceptance', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-eng-d02-'))
  const owner = 'manager-eng-d02'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 1. Manager 先派出合法 Engineer 子会话进行调查
    const forked = forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'engineer',
      'Ada',
      'INVESTIGATE-INITIAL-CODEBASE',
    )
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    await forked

    // 完成第一轮调查并结算
    assert.equal(await forkTool.settle(runtime, owner, 'ADA-FIRST-SETTLED', 'run-ada-1'), true)

    // 2. 在 Review 接纳前，Manager resume 已有的合法只读 Engineer（Ada）
    // 该 resume 路径面向 Engineer，绝不应被 DevOps 的未评审门禁误伤
    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'Ada',
      'CONTINUE-INVESTIGATION-BEFORE-REVIEW',
    )
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    const result = await resumed
    assert.match(result, /Ada/)
    assert.match(result, /carries this charge now|现已接下这项托付/i)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[capability-enforcement-026] D04_resume_devops_when_busy_rejected_under_existing_rules', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d04-'))
  const owner = 'manager-devops-d04'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 1. 接纳评审，使 Manager 具备向绑定的固定 DevOps 派工的资格
    await forkTool.injectAcceptedAssessment(runtime, owner)

    // 2. 参照 delegation 003 / 024 成功模式：发起第一次 DevOps 派工，基于事件驱动等待首条 prompt 发射
    const firstResumePromise = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-RUNNING-INITIAL-VERIFICATION',
    )
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true, 'First prompt (index 0) must be accepted cleanly')

    const firstResume = await firstResumePromise
    assert.match(firstResume, /devops/)
    assert.match(firstResume, /carries this charge now|现已接下这项托付/i)

    // 3. 观测 DevOps 状态已确立为活跃且处于 in-flight 派发状态（进入 PendingRuns）
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'devops'), 'Active')
    assert.equal(forkTool.promptCount(runtime), 1)

    // 4. DevOps 正在忙碌执行中（尚未 settle），此时 Manager 发起新的不同 charge 派工：
    // 底层 handleExistingDevOps 识别到 isBusy && !isDuplicate，通过 fromResult 同步返回 PersonCannotTakeCharge 拒绝
    const secondResume = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-ANOTHER-TASK-WHILE-BUSY',
    )
    assert.match(
      secondResume,
      /cannot take another charge|尚不能再接下另一项托付|cannot take charge|busy|无法承担新的差事/i,
      'Busy DevOps must be rejected when receiving a different charge',
    )
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[capability-enforcement-026] D05_case_variation_and_unbound_identity_cannot_bypass_devops_gate', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d05-'))
  const owner = 'manager-devops-d05'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 评审未接纳状态下，通过大小写变形或带空格尝试绕过 DevOps 门禁
    const variations = ['DEVops', 'DevOps ', ' devops ', 'DEVOPS']
    for (const name of variations) {
      const res = await forkTool.executeManagerResume(
        runtime,
        toolModule,
        owner,
        '',
        name,
        'BYPASS-ATTEMPT',
      )
      // 真实归属识别为 devops，在 review 接纳前均被严格拒绝，零副作用
      assert.doesNotMatch(res, /carries this charge now|现已接下这项托付/i)
      assert.equal(forkTool.childCount(runtime), 0, `No child session may be created for bypass attempt '${name}'`)
    }

    // 尝试传入非本路绑定的未知身份
    const unbound = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'stranger-operator',
      'UNBOUND-ATTEMPT',
    )
    assert.match(unbound, /没有以该 name|不为人知|unknown|person-unknown|No continuing person is known by that name/i)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[capability-enforcement-026] D06_fork_devops_or_creating_alternative_devops_remains_strictly_denied', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d06-'))
  const owner = 'manager-devops-d06'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 1. Manager 试图通过 calling: 'devops' 进行 fork：同步拒绝 unknown-calling（只能 fork Engineer）
    const resCalling = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'devops',
      'Operator1',
      'FORK-DEVOPS-ATTEMPT',
    )
    assert.match(resCalling, /unknown-calling|only targets Engineer|只能 fork Engineer|未结识的 calling/i)
    assert.equal(forkTool.childCount(runtime), 0, 'No child may be placed for invalid calling')

    // 2. 接纳评审并绑定固定 DevOps，使其名字属于当前持续历史
    await forkTool.injectAcceptedAssessment(runtime, owner)
    forkTool.awaitPromptCount(runtime, 1).then(() => {
      forkTool.acceptPrompt(runtime, 0)
    })
    const bound = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'BIND-DEVOPS-FIRST',
    )
    assert.match(bound, /devops/)

    // 3. Manager 试图通过 name: 'devops' 创建替代 devops：同步拒绝 name-already-belongs
    const resName = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'engineer',
      'devops',
      'REPLACE-DEVOPS-ATTEMPT',
    )
    assert.match(
      resName,
      /name-already-belongs|已属于|already belongs/i,
      'Forking an engineer with existing devops byname must be rejected synchronously',
    )
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
