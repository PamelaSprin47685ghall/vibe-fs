import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

const candidate = ({ cutoff, prefixDigest = `prefix-${cutoff}`, digest = `frozen-${cutoff}`, seal = `seal-${cutoff}` }) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: digest,
    cutoff,
    prefixDigest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const rebase = (state, { previousEpoch, nextEpoch, cutoff, digest, seal, prefixDigest }) =>
  prefix.applyRebase(
    { previousEpoch, nextEpoch, candidate: candidate({ cutoff, digest, seal, prefixDigest }) },
    state,
  )

const reanchor = (state, { previousEpoch, nextEpoch, observedRun = 'msg_compaction' }) =>
  prefix.applyReanchor({ previousEpoch, nextEpoch, observedRun }, state)

test('WHAT[prefix-stability-012] invalid later prefix facts leave the supplied committed projection unchanged', () => {
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 7 }).value
  const reanchored = reanchor(committed, { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c1' }).value

  // A subsequent, ill-formed attempt (stale epoch / non-successor / replay of
  // the same compaction) is refused — and the committed reanchor stays exactly
  // as it was: epoch advanced once, snapshot retired, run recorded.
  assert.deepEqual(
    rebase(reanchored, { previousEpoch: 1, nextEpoch: 2, cutoff: 9 }),
    { ok: false, error: 'StalePrefixEpoch' },
  )
  assert.deepEqual(
    reanchor(reanchored, { previousEpoch: 2, nextEpoch: 3, observedRun: 'msg_c1' }),
    { ok: false, error: 'CompactionAlreadyReanchored' },
  )
  assert.deepEqual(
    rebase(reanchored, { previousEpoch: 2, nextEpoch: 4, cutoff: 9 }),
    { ok: false, error: 'NonSequentialPrefixEpoch' },
  )
  assert.equal(prefix.epochOf(reanchored), 2n)
  assert.equal(prefix.hasSnapshot(reanchored), false)
  assert.deepEqual(prefix.reanchoredRuns(reanchored), ['msg_c1'])
})

