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
      `export const routingProtocol = 2\nexport default async () => ({ model: 'provider/x', reasoning: 'none' })\n`,
    )
    assert.throws(() => invokeScheduler(scheduler, 'coder', []), /Promise|synchronous/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(
      path,
      `export const routingProtocol = 2\nexport default () => ({ model: 'bare-model', reasoning: 'none' })\n`,
    )
    assert.throws(() => invokeScheduler(scheduler, 'coder', []), /provider\/model/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(
      path,
      `export const routingProtocol = 2\nexport default () => ({ model: 'provider/model', reasoning: '' })\n`,
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
    calls.push({ role, previous: previous ?? null, purpose })
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

test('WHAT[execution-model-routing-002] EMR_002_purpose_reaches_scheduler_and_selects_predictor_pool', async () => {
  const behaviour = (role, running, previous, purpose) =>
    purpose === 'readonly-delegate'
      ? { model: 'provider/predictor', reasoning: 'none' }
      : { model: 'provider/owner', reasoning: 'none' }
  const { scheduler, calls } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  const normal = await acquire(runtime, 'ses-normal', 'msg-1', 'engineer', 'alice', 'normal')
  assert.equal(normal.target.model, 'provider/owner')

  const delegate = await acquire(runtime, 'ses-delegate', 'msg-1', 'engineer', 'alice', 'readonly-delegate')
  assert.equal(delegate.target.model, 'provider/predictor')

  assert.deepEqual(
    calls.map((call) => call.purpose),
    ['normal', 'readonly-delegate'],
  )
  assert.deepEqual(
    calls.map((call) => call.previous),
    [null, null],
  )
})

test('WHAT[execution-model-routing-002] EMR_002_pending_demand_reroute_keeps_readonly_delegate_purpose', async () => {
  // The Predictor provider has room for exactly one lease; the waiting demand
  // must re-run through the scheduler with its own purpose after a release.
  const behaviour = (role, running, previous, purpose) =>
    running.length === 0 ? { model: 'provider/predictor', reasoning: 'none' } : null
  const { scheduler, calls } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  const first = await acquire(runtime, 'ses-first', 'msg-1', 'engineer', 'alice', 'readonly-delegate')
  assert.equal(first.target.model, 'provider/predictor')

  const waiting = await beginExecutionAdmission(
    runtime,
    'ses-waiting',
    'msg-1',
    'engineer',
    'bob',
    null,
    'readonly-delegate',
  )
  assert.equal(waiting.kind, 'Queued')

  releasePhysicalExecution(runtime, 'ses-first', 'msg-1')

  const completed = await awaitQueuedExecutionAdmission(waiting.queue)
  assert.equal(completed.kind, 'Acquired')
  assert.equal(executionAdmissionTarget(runtime, completed.lease).model, 'provider/predictor')

  assert.ok(calls.length >= 3, 'the waiting demand must re-enter the scheduler after the release')
  for (const call of calls) {
    assert.equal(call.purpose, 'readonly-delegate', 'a rerun of a delegate demand must not fall back to normal')
  }
})

test('WHAT[execution-model-routing-002] EMR_002_provider_step_recheck_keeps_readonly_delegate_purpose', async () => {
  const behaviour = (role, running, previous, purpose) =>
    purpose === 'readonly-delegate'
      ? { model: 'provider/predictor', reasoning: 'none' }
      : running.length === 0
        ? { model: 'provider/owner', reasoning: 'none' }
        : null
  const { scheduler, calls } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  const owner = await acquire(runtime, 'ses-lender', 'msg-1', 'engineer', 'owner', 'normal')
  assert.equal(owner.target.model, 'provider/owner')

  // Owner holds the only provider step in flight; the delegate demand borrows
  // the lender credit, so its provider-step availability recheck runs through
  // the scheduler with the delegate target as the previous preference.
  enterProviderStep(runtime, 'ses-lender', 'msg-1', [])

  const delegate = await acquire(
    runtime,
    'ses-borrower',
    'msg-1',
    'engineer',
    'delegate',
    'readonly-delegate',
    'ses-lender',
  )
  assert.equal(delegate.target.model, 'provider/predictor')

  enterProviderStep(runtime, 'ses-borrower', 'msg-1', [])

  const recheck = calls[calls.length - 1]
  assert.equal(recheck.purpose, 'readonly-delegate', 'availability recheck must not fall back to normal')
  assert.equal(recheck.previous.model, 'provider/predictor')
})

test('WHAT[execution-model-routing-002] EMR_002_readonly_delegate_never_inherits_owner_previous', async () => {
  const behaviour = (role, running, previous, purpose) =>
    purpose === 'readonly-delegate'
      ? { model: 'provider/predictor', reasoning: 'none' }
      : { model: 'provider/owner', reasoning: 'none' }
  const { scheduler, calls } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  const owner = await acquire(runtime, 'ses-shared', 'msg-1', 'engineer', 'alice', 'normal')
  assert.equal(owner.target.model, 'provider/owner')

  const delegate = await acquire(runtime, 'ses-shared', 'msg-2', 'engineer', 'alice', 'readonly-delegate')
  assert.equal(delegate.target.model, 'provider/predictor')

  const delegateCall = calls[calls.length - 1]
  assert.equal(delegateCall.purpose, 'readonly-delegate')
  assert.equal(delegateCall.previous, null, 'a delegate execution must not inherit the owner target as previous')

  const leased = tryLease(runtime, 'ses-shared', 'msg-3', 'engineer', 'alice', null, 'readonly-delegate')
  assert.equal(leased.model, 'provider/predictor')
  const leaseCall = calls[calls.length - 1]
  assert.equal(leaseCall.purpose, 'readonly-delegate')
  assert.equal(leaseCall.previous, null, 'the lease/validation path must not inherit across purposes either')
})

test('WHAT[execution-model-routing-002] EMR_002_same_physical_execution_cannot_change_purpose', async () => {
  const behaviour = () => ({ model: 'provider/target', reasoning: 'none' })
  const { scheduler } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  await acquire(runtime, 'ses-immutable', 'msg-1', 'engineer', 'alice', 'normal')

  await assert.rejects(
    acquireExecutionAdmission(
      runtime,
      'ses-immutable',
      'msg-1',
      'engineer',
      'alice',
      null,
      'readonly-delegate',
    ),
    /changed execution purpose/,
  )
})

test('WHAT[execution-model-routing-002] EMR_002_readonly_delegate_does_not_inherit_fixed_devops_binding', async () => {
  const behaviour = (role, running, previous, purpose) =>
    purpose === 'readonly-delegate'
      ? { model: 'provider/predictor', reasoning: 'none' }
      : previous ?? { model: 'provider/devops-owner', reasoning: 'high' }
  const { scheduler, calls } = observingScheduler(behaviour)
  const runtime = createRuntime(scheduler)

  const owner = await acquire(runtime, 'ses-devops', 'msg-1', 'devops', 'devops', 'normal')
  assert.equal(owner.target.model, 'provider/devops-owner')

  const delegate = await acquire(runtime, 'ses-devops', 'msg-2', 'devops', 'devops', 'readonly-delegate')
  assert.equal(delegate.target.model, 'provider/predictor')

  const delegateCall = calls[calls.length - 1]
  assert.equal(delegateCall.purpose, 'readonly-delegate')
  assert.equal(delegateCall.previous, null, 'the fixed DevOps binding must not cover a delegate execution')

  const ownerAgain = await acquire(runtime, 'ses-devops', 'msg-3', 'devops', 'devops', 'normal')
  assert.equal(ownerAgain.target.model, 'provider/devops-owner', 'the owner binding survives the delegate execution')
})

test('WHAT[execution-model-routing-002] EMR_002_invoke_scheduler_passes_purpose_through_the_boundary', async () => {
  const { scheduler } = observingScheduler((role, running, previous, purpose) =>
    purpose === 'readonly-delegate'
      ? { model: 'provider/predictor', reasoning: 'none' }
      : { model: 'provider/owner', reasoning: 'none' },
  )
  const observed = invokeScheduler(scheduler, 'engineer', [], null, 'readonly-delegate')
  assert.equal(observed.model, 'provider/predictor')
  assert.equal(invokeScheduler(scheduler, 'engineer', [], null, 'normal').model, 'provider/owner')
})

}
