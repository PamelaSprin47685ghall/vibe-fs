import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import plugin from '../../../dist/OpenCode/Plugin/Plugin.js'
import { createEnvironment, managedConfig, routeMessage } from './support/process-shared-routing.mjs'

const {
  createRuntime,
  acquireExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  snapshotOccupied,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.model}|${value.reasoning}`
const acquireManaged = async (runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId = null) => {
  const acquisition = await acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
  )
  if (acquisition.kind !== 'Acquired') return { kind: acquisition.kind, target: null }

  const projected = executionAdmissionTarget(runtime, acquisition.lease)
  const observed = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target: projected,
  }
  const settlement = commitExecutionAdmission(runtime, acquisition.lease, observed)
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return { kind: 'Acquired', target: projected }
}
const acquireTarget = async (...args) => {
  const outcome = await acquireManaged(...args)
  assert.equal(outcome.kind, 'Acquired')
  return outcome.target
}

test('WHAT[EMR-003] EMR_003_two_plugin_instances_share_one_process_running_multiset', async () => {
  const environment = createEnvironment(plugin.server)
  const previousHome = process.env.HOME
  process.env.HOME = environment.home
  let first
  let second

  try {
    first = await environment.createPlugin('root-workspace')
    second = await environment.createPlugin('worktree-workspace')
    await first.config(managedConfig())
    await second.config(managedConfig())

    const a = await routeMessage(first, 'ses_shared_a')
    const b = await routeMessage(second, 'ses_shared_b')

    assert.deepEqual([a.providerID, a.modelID, a.variant], ['provider', 'model-a', 'none'])
    assert.deepEqual([b.providerID, b.modelID, b.variant], ['provider', 'model-b', 'none'])
  } finally {
    if (second) await second.dispose()
    if (first) await first.dispose()
    process.env.HOME = previousHome
    environment.dispose()
  }
})

test('WHAT[EMR-003] EMR_003_each_active_physical_execution_contributes_one_running_occurrence', async () => {
  const runtime = createRuntime(() => target())

  const first = await acquireTarget(runtime, 'session-a', 'msg-a', 'coder', 'alice')
  const same = await acquireTarget(runtime, 'session-a', 'msg-a', 'coder', 'alice')
  const otherExecution = await acquireTarget(runtime, 'session-b', 'msg-b', 'coder', 'bob')

  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(same), 'provider/shared|none')
  assert.equal(key(otherExecution), 'provider/shared|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'two physical executions contribute two occurrences even on one target')
})

test('WHAT[EMR-003] process restart drops process-local capacity and rebuilds only from explicit physical observations', (context) => {
  const home = mkdtempSync(join(tmpdir(), 'wanxiangshu-capacity-restart-'))
  const config = join(home, '.config', 'opencode', 'wanxiangshu.mjs')
  mkdirSync(dirname(config), { recursive: true })
  writeFileSync(
    config,
    "export default function route(_role, running) { return running.length < 1 ? { model: 'provider/restart', reasoning: 'none' } : null }\n",
  )

  let maxRetained = 0
  try {
    for (let cycle = 0; cycle < restartCount; cycle += 1) {
      const result = runProcess(home, (seed + cycle) >>> 0)
      assert.equal(result.initial.ledgerEntries.length, 0, 'a new process cannot inherit a dead process token')
      assert.equal(result.initial.tokens.length, 0)
      assert.equal(result.initial.custodies.length, 0)
      assert.equal(result.initial.executions.length, 0)
      assert.equal(result.initial.waiters.length, 0)
      assert.equal(result.initial.owners.length, 0)
      assert.equal(result.initial.lineage.length, 0)

      assert.equal(result.observed.ledgerEntries.length, 1, 'only the new explicit admission observation reconstructs capacity')
      assert.equal(result.observed.tokens.length, 1)
      assert.equal(result.observed.executions.length, 1)
      assert.deepEqual(result.afterDuplicateReload, result.observed)
      maxRetained = Math.max(maxRetained, result.maxRetained)
    }
  } finally {
    rmSync(home, { recursive: true, force: true })
  }

  assert.ok(maxRetained <= 5, 'one live exact execution retains only ledger, token, custody, execution, and owner nodes')
  context.diagnostic(
    `task36 restart seed=${seed} restarts=${restartCount} operations=${restartCount * operationsPerRestart} maxRetained=${maxRetained}`,
  )
})

