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
