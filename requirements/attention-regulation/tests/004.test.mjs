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
