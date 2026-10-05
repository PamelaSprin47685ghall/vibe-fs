import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const companion = await import("../../../dist/Context/Companion/ProjectionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[prefix-stability-003] the registered failure proof rejects production promotion of Failed candidates', async t => {
  const assert = (await import('node:assert/strict')).default
  const { spawn } = await import('node:child_process')
  const { createInterface } = await import('node:readline')
  const { join } = await import('node:path')
  const { checkedTest } = await import('./support/registered-prefix.mjs')
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  env.WANXIANGSHU_FAILED_PREFIX_PROMOTION_MUTATION = 'enabled'
  const stages = [
    'registered authority and Host history create the real Blogger request',
    'actual Chronicle execution and Host transcript commit the covered frame',
    'native checkpoint tool completion opens the real phase window',
    'exact first provider failure establishes the retry budget without promotion',
    'admitted retry renders its candidate without changing committed state',
  ].map(name => ({ name, receipt: Promise.withResolvers() }))
  const child = spawn(process.execPath, [
    join(import.meta.dirname, 'support/promotion-proof-runner.mjs'),
    import.meta.filename, join(import.meta.dirname, 'support/failed-promotion-loader.mjs'),
  ], { env, stdio: ['ignore', 'pipe', 'pipe'] })
  let output = ''
  const rejected = Promise.withResolvers()
  let fullyDrained = false
  const lines = createInterface({ input: child.stdout })
  lines.on('line', line => {
    const data = JSON.parse(line)
    if (data.type === 'drained') {
      fullyDrained = true
      return
    }
    if (data.runEntry !== import.meta.filename) return
    if (![import.meta.filename, join(import.meta.dirname, 'support/registered-prefix.mjs')].includes(data.file)) return
    if (!data.passed) output += data.error
    for (const stage of stages) {
      if (data.name === stage.name) stage.receipt.resolve(data.passed === true && !data.skipped && !data.todo)
    }
    if (data.name === 'exact retry failure discards its candidate without a prefix commit or rollback') rejected.resolve(data)
  })
  child.stderr.on('data', chunk => { output += chunk })
  const exit = Promise.withResolvers()
  child.on('error', exit.reject)
  child.on('close', code => {
    for (const stage of stages) stage.receipt.resolve(false)
    rejected.resolve(null)
    exit.resolve(code)
  })
  try {
    for (const stage of stages) {
      await checkedTest(t, `the production mutant passes ${stage.name}`, async () => {
        assert.equal(await stage.receipt.promise, true, output)
      })
    }
    await checkedTest(t, 'the real retry settlement detects the wrong production promotion', async () => {
      const verdict = await rejected.promise
      assert.ok(verdict, output)
      assert.equal(verdict.passed, false, output)
      assert.equal(verdict.skipped, false, output)
      assert.equal(verdict.todo, false, output)
      assert.match(output, /a failed retry must not append a prefix commit or rollback/)
    })
    await checkedTest(t, 'the production mutant finishes and releases its owned test process', async () => {
      assert.equal(await exit.promise, 1, output)
      assert.equal(fullyDrained, true, output)
    })
  } finally {
    if (child.exitCode === null) child.kill('SIGTERM')
    await exit.promise
    lines.close()
  }
})

test('WHAT[prefix-stability-003] absent candidate selects the supplied committed snapshot', () => {
  const committed = snapshotAt(4)

  const failed = planner.attemptPlan({
    role: 'Engineer',
    tier: 'Fast',
    kind: 'WorkMain',
    mayRecover: true,
    noCandidateReason: 'NoCoverage',
  })

  assert.equal(failed.choice, 'UseCommittedEpoch')
  assert.equal(failed.probeId, null)
  assert.equal(failed.noProbeReason, 'NoCoverage')

  const next = prefix.forChoice({ kind: 'committed' }, committed, companion.memoryPreamble, 'B BODY')
  assert.equal(next.dropLeading, 4)
})
test('WHAT[prefix-stability-003] CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way', () => {
  // A probe is not a different kind of request — it is the same request with a
  // candidate prefix. Separate code paths would let the two drift, and CTX-012 requires
  // a promoted probe to be byte-identical to what the successful attempt sent.
  const candidate = snapshotAt(7, { seal: 'seal-candidate' })

  const asProbe = prefix.forChoice({ kind: 'probe', candidate }, null, companion.memoryPreamble, 'BODY')
  const asCommitted = prefix.forSnapshot(candidate, companion.memoryPreamble, 'BODY')

  assert.deepEqual(asProbe, asCommitted)
})
test('WHAT[prefix-stability-003] CTX_010_the_required_blob_follows_the_choice_not_the_committed_state', () => {
  // The failure this prevents: reading the COMMITTED snapshot's blob for a probe
  // attempt injects the old FrozenRecordPrefix under the candidate's synthetic id. The provider
  // sees a changed prefix, and no fold can detect it — both halves are individually
  // well-formed.
  const committed = snapshotAt(4)
  const candidate = snapshotAt(9)

  assert.equal(prefix.requiredBlob({ kind: 'committed' }, committed), 'blob-frozen-4')
  assert.equal(
    prefix.requiredBlob(
      { kind: 'probe', candidate: probeFor({ cutoff: 9 }).candidate },
      committed,
    ),
    'blob-frozen-9',
    'a probe attempt reads the CANDIDATE blob',
  )

  assert.equal(prefix.requiredBlob({ kind: 'committed' }, null), null, 'raw history needs no blob')
})
}

