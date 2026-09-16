// ENF-021: Blogger repair single-owner physical effects and quiescence gating
import assert from 'node:assert/strict'
import test from 'node:test'

import * as trace from '../../../dist/Interaction/Repair/BloggerRepairTraceSurface.js'

test('WHAT[ENF-021] repeat_terminal_idle_is_idempotent_no_duplicate_nudge', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-repeat-idle', 'ses-blogger-repeat')

    const first = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(first.outcome, 'Sent')
    assert.equal(first.effect, 'nudge')
    assert.equal(first.sentCount, 1)

    const second = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(second.outcome, 'AlreadyAdmitted')
    assert.equal(second.effect, 'none')
    assert.equal(second.sentCount, 1)
  })
})

test('WHAT[ENF-021] idle_without_quiescence_permit_spends_no_budget', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-non-quiescent', 'ses-blogger-non-quiescent')
    trace.denyQuiescence(ctx)

    const observation = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(observation.outcome, 'NotSent')
    assert.equal(observation.effect, 'none')
    assert.equal(observation.sentCount, 0)
  })
})

test('WHAT[ENF-021] next_terminal_sends_at_most_one_aabb_then_abandons', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-nudge-then-aabb', 'ses-blogger-nudge-aabb')

    const nudgeObs = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(nudgeObs.outcome, 'Sent')
    assert.equal(nudgeObs.effect, 'nudge')
    assert.equal(nudgeObs.sentCount, 1)

    const aabbObs = await trace.observeIdle(ctx, episode, 'run-2')
    assert.equal(aabbObs.outcome, 'Sent')
    assert.equal(aabbObs.effect, 'aabb')
    assert.equal(aabbObs.sentCount, 2)

    const thirdObs = await trace.observeIdle(ctx, episode, 'run-3')
    assert.equal(thirdObs.outcome, 'Retired')
    assert.equal(thirdObs.effect, 'abandon')
    assert.equal(thirdObs.sentCount, 2)
  })
})

test('WHAT[ENF-021] exhausted repair stops its real continuation without a process fatal', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-exhausted', 'ses-blogger-exhausted')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    const finalObs = await trace.observeIdle(ctx, episode, 'run-3')

    assert.equal(finalObs.outcome, 'Retired')
    assert.equal(finalObs.abandonSettled, true)
    assert.equal(ctx.fatalReported, false)
  })
})

test('WHAT[ENF-021] terminal without provider identity stops only the exact request', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-no-id', 'ses-blogger-no-id')
    const obs = await trace.observeIdle(ctx, episode, '')

    assert.equal(obs.outcome, 'NotSent')
    assert.equal(obs.effect, 'none')
    assert.equal(obs.sentCount, 0)
  })
})

test('WHAT[ENF-021] failed durable abandon retains its flight and never reports settlement', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-failed-abandon', 'ses-blogger-failed-abandon')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    trace.failNextDurableAppend(ctx)

    const finalObs = await trace.observeIdle(ctx, episode, 'run-3')
    assert.equal(finalObs.outcome, 'Failed')
    assert.equal(finalObs.abandonSettled, false)
  })
})

test('WHAT[ENF-021] repair settlement failure rejects every waiting observer without fatal or release', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-settlement-fail', 'ses-blogger-settlement-fail')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    trace.failNextDurableAppend(ctx)

    const [obs1, obs2] = await Promise.all([
      trace.observeIdle(ctx, episode, 'run-3'),
      trace.observeIdle(ctx, episode, 'run-3'),
    ])
    assert.equal(obs1.outcome, 'Failed')
    assert.equal(obs2.outcome, 'Failed')
    assert.equal(ctx.fatalReported, false)
  })
})

test('WHAT[ENF-021] unknown abandon commit still rejects every observer without release', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-unknown-abandon', 'ses-blogger-unknown-abandon')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    trace.setUnknownCommit(ctx)

    const obs = await trace.observeIdle(ctx, episode, 'run-3')
    assert.equal(obs.outcome, 'Failed')
  })
})

test('WHAT[ENF-021] late observers after settlement failure get the same failure and no new budget', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-late-observer', 'ses-blogger-late-observer')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    trace.failNextDurableAppend(ctx)

    await trace.observeIdle(ctx, episode, 'run-3')
    const late = await trace.observeIdle(ctx, episode, 'run-4')
    assert.equal(late.outcome, 'Failed')
    assert.equal(late.sentCount, 2)
  })
})

test('WHAT[ENF-021] generated duplicate and interleaved repair observations preserve bounded effects', async () => {
  const result = await trace.runInterleavedPropertySuite()
  assert.equal(result.violations.length, 0)
})

test('WHAT[ENF-021] transform_and_idle_interleave_resolves_to_single_owner', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-interleave', 'ses-blogger-interleave')
    const [idleObs, transformObs] = await Promise.all([
      trace.observeIdle(ctx, episode, 'run-1'),
      trace.observeTransform(ctx, episode, 'run-1'),
    ])
    const sentCount = (idleObs.outcome === 'Sent' ? 1 : 0) + (transformObs.outcome === 'Sent' ? 1 : 0)
    assert.equal(sentCount, 1)
  })
})

test('WHAT[ENF-021] transform_on_aabb_claimed_terminal_waits_without_double_spend', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-transform-aabb', 'ses-blogger-transform-aabb')
    await trace.observeIdle(ctx, episode, 'run-1')
    await trace.observeIdle(ctx, episode, 'run-2')
    const transformObs = await trace.observeTransform(ctx, episode, 'run-2')
    assert.equal(transformObs.outcome, 'AlreadyAdmitted')
    assert.equal(transformObs.sentCount, 2)
  })
})

test('WHAT[ENF-021] repair_without_journal_abandons_without_physical_sends', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    const episode = trace.startEpisode(ctx, 'req-no-journal', 'ses-blogger-no-journal')
    trace.detachJournal(ctx)
    const obs = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(obs.outcome, 'Retired')
    assert.equal(obs.sentCount, 0)
  })
})

test('WHAT[ENF-021] shutdown_rejects_new_repair_episode_before_drain', async (t) => {
  await trace.withTraceContext(t, async (ctx) => {
    trace.triggerShutdown(ctx)
    const episode = trace.startEpisode(ctx, 'req-shutdown', 'ses-blogger-shutdown')
    const obs = await trace.observeIdle(ctx, episode, 'run-1')
    assert.equal(obs.outcome, 'NotSent')
    assert.equal(obs.sentCount, 0)
  })
})
