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

test('WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine', () => {
  assert.deepEqual(attention.pending('ses-a', attention.empty()), [])
  const source = [
    read('src/Wanxiangshu/Interaction/Attention/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Attention/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /\b(Stage|Priority|Deadline|DependencyGraph|AutoResume|BackgroundExecutor)\b/)
})