test('WHAT[prefix-stability-003] an actual admitted retry candidate is discarded on exact provider failure and stays discarded after reopen', async t => {
  const assert = (await import('node:assert/strict')).default
  const { configureManagedPlugin, withRestartablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const { checkedTest, coveredSession, continuation, completeToolAttempt, failAttempt, facts, observeFact, prefixHistory, prefixState, state } = await import('./support/registered-prefix.mjs')
  const check = (name, action) => checkedTest(t, name, action)
  const sessionID = 'ses-prefix-failed-retry'
  await withRestartablePlugin(async (start, directory, host) => {
    const hooks = await start()
    await configureManagedPlugin(hooks)
    let baseline
    let history
    let session
    let retry
    await host.withRuntime(async journalRuntime => {
      const runtime = { ...journalRuntime, ...host }
      await check('registered producer commits the covered material', async producer => {
        session = await coveredSession(hooks, runtime, directory, sessionID, producer)
        history = prefixHistory(directory)
        assert.deepEqual(history.rebases, [])
        assert.deepEqual(history.reanchors, [])
      })
      await check('exact first provider failure establishes the retry budget without promotion', async () => {
        const initial = await continuation(hooks, runtime, session, 'msg-first-candidate')
        assert.ok(initial.projected.messages.some(message => message.info?.source === 'companion-memory'))
        await observeFact(directory, 'FailureRecorded', () => failAttempt(hooks, runtime, session, initial, 'Failed'))
        assert.deepEqual(prefixHistory(directory), history, 'failed candidate must not be promoted')
        assert.equal(state(runtime, sessionID).providerFailures.consecutiveFailureCount, 1)
        baseline = prefixState(runtime, sessionID)
      })
      await check('admitted retry renders its candidate without changing committed state', async () => {
        retry = await continuation(hooks, runtime, session, 'msg-failed-retry', 'ProviderRetryAttempt')
        assert.equal(retry.message.metadata.wanxiangshu_origin, 'ProviderRetryAttempt')
        assert.ok(retry.projected.messages.some(message => message.info?.source === 'companion-memory'), 'the actual retry must carry a candidate')
        const started = facts(directory, 'ProviderStarted').find(fact => fact.Key.PhysicalUserMessageId[1] === retry.physical)
        assert.deepEqual(started.Evidence.ProviderRun, ['ProviderRunIdentity', retry.run.info.id])
        assert.deepEqual(started.Evidence.Accepted.Origin, ['Continuation', 'ProviderRetryAttempt'])
        assert.equal(started.Evidence.ProjectionChoice[0], 'UsePrefixProbe')
        assert.equal(started.Evidence.ProjectionChoice[1].Candidate.CutoffExclusive, 4)
        assert.deepEqual(prefixState(runtime, sessionID), baseline, 'candidate rendering cannot commit its epoch')
      })
      await check('exact retry failure discards its candidate without a prefix commit or rollback', async () => {
        if (process.env.WANXIANGSHU_FAILED_PREFIX_PROMOTION_MUTATION === 'enabled') {
          globalThis.__wanxiangshu_failed_prefix_promotion = true
        }
        try {
          await observeFact(directory, 'FailureRecorded', () => failAttempt(hooks, runtime, session, retry, 'Failed'))
          const failures = facts(directory, 'FailureRecorded')
          assert.deepEqual(failures.at(-1).ProviderRun, ['ProviderRunIdentity', retry.run.info.id])
          assert.deepEqual(prefixHistory(directory), history, 'a failed retry must not append a prefix commit or rollback')
          assert.equal(state(runtime, sessionID).providerFailures.consecutiveFailureCount, 2, 'exact retry settlement records the second failure')
          assert.deepEqual(prefixState(runtime, sessionID), baseline)
        } finally {
          delete globalThis.__wanxiangshu_failed_prefix_promotion
          await continuation(hooks, runtime, session, 'msg-admitted-after-retry-failure', 'ProviderRetryAttempt')
          await host.stop(hooks)
        }
        assert.deepEqual(prefixHistory(directory), history, 'draining settlement must preserve the complete prefix history')
        assert.deepEqual(prefixState(runtime, sessionID), baseline)
      })
    })
    await host.stop(hooks)
    const reopened = await start()
    await configureManagedPlugin(reopened)
    await host.withRuntime(async journalRuntime => {
      const runtime = { ...journalRuntime, ...host }
      let next
      await check('reopened journal preserves state and a normal request receives its own admission', async () => {
        assert.deepEqual(prefixHistory(directory), history)
        assert.deepEqual(prefixState(runtime, sessionID), baseline)
        next = await continuation(reopened, runtime, session, 'msg-normal-after-failure')
        assert.deepEqual(prefixHistory(directory), history)
        assert.deepEqual(prefixState(runtime, sessionID), baseline)
        assert.equal(next.message.metadata.wanxiangshu_origin, 'ManagedDelegationAssignment')
        const started = facts(directory, 'ProviderStarted').find(fact => fact.Key.PhysicalUserMessageId[1] === next.physical)
        assert.deepEqual(started.Evidence.Accepted.Origin, ['Continuation', 'ManagedDelegationAssignment'])
        assert.deepEqual(started.Evidence.ProviderRun, ['ProviderRunIdentity', next.run.info.id])
        assert.equal(started.Evidence.ProjectionChoice[0], 'UsePrefixProbe')
        assert.equal(started.Evidence.ProjectionChoice[1].Candidate.CutoffExclusive, 4)
      })
      await check('only the new successful provider run commits the later rebase', async () => {
        await completeToolAttempt(session, next, reopened)
        const committed = prefixHistory(directory).rebases
        assert.equal(committed.length, 1, 'only the later successful ordinary attempt may commit')
        assert.deepEqual(committed[0].SolvingProviderRun, ['ProviderRunIdentity', next.run.info.id])
        assert.notDeepEqual(committed[0].SolvingProviderRun, ['ProviderRunIdentity', 'run-msg-failed-retry'])
      })
    })
  })
})
