import assert from 'node:assert/strict'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context, user, toolBatch, transform, hints } from './support/plugin.mjs'

test('WHAT[concern-routing-002] projection announcement coverage is per recipient, including the owner', () => {
  let state = concern.subscribe('owner', 'generation', 'build', 'build health', concern.empty()).state
  for (const recipient of ['owner', 'peer', 'newcomer']) {
    const prepared = concern.prepare(recipient, state)
    assert.deepEqual(prepared.announcements, [{ id: 'build', concern: 'build health' }])
    const placed = concern.place(recipient, prepared.announcedGenerations, [], state)
    assert.equal(placed.ok, true)
    state = placed.state
    assert.deepEqual(concern.prepare(recipient, state).announcements, [])
  }
})

test('WHAT[concern-routing-002] actual newly eligible peer receives address discovery once and never receives the owner’s message', async () => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    await admit(runtime, 'discovery-owner', 'engineer')
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'DISCOVERY-CONCERN' }, context('discovery-owner', 'subscription'))
    await hooks.tool.publish.execute({ id: 'build', message: 'OWNER-ONLY-MESSAGE' }, context('discovery-owner', 'publication'))
    await admit(runtime, 'discovery-peer', 'engineer')
    const first = await transform(hooks, runtime, 'discovery-peer', [user('discovery-peer')])
    assert.match(JSON.stringify(hints(first)), /DISCOVERY-CONCERN/)
    assert.doesNotMatch(JSON.stringify(first), /OWNER-ONLY-MESSAGE|discovery-owner/)
    const next = await transform(hooks, runtime, 'discovery-peer', [...first, ...toolBatch('discovery-peer', 'second')])
    assert.ok(hints(next).length > hints(first).length)
    assert.doesNotMatch(JSON.stringify(hints(next).slice(hints(first).length)), /DISCOVERY-CONCERN|OWNER-ONLY-MESSAGE/)
  })
})

test('WHAT[concern-routing-002] eligible participant kinds receive each live generation once, excluding ineligible roles', async () => {
  const { withRestartablePlugin, configureManagedPlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
  await withRestartablePlugin(async (start, _directory, fixture) => {
    const boot = async () => {
      const hooks = await start()
      await configureManagedPlugin(hooks)
      return hooks
    }
    let hooks = await boot()
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'restart-owner')
    })
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'RESTART-GENERATION' }, context('restart-owner', 'subscription'))
    await hooks.tool.publish.execute({ id: 'build', message: 'OWNER-RESTART-MESSAGE' }, context('restart-owner', 'publication'))

    // Eligible participant kinds: engineer, manager, devops each receive the
    // announcement exactly once in their first Pair Hint after eligibility.
    const eligible = [
      ['restart-engineer', 'engineer'],
      ['restart-manager', 'manager'],
      ['restart-devops', 'devops'],
    ]
    await fixture.withRuntime(async (runtime) => {
      for (const [session, role] of eligible) await admit(runtime, session, role)
    })
    // Observe the durable admission: each session's accepted root carries the
    // declared role, so the test really covers distinct participant kinds.
    for (const [session, role] of eligible) {
      await fixture.withRuntime(async (runtime) => {
        const observed = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.equal(observed?.participantIdentity?.role, role, `${session} must be admitted as ${role}`)
      })
    }
    const delivered = []
    for (const [session] of eligible) {
      const first = await transform(hooks, fixture, session, [user(session)])
      assert.match(JSON.stringify(hints(first)), /RESTART-GENERATION/, `${session} receives the announcement`)
      delivered.push(session)
      const next = await transform(hooks, fixture, session, [...first, ...toolBatch(session, 'second')])
      assert.doesNotMatch(JSON.stringify(hints(next).slice(hints(first).length)), /RESTART-GENERATION/, 'no repeat within the same life')
    }
    assert.deepEqual(delivered, eligible.map(([session]) => session))

    // Restart: the durable subscription survives and a newly eligible peer
    // still receives the announcement.
    await fixture.stop(hooks)
    hooks = await boot()
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'restart-newcomer')
    })
    const newcomer = await transform(hooks, fixture, 'restart-newcomer', [user('restart-newcomer')])
    assert.match(JSON.stringify(hints(newcomer)), /RESTART-GENERATION/, 'newly eligible peer receives it after restart')

    // Ineligible role: blogger carries no cognitive tools, so no announcement.
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'restart-blogger', 'blogger')
    })
    const blogger = await transform(hooks, fixture, 'restart-blogger', [user('restart-blogger')])
    assert.doesNotMatch(JSON.stringify(hints(blogger)), /RESTART-GENERATION|OWNER-RESTART-MESSAGE/, 'ineligible role receives nothing')
    await fixture.stop(hooks)
  })
})

test('WHAT[concern-routing-002] new physical ingress after plugin reopen replays frozen history without repeating announcements', async () => {
  const { withRestartablePlugin, configureManagedPlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  await withRestartablePlugin(async (start, _directory, fixture) => {
    const boot = async () => {
      const hooks = await start()
      await configureManagedPlugin(hooks)
      return hooks
    }
    let hooks = await boot()
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'repro-owner')
    })
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'REPRO-GENERATION' }, context('repro-owner', 'subscription'))
    await hooks.tool.publish.execute({ id: 'build', message: 'OWNER-REPRO-MESSAGE' }, context('repro-owner', 'publication'))

    const eligible = [
      ['repro-engineer', 'engineer'],
      ['repro-manager', 'manager'],
      ['repro-devops', 'devops'],
    ]
    await fixture.withRuntime(async (runtime) => {
      for (const [session, role] of eligible) await admit(runtime, session, role)
    })
    const deliveredHints = new Map()
    for (const [session] of eligible) {
      const first = await transform(hooks, fixture, session, [user(session)])
      assert.match(JSON.stringify(hints(first)), /REPRO-GENERATION/, session + ' receives the announcement before restart')
      deliveredHints.set(session, hints(first))
    }

    // Restart the plugin incarnation over the same Git-private journal
    // (plugin reopen — not a proven OS-crash recovery).
    await fixture.stop(hooks)
    hooks = await boot()

    for (const [session] of eligible) {
      const frozen = deliveredHints.get(session)
      // A reopened plugin has no old provider lease. A new physical input
      // starts the next execution while its history replays the frozen hint.
      const replay = await transform(hooks, fixture, session, [
        user(session), user(session, `physical-${session}-after-restart`),
      ])
      const historical = replay.filter(message => message.info?.id === `root-${session}`)
      assert.deepEqual(hints(historical), frozen, session + ' replays the frozen hint byte-for-byte')
      assert.ok(hints(replay).length > frozen.length, session + ' reaches a new occurrence on the new physical input')
      assert.doesNotMatch(JSON.stringify(hints(replay).slice(frozen.length)), /REPRO-GENERATION/, session + ' does not repeat the announcement after restart')
      assert.doesNotMatch(JSON.stringify(replay), /OWNER-REPRO-MESSAGE/, session + ' never receives the owner mailbox contents')

      const next = await transform(hooks, fixture, session, [...replay, ...toolBatch(session, 'after-restart')])
      const nextHints = hints(next)
      assert.deepEqual(nextHints.slice(0, frozen.length), frozen, session + ' keeps historical hints unchanged')
      assert.ok(nextHints.length > frozen.length, session + ' reaches a new Pair Hint occurrence')
      assert.doesNotMatch(JSON.stringify(nextHints.slice(frozen.length)), /REPRO-GENERATION/, session + ' does not repeat the announcement in the new occurrence')
    }
    await fixture.stop(hooks)
  })
})