for (const boundary of ['rebase', 'reanchor']) {
  for (const outcome of ['Failed', 'Aborted']) {
    test(`WHAT[prefix-stability-012] a registered committed ${boundary} survives later ${outcome}, journal reopen and a new legitimate request`, async t => {
      const { configureManagedPlugin, withRestartablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
      const { checkedTest, coveredSession, commitPendingBlogger, continuation, completeToolAttempt, failAttempt, facts, observeFact, prefixHistory, prefixState, state } = await import('./support/registered-prefix.mjs')
      const check = (name, action) => checkedTest(t, name, action)
      const sessionID = `ses-prefix-irreversible-${boundary}-${outcome}`
      await withRestartablePlugin(async (start, directory, host) => {
        const hooks = await start()
        await configureManagedPlugin(hooks)
        let session
        let committedHistory
        let committedState
        await host.withRuntime(async journalRuntime => {
          const runtime = { ...journalRuntime, ...host }
          session = await coveredSession(hooks, runtime, directory, sessionID, t)
          await check('registered successful provider attempt durably commits its own rebase', async () => {
            const attempt = await continuation(hooks, runtime, session, 'msg-commit-prefix')
            assert.ok(attempt.projected.messages.some(message => message.info?.source === 'companion-memory'))
            assert.deepEqual(prefixHistory(directory).rebases, [])
            assert.deepEqual(prefixHistory(directory).reanchors, [])
            await completeToolAttempt(session, attempt, hooks)
            const committed = prefixHistory(directory).rebases
            assert.equal(committed.length, 1)
            assert.deepEqual(committed[0].SolvingProviderRun, ['ProviderRunIdentity', attempt.run.info.id])
            assert.deepEqual(state(runtime, sessionID).prefixEpoch, { epochId: 1n, snapshotPresent: true })
          })
          if (boundary === 'reanchor') {
            await check('the pending real Blogger request commits coverage beyond the existing rebase', async () => {
              await commitPendingBlogger(hooks, runtime, directory, session)
              assert.equal(prefixHistory(directory).rebases.length, 1)
              assert.equal(prefixHistory(directory).reanchors.length, 0)
            })
            await check('fresh real producer coverage lets normal exact reconciliation settle before manual compaction', async () => {
              const warmup = await continuation(hooks, runtime, session, 'msg-before-compaction')
              await observeFact(directory, 'FailureRecorded', () => failAttempt(hooks, runtime, session, warmup, 'Failed'))
              assert.equal(state(runtime, sessionID).providerFailures.consecutiveFailureCount, 1)
            })
            await check('Host snapshot observer commits the real compaction reanchor', async () => {
              session.push({ info: { id: 'msg-real-compaction', sessionID, role: 'assistant', agent: 'compaction', summary: true, time: { created: session.clock + 4, completed: session.clock + 5 } }, parts: [{ type: 'text', text: 'Host compacted context' }] })
              await observeFact(directory, 'ContextReanchored', async () => {
                await continuation(hooks, runtime, session, 'msg-compaction-successor')
                await hooks.event({ event: { type: 'session.status', sessionID, properties: { sessionID, status: { type: 'retry', attempt: 1 } } } })
              })
              const history = prefixHistory(directory)
              assert.equal(history.rebases.length, 1)
              assert.equal(history.reanchors.length, 1, 'the actual HostCompactionObserver must append ContextReanchored')
              assert.deepEqual(history.reanchors[0].ObservedCompactionRun, ['ProviderRunIdentity', 'msg-real-compaction'])
              assert.deepEqual(state(runtime, sessionID).prefixEpoch, { epochId: 2n, snapshotPresent: false })
            })
          }
          committedHistory = prefixHistory(directory)
          committedState = prefixState(runtime, sessionID)
        })
        await host.withRuntime(async journalRuntime => {
          const runtime = { ...journalRuntime, ...host }
          await check(`new admitted attempt reaches exact ${outcome} settlement without changing committed prefix facts`, async () => {
            const later = await continuation(hooks, runtime, session, `msg-later-${outcome}`)
            assert.deepEqual(prefixHistory(directory), committedHistory)
            assert.deepEqual(prefixState(runtime, sessionID), committedState)
            if (outcome === 'Failed') {
              await observeFact(directory, 'FailureRecorded', () => failAttempt(hooks, runtime, session, later, outcome))
              assert.deepEqual(facts(directory, 'FailureRecorded').at(-1).ProviderRun, ['ProviderRunIdentity', later.run.info.id])
              assert.deepEqual(prefixHistory(directory), committedHistory)
              assert.deepEqual(prefixState(runtime, sessionID), committedState)
              await continuation(hooks, runtime, session, 'msg-admitted-after-failure', 'ProviderRetryAttempt')
            } else {
              const disposition = await failAttempt(hooks, runtime, session, later, outcome)
              assert.deepEqual(disposition, { phase: 'Terminal', disposition: 'Cancelled' })
            }
            await host.stop(hooks)
            assert.deepEqual(prefixHistory(directory), committedHistory)
            assert.deepEqual(prefixState(runtime, sessionID), committedState)
          })
        })
        const reopened = await start()
        await configureManagedPlugin(reopened)
        await host.withRuntime(async journalRuntime => {
          const runtime = { ...journalRuntime, ...host }
          await check('cold journal replay preserves the original boundary and the next legitimate request', async () => {
            assert.deepEqual(prefixHistory(directory), committedHistory)
            assert.deepEqual(prefixState(runtime, sessionID), committedState)
            const next = await continuation(reopened, runtime, session, 'msg-after-reopen')
            assert.equal(next.message.metadata.wanxiangshu_origin, 'ManagedDelegationAssignment')
            const started = facts(directory, 'ProviderStarted').find(fact => fact.Key.PhysicalUserMessageId[1] === next.physical)
            assert.deepEqual(started.Evidence.Accepted.Origin, ['Continuation', 'ManagedDelegationAssignment'])
            assert.deepEqual(started.Evidence.ProviderRun, ['ProviderRunIdentity', next.run.info.id])
            assert.equal(started.Evidence.ProjectionChoice, 'UseCommittedEpoch')
            assert.deepEqual(prefixHistory(directory), committedHistory)
            assert.deepEqual(prefixState(runtime, sessionID), committedState)
          })
        })
      })
    })
  }
}
