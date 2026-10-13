import assert from 'node:assert/strict'
import test from 'node:test'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const journal = await import('../../../dist/Persistence/Journal/ObligationJournalSurface.js')
const forkTool = await import('../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js')
const revisionSurface = await import('../../../dist/Persistence/Journal/RevisionSurface.js')

const openIncumbencyRefusal =
  'openIncumbency no longer appends RoadOpened; this surface has no event store, observed head, or work id'
const acceptedAssessmentRefusal =
  'injectAcceptedAssessment refused: no event store, observed head, or workId; zero writes'

const assertOpenIncumbencyRefused = async (runtime, sessionId) => {
  const before = revisionSurface.revision(runtime.journal)
  const opened = await journal.openIncumbency(runtime.journal, sessionId, `inc-${sessionId}`)
  assert.equal(opened?.ok, false)
  assert.equal(opened?.error, openIncumbencyRefusal)
  assert.equal(revisionSurface.revision(runtime.journal), before, 'refused openIncumbency must not append')
}

const assertAcceptedAssessmentRefused = async (runtime, sessionId) => {
  const before = revisionSurface.revision(runtime.journal)
  await assert.rejects(forkTool.injectAcceptedAssessment(runtime, sessionId), (error) => {
    assert.equal(error?.message, acceptedAssessmentRefusal)
    return true
  })
  assert.equal(revisionSurface.revision(runtime.journal), before, 'refused injectAcceptedAssessment must not append')
}

const denied = /当前不可用|not available right now|denied-task-state/i

const call = (hooks, toolName, sessionID, args = {}) =>
  hooks.tool[toolName].execute(args, { sessionID, agent: 'manager' })

test('没有任期时 read、review、fork、resume 在工具执行入口被拒绝', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-no-tenure'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    for (const toolName of ['read', 'review', 'fork', 'resume']) {
      const result = await call(hooks, toolName, sessionID, { name: 'Ada', charge: 'look' })
      assert.match(String(result), denied)
    }
  })
})

test('有任期且评审尚未接纳时只读与观察放行，派工和 deliver 在执行入口被拒绝', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-unreviewed')
  })
})

test('评审已接纳且尚未确认时 read 与固定 DevOps 派工在执行入口放行', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-accepted')
    await assertAcceptedAssessmentRefused(runtime, 'ses-accepted')
  })
})

test('有确认且没有评审时 deliver 在执行入口被拒绝，review 不被空集拒绝', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-confirm-first')
  })
})

import { permissionOfToolName } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { manager, plan } from '../../../dist/Foundation/RolesSurface.js'

const caseName = (value) => {
  if (value == null) return null
  if (typeof value.name !== 'string') {
    throw new Error('permission 结果没有 case 名，不能把它当成 Finality 或 Deliver')
  }
  return value.name
}

const permissionName = (value) => {
  const name = caseName(value)
  if (name === 'Some') return caseName(value.fields?.[0])
  if (name === 'None') return null
  return name
}

test('按角色解析 deliver，不是按列表顺序：Manager 得到 Finality，Plan 得到 Deliver，未知名字不得当成 deliver 放行', () => {
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
