import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const {
  createRuntime,
  acquireExecutionAdmission,
  beginExecutionAdmission,
  awaitQueuedExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  tryReserveManaged,
  tryLease,
  releasePhysicalExecution,
  cancelPendingExecution,
  enterProviderStep,
  endProviderStep,
  takeProviderRunTarget,
  suppressProviderStep,
  snapshotOccupied,
  capacitySnapshot,
  pendingCount,
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
const provider = (model) => model.slice(0, model.indexOf('/'))
const providerLimited = (limits, routes) => (role, running, previous) => {
  const candidates = routes[role] ?? []
  const count = (name) => running.filter((item) => provider(item.model) === name).length
  const available = (candidate) => count(provider(candidate.model)) < (limits[provider(candidate.model)] ?? 0)
  if (previous && candidates.some((candidate) => key(candidate) === key(previous)) && available(previous)) return previous
  return candidates.find(available) ?? null
}

test('WHAT[execution-model-routing-002] EMR_002_scheduler_program_error_poisons_pending_and_future_demands', async () => {
  const runtime = createRuntime((role) => {
    if (role === 'devops') return null
    throw new Error('bad scheduler program')
  })

  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiting', 'devops', 'alice')
  const waitingRejected = assert.rejects(waiting, /bad scheduler program/)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  await assert.rejects(acquireTarget(runtime, 'boom', 'msg-boom', 'manager', 'bob'), /bad scheduler program/)
  await waitingRejected
  await assert.rejects(acquireTarget(runtime, 'later', 'msg-later', 'devops', 'carol'), /bad scheduler program/)
  assert.equal(pendingCount(runtime), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtemp, readFile, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const { bootstrapAndLoadAt, invokeScheduler } = routing
const template = `export const routingProtocol = 2
export const hasTheoreticalCapacity = () => true
export default function route(role, running) {
  if (role !== 'coder') return null
  return running.length === 0
    ? { model: 'provider/coder-model', reasoning: 'none' }
    : null
}\n`
const withTemp = async (run) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-routing-'))
  try {
    await run(join(root, 'nested', 'wanxiangshu.mjs'))
  } finally {
    await rm(root, { recursive: true, force: true })
  }
}

test('WHAT[execution-model-routing-002] EMR_002_scheduler_preserves_running_duplicates_null_and_previous', async () => {
  await withTemp(async (path) => {
    const body = `export const routingProtocol = 2
export const hasTheoreticalCapacity = () => true
export default function route(role, running, previous) {
      if (running.length !== 2) throw new Error('duplicates lost')
      if (running[0].model !== running[1].model) throw new Error('unexpected running')
      if (role === 'new' && previous !== null) throw new Error('new conversation previous must be null')
      if (role === 'continued' && (previous?.model !== 'provider/previous' || previous?.reasoning !== 'high')) {
        throw new Error('previous target lost')
      }
      return previous
    }\n`
    const scheduler = await bootstrapAndLoadAt(path, body)
    const running = [
      { model: 'provider/shared', reasoning: 'low' },
      { model: 'provider/shared', reasoning: 'low' },
    ]
    assert.equal(invokeScheduler(scheduler, 'new', running, null), null)
    assert.deepEqual(
      invokeScheduler(scheduler, 'continued', running, { model: 'provider/previous', reasoning: 'high' }),
      { model: 'provider/previous', reasoning: 'high' },
    )
  })
})
test('WHAT[execution-model-routing-002] EMR_002_scheduler_program_errors_fail_closed', async () => {
  await withTemp(async (path) => {
    const invalidDefault = `export default 42\n`
    await assert.rejects(() => bootstrapAndLoadAt(path, invalidDefault), /default export.*function/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(
      path,
      `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport default async () => ({ model: 'provider/x', reasoning: 'none' })\n`,
    )
    assert.throws(() => invokeScheduler(scheduler, 'coder', []), /Promise|synchronous/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(
      path,
      `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport default () => ({ model: 'bare-model', reasoning: 'none' })\n`,
    )
    assert.throws(() => invokeScheduler(scheduler, 'coder', []), /provider\/model/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(
      path,
      `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport default () => ({ model: 'provider/model', reasoning: '' })\n`,
    )
    assert.throws(() => invokeScheduler(scheduler, 'coder', []), /reasoning/i)
  })
})
}
{
const { default: assert } = await import("node:assert/strict");
const { mkdir, mkdtemp, readFile, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const {
  bootstrapAndLoadAt,
  invokeScheduler,
  createRuntime,
  acquireExecutionAdmission,
  beginExecutionAdmission,
  awaitQueuedExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  releasePhysicalExecution,
  enterProviderStep,
  tryLease,
} = routing

const withTemp = async (run) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-routing-purpose-'))
  try {
    await run(join(root, 'nested', 'wanxiangshu.mjs'))
  } finally {
    await rm(root, { recursive: true, force: true })
  }
}

// The loader creates the nested directory itself, but a test that pre-writes a
// file before bootstrapping must create it first.
const writeConfig = async (path, body) => {
  await mkdir(join(path, '..'), { recursive: true })
  await writeFile(path, body, 'utf8')
}

const observingScheduler = (behaviour) => {
  const calls = []
  const scheduler = (role, running, previous, purpose) => {
    calls.push({ role, running, previous: previous ?? null, purpose })
    return behaviour(role, running, previous, purpose)
  }
  return { scheduler, calls }
}

const acquire = async (runtime, sessionId, physicalUserMessageId, role, participant, purpose, lenderSessionId = null) => {
  const acquisition = await acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
    purpose,
  )
  if (acquisition.kind !== 'Acquired') return { kind: acquisition.kind, target: null }

  const projected = executionAdmissionTarget(runtime, acquisition.lease)
  const settlement = commitExecutionAdmission(runtime, acquisition.lease, {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target: projected,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return { kind: 'Acquired', target: projected }
}

test('WHAT[execution-model-routing-002] EMR_002_loader_rejects_pre_protocol_two_configuration_without_touching_it', async () => {
  await withTemp(async (path) => {
    // A three-parameter JS function silently ignores the extra argument, so a
    // quiet call proves nothing: the loader must require the explicit marker.
    const old = `export default function route(role, running) {
  return { model: 'provider/old', reasoning: 'none' }
}
`
    await writeConfig(path, old)
    await assert.rejects(() => bootstrapAndLoadAt(path, old), /routingProtocol/)
    assert.equal(await readFile(path, 'utf8'), old, 'an existing user configuration is never overwritten')
  })

  await withTemp(async (path) => {
    const wrongVersion = `export const routingProtocol = 1
export default function route(role, running, previous, purpose) {
  return { model: 'provider/old', reasoning: 'none' }
}
`
    await writeConfig(path, wrongVersion)
    await assert.rejects(() => bootstrapAndLoadAt(path, wrongVersion), /routingProtocol = 1/)
  })

  await withTemp(async (path) => {
    const notANumber = `export const routingProtocol = '2'
export default function route(role, running, previous, purpose) {
  return { model: 'provider/old', reasoning: 'none' }
}
`
    await writeConfig(path, notANumber)
    await assert.rejects(() => bootstrapAndLoadAt(path, notANumber), /routingProtocol must be the number 2/)
  })
})

test('WHAT[execution-model-routing-002] EMR_002_invoke_scheduler_passes_role_running_and_previous', async () => {
  const { scheduler, calls } = observingScheduler((role, running, previous) =>
    ({ model: 'provider/owner', reasoning: 'none' }),
  )
  const observed = invokeScheduler(scheduler, 'engineer', [], null)
  assert.equal(observed.model, 'provider/owner')
  assert.equal(calls[0].role, 'engineer')
  assert.deepEqual(calls[0].running, [])
  assert.equal(calls[0].previous, null)
})

}

{
const { default: assert } = await import("node:assert/strict");
const { mkdir, mkdtemp, readFile, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

// CUT-10: the protocol-2 ABI is owned by wanxiangli/routing. These cases were
// accepted by the host before the cut (see proposals/extraction-kanban/cards/CUT-10.md).
const withConfig = async (body, run) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-routing-cut10-'))
  const path = join(root, 'nested', 'wanxiangshu.mjs')
  try {
    await mkdir(join(path, '..'), { recursive: true })
    await writeFile(path, body, 'utf8')
    await run(path)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
}

test('WHAT[execution-model-routing-002] EMR_002_loader_refuses_module_without_theoretical_capacity_query', async () => {
  const body = `export const routingProtocol = 2
export default function route(role, running, previous) {
  return { model: 'provider/model', reasoning: 'none' }
}
`
  await withConfig(body, async (path) => {
    await assert.rejects(() => routing.bootstrapAndLoadAt(path, body), /hasTheoreticalCapacity/)
    assert.equal(await readFile(path, 'utf8'), body, 'an existing user configuration is never overwritten')
  })
})

test('WHAT[execution-model-routing-002] EMR_002_async_theoretical_capacity_fails_closed_instead_of_queueing', async () => {
  const scheduler = () => null
  scheduler.hasTheoreticalCapacity = async () => false
  const runtime = routing.createRuntime(scheduler)
  await assert.rejects(
    async () => routing.beginExecutionAdmission(runtime, 'session', 'msg-1', 'engineer', 'alice', null),
    /hasTheoreticalCapacity must be synchronous/,
  )
  assert.equal(routing.pendingCount(runtime), 0, 'a Promise is not capacity: nothing may wait for it')
  await assert.rejects(
    async () => routing.beginExecutionAdmission(runtime, 'later', 'msg-2', 'engineer', 'bob', null),
    /hasTheoreticalCapacity must be synchronous/,
    'the configuration error poisons the runtime like a failing route',
  )
})

test('WHAT[execution-model-routing-002] EMR_002_scheduler_receives_exactly_role_running_previous', async () => {
  const seen = []
  const scheduler = function (...args) {
    seen.push(args.length)
    return { model: 'provider/model', reasoning: 'none' }
  }
  scheduler.hasTheoreticalCapacity = function (...args) {
    seen.push(args.length)
    return true
  }
  const runtime = routing.createRuntime(scheduler)
  const acquired = await routing.beginExecutionAdmission(runtime, 'session', 'msg-1', 'engineer', 'alice', null)
  assert.equal(acquired.kind, 'Acquired')
  assert.ok(seen.length >= 2)
  assert.ok(seen.every((count, index) => count === (index === 0 ? 1 : 3)), `arities ${seen}`)
})
}
