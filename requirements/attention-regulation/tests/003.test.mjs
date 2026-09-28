import assert from 'node:assert/strict'
import test from 'node:test'
import { recordingPort, context, tools, toolModule, attention } from './support/attention-port.mjs'

test('WHAT[attention-regulation-003] actual defer tool persists trimmed work through its port before returning acceptance', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'defer', { new_work: '  investigate later  ' }, context())
  assert.ok(accepted.includes('investigate later'))
  assert.deepEqual(fixture.appends, [{ session: 'session-a', providerRun: 'run-1', fact: { session: 'session-a', occurrence: 'call-1', text: 'investigate later' } }])
  assert.deepEqual(attention.pending('session-a', fixture.state), [{ occurrence: 'call-1', text: 'investigate later' }])
})

test('WHAT[attention-regulation-003] absent rejected and throwing durability cannot report successful defer', async () => {
  const args = { new_work: 'later' }
  const unavailable = await tools.execute(tools.withoutJournal(toolModule), 'defer', args, context())
  const refused = recordingPort()
  refused.accept = false
  assert.equal(await tools.execute(refused.tools, 'defer', args, context()), unavailable)
  assert.deepEqual(attention.pending('session-a', refused.state), [])
  const broken = recordingPort()
  const failure = new Error('append transport failed')
  broken.fault = failure
  await assert.rejects(tools.execute(broken.tools, 'defer', args, context()), (error) => error === failure)
  const brokenRead = tools.create(toolModule, () => { throw failure }, async () => assert.fail('failed read must not append'))
  await assert.rejects(tools.execute(brokenRead, 'defer', args, context()), (error) => error === failure)
})

test('WHAT[attention-regulation-003] blank work and missing occurrence identity do not touch durable state', async () => {
  const fixture = recordingPort()
  await tools.execute(fixture.tools, 'defer', { new_work: ' \n ' }, context())
  for (const ctx of [{ sessionID: 'session-a' }, { sessionID: 'session-a', callID: 'call-1' }, context('')]) {
    await tools.execute(fixture.tools, 'defer', { new_work: 'later' }, ctx)
  }
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})
