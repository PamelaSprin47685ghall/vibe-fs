import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as toolModule from '@opencode-ai/plugin'
import * as attention from '../../../dist/Interaction/Attention/Surface.js'
import * as tools from '../../../dist/OpenCode/Tools/AttentionToolSurface.js'

const read = (path) => readFileSync(path, 'utf8')

const context = (sessionID = 'ses-a', callID = 'call-1') => ({ sessionID, callID, messageID: 'run-1' })

const recordingPort = () => {
  const fixture = { state: attention.empty(), reads: 0, appends: [], accept: true, fault: null }
  fixture.tools = tools.create(toolModule, () => {
    fixture.reads += 1
    return fixture.state
  }, async (session, providerRun, fact) => {
    fixture.appends.push({ session, providerRun, fact })
    if (fixture.fault) throw fixture.fault
    if (!fixture.accept) return false
    fixture.state = attention.record(fact.session, fact.occurrence, fact.text, fixture.state)
    return true
  })
  return fixture
}

test('WHAT[ATTENTION-REGULATION-001] enough is a pure cognitive stop with no durable authority state', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'enough', { decision: '  use the existing result  ' }, context())
  const rejected = await tools.execute(fixture.tools, 'enough', { decision: ' \n ' }, context())
  assert.ok(accepted.includes('use the existing result'))
  assert.notEqual(accepted, rejected)
  assert.equal(await tools.execute(tools.withoutJournal(toolModule), 'enough', { decision: 'use the existing result' }, context()), accepted)
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})

test('WHAT[ATTENTION-REGULATION-002] abandon releases only cognitive attention and never mutates obligations or authority', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'abandon', { commitment: 'drop the speculative branch' }, context())
  const rejected = await tools.execute(fixture.tools, 'abandon', { commitment: '' }, context())
  assert.ok(accepted.includes('drop the speculative branch'))
  assert.notEqual(accepted, rejected)
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})

test('WHAT[ATTENTION-REGULATION-003] defer creates pending work without creating execution or obligation state', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'defer', { new_work: '  investigate later  ' }, context())
  assert.ok(accepted.includes('investigate later'))
  assert.equal(fixture.reads, 1)
  assert.deepEqual(fixture.appends, [{ session: 'ses-a', providerRun: 'run-1', fact: { session: 'ses-a', occurrence: 'call-1', text: 'investigate later' } }])
  assert.deepEqual(attention.pending('ses-a', fixture.state), [{ occurrence: 'call-1', text: 'investigate later' }])

  const unavailable = await tools.execute(tools.withoutJournal(toolModule), 'defer', { new_work: 'later' }, context())
  const failed = recordingPort()
  failed.accept = false
  assert.equal(await tools.execute(failed.tools, 'defer', { new_work: 'later' }, context()), unavailable)
  assert.deepEqual(attention.pending('ses-a', failed.state), [])
  assert.equal(failed.appends.length, 1)

  const broken = recordingPort()
  broken.fault = new Error('append transport failed')
  await assert.rejects(tools.execute(broken.tools, 'defer', { new_work: 'later' }, context()), /append transport failed/)
  const brokenRead = tools.create(toolModule, () => { throw new Error('snapshot read failed') },
    async () => { assert.fail('a failed read must not append') })
  await assert.rejects(tools.execute(brokenRead, 'defer', { new_work: 'later' }, context()), /snapshot read failed/)

  const invalid = recordingPort()
  await tools.execute(invalid.tools, 'defer', { new_work: ' \n ' }, context())
  for (const ctx of [{ sessionID: 'ses-a' }, { sessionID: 'ses-a', callID: 'call-1' }, context('')]) {
    assert.equal(await tools.execute(invalid.tools, 'defer', { new_work: 'later' }, ctx), unavailable)
  }
  assert.equal(invalid.reads, 0)
  assert.deepEqual(invalid.appends, [])
})

test('WHAT[ATTENTION-REGULATION-004] deferred work is occurrence-idempotent and participant-life isolated', async () => {
  const fixture = recordingPort()
  await tools.execute(fixture.tools, 'defer', { new_work: 'one' }, context())
  await tools.execute(fixture.tools, 'defer', { new_work: 'do not replace one' }, context())
  await tools.execute(fixture.tools, 'defer', { new_work: 'two' }, context('ses-b'))
  assert.equal(fixture.appends.length, 2)
  assert.deepEqual(attention.pending('ses-a', fixture.state), [{ occurrence: 'call-1', text: 'one' }])
  assert.deepEqual(attention.pending('ses-b', fixture.state), [{ occurrence: 'call-1', text: 'two' }])
  fixture.state = attention.resurface('ses-a', 'celebration-1', ['call-1'], fixture.state)
  await tools.execute(fixture.tools, 'defer', { new_work: 'do not resurrect' }, context())
  assert.equal(fixture.appends.length, 2)
  assert.deepEqual(attention.pending('ses-a', fixture.state), [])
})

test('WHAT[ATTENTION-REGULATION-005] resurfacing consumes deferred visibility once without activating work', () => {
  let state = attention.empty()
  state = attention.record('ses-a', 'call-1', 'one', state)
  state = attention.record('ses-a', 'call-2', 'two', state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  assert.deepEqual(attention.pending('ses-a', state), [])

  const projection = read('src/Wanxiangshu/Interaction/Attention/Projection.fs')
  assert.doesNotMatch(projection, /StartWork|Activate|Delegate|Background/)
})

test('WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine', () => {
  assert.deepEqual(attention.pending('ses-a', attention.empty()), [])
  const source = [
    read('src/Wanxiangshu/Interaction/Attention/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Attention/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /\b(Stage|Priority|Deadline|DependencyGraph|AutoResume|BackgroundExecutor)\b/)
})

