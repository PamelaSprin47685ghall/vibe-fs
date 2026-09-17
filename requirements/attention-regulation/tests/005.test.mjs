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
