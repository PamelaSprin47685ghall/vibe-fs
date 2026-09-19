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

test('WHAT[attention-regulation-002] abandon releases only cognitive attention and never mutates obligations or authority', async () => {
  const fixture = recordingPort()
  const accepted = await tools.execute(fixture.tools, 'abandon', { commitment: 'drop the speculative branch' }, context())
  const rejected = await tools.execute(fixture.tools, 'abandon', { commitment: '' }, context())
  assert.ok(accepted.includes('drop the speculative branch'))
  assert.notEqual(accepted, rejected)
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})
