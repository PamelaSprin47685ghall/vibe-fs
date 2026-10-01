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
    await admit(runtime, 'discovery-owner')
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'DISCOVERY-CONCERN' }, context('discovery-owner', 'subscription'))
    await hooks.tool.publish.execute({ id: 'build', message: 'OWNER-ONLY-MESSAGE' }, context('discovery-owner', 'publication'))
    await admit(runtime, 'discovery-peer')
    const first = await transform(hooks, 'discovery-peer', [user('discovery-peer')])
    assert.match(JSON.stringify(hints(first)), /DISCOVERY-CONCERN/)
    assert.doesNotMatch(JSON.stringify(first), /OWNER-ONLY-MESSAGE|discovery-owner/)
    const next = await transform(hooks, 'discovery-peer', [...first, ...toolBatch('discovery-peer', 'second')])
    assert.ok(hints(next).length > hints(first).length)
    assert.doesNotMatch(JSON.stringify(hints(next).slice(hints(first).length)), /DISCOVERY-CONCERN|OWNER-ONLY-MESSAGE/)
  })
})

test('WHAT[concern-routing-002] all eligible participant kinds receive each live generation exactly once across restart, excluding ineligible roles', async () => {
  const { withRestartablePlugin, configureManagedPlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
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
      for (const [session] of eligible) await admit(runtime, session)
    })
    const seen = new Map()
    for (const [session] of eligible) {
      const first = await transform(hooks, session, [user(session)])
      assert.match(JSON.stringify(hints(first)), /RESTART-GENERATION/, `${session} receives the announcement`)
      seen.set(session, 1)
      const next = await transform(hooks, session, [...first, ...toolBatch(session, 'second')])
      assert.doesNotMatch(JSON.stringify(hints(next).slice(hints(first).length)), /RESTART-GENERATION/, 'no repeat within the same life')
    }

    // Restart: durable subscription survives, already-delivered recipients do
    // not get a second announcement, and a newly eligible peer still gets it.
    await fixture.stop(hooks)
    hooks = await boot()
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'restart-newcomer')
    })
    const newcomer = await transform(hooks, 'restart-newcomer', [user('restart-newcomer')])
    assert.match(JSON.stringify(hints(newcomer)), /RESTART-GENERATION/, 'newly eligible peer receives it after restart')
    // Across-restart exactly-once for already-delivered recipients depends on
    // journal replay restoring AnnouncementCoverage; recorded in GAP-155.

    // Ineligible role: blogger carries no cognitive tools, so no announcement.
    await fixture.withRuntime(async (runtime) => {
      await admit(runtime, 'restart-blogger', 'blogger')
    })
    const blogger = await transform(hooks, 'restart-blogger', [user('restart-blogger')])
    assert.doesNotMatch(JSON.stringify(hints(blogger)), /RESTART-GENERATION|OWNER-RESTART-MESSAGE/, 'ineligible role receives nothing')
    await fixture.stop(hooks)
  })
})
