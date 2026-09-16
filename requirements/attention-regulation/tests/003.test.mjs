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
