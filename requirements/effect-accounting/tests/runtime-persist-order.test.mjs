// The fork owner persists effect identity before physical work and the job fact
// after the Manager session exists. Verify the source-owned ordering directly.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = new URL('../../../', import.meta.url).pathname
const runtime = () => readFileSync(join(ROOT, 'src/Wanxiangshu/Change/Runtime.fs'), 'utf8')
const body = () => runtime().slice(runtime().indexOf('let forkManagerCore'), runtime().indexOf('member _.ForkManager'))
const indexOf = (text, token) => {
  const index = text.indexOf(token)
  assert.notEqual(index, -1, `missing ${token}`)
  return index
}

test('WHAT[EFFECT-ACCOUNTING-003] PERSIST_009_fork_appends_worktree_request_created_then_manager_job', () => {
  const source = body()
  const requested = indexOf(source, 'OrchestratorFact.WorktreeCreateRequested')
  const created = indexOf(source, 'OrchestratorFact.WorktreeCreated')
  const manager = indexOf(source, 'OrchestratorFact.ManagerJobCreated')
  assert.ok(requested < created, 'request must be durable before physical creation result')
  assert.ok(created < manager, 'manager job must be durable after the Manager session exists')
  assert.ok(indexOf(source, 'appendFact StreamId.Workspace requestFact') < indexOf(source, 'WorktreeResource.Create'))
  assert.ok(indexOf(source, 'appendFact StreamId.Workspace createdFact') < indexOf(source, 'relay.OpenRoad'))
})
