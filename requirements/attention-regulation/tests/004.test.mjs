import assert from 'node:assert/strict'
import test from 'node:test'
import * as attention from '../../../dist/Interaction/Attention/Surface.js'
import { recordingPort, context, tools } from './support/attention-port.mjs'
import { withRestartablePlugin, configureManagedPlugin, acceptAuthorityRoot, activateLife } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[attention-regulation-004] actual port path deduplicates occurrence and does not resurrect consumed work', async () => {
  const fixture = recordingPort()
  for (let replay = 0; replay < 2; replay += 1) {
    await tools.execute(fixture.tools, 'defer', { new_work: 'one' }, context())
  }
  await tools.execute(fixture.tools, 'defer', { new_work: 'two' }, context('session-b'))
  assert.equal(fixture.appends.length, 2)
  assert.deepEqual(attention.pending('session-a', fixture.state), [{ occurrence: 'call-1', text: 'one' }])
  assert.deepEqual(attention.pending('session-b', fixture.state), [{ occurrence: 'call-1', text: 'two' }])
  fixture.state = attention.resurface('session-a', 'celebration-1', ['call-1'], fixture.state)
  await tools.execute(fixture.tools, 'defer', { new_work: 'one' }, context())
  assert.equal(fixture.appends.length, 2)
  assert.deepEqual(attention.pending('session-a', fixture.state), [])
})

test('WHAT[attention-regulation-004] real plugin restart preserves each participant work and frozen consumption result', async () => {
  await withRestartablePlugin(async (start, _directory, fixture) => {
    const boot = async () => {
      const hooks = await start()
      await configureManagedPlugin(hooks)
      return hooks
    }
    const ctx = (sessionID, callID) => ({ sessionID, agent: 'manager', messageID: `run-${sessionID}`, callID })
    let hooks = await boot()
    await fixture.withRuntime(async (runtime) => {
      for (const session of ['attention-a', 'attention-b']) {
        await acceptAuthorityRoot(runtime, session, 'manager', `root-${session}`)
        await activateLife(runtime, session, `root-${session}`)
      }
    })
    for (const session of ['attention-a', 'attention-b']) {
      await hooks.tool.defer.execute({ new_work: `WORK FOR ${session}` }, ctx(session, 'defer-1'))
    }
    await fixture.stop(hooks)
    hooks = await boot()
    const args = { experience: 'A local experiment completed.' }
    const first = await hooks.tool.celebrate.execute(args, ctx('attention-a', 'celebrate-1'))
    assert.ok(first.includes('WORK FOR attention-a'))
    assert.equal(first.includes('WORK FOR attention-b'), false)
    await fixture.stop(hooks)
    hooks = await boot()
    assert.equal(await hooks.tool.celebrate.execute(args, ctx('attention-a', 'celebrate-1')), first)
    const next = await hooks.tool.celebrate.execute(args, ctx('attention-a', 'celebrate-2'))
    assert.equal(next.includes('WORK FOR'), false)
    const other = await hooks.tool.celebrate.execute(args, ctx('attention-b', 'celebrate-1'))
    assert.ok(other.includes('WORK FOR attention-b'))
    assert.equal(other.includes('WORK FOR attention-a'), false)
  })
})

test.todo('WHAT[attention-regulation-004] GAP-118 exact life closure then SessionId reuse never inherits old pending work')
