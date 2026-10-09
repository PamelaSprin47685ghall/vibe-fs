import assert from 'node:assert/strict'
import test from 'node:test'
import * as tools from '../../../dist/OpenCode/Tools/AttentionToolSurface.js'
import { recordingPort, context, toolModule } from './support/attention-port.mjs'

test('WHAT[attention-regulation-001] enough accepts nonblank decisions without reading or appending attention state', async () => {
  const fixture = recordingPort()
  const args = { decision: '  use the existing result  ' }
  const accepted = await tools.execute(fixture.tools, 'enough', args, context())
  assert.ok(accepted.includes('use the existing result'))
  assert.equal(await tools.execute(fixture.tools, 'enough', args, context()), accepted)
  const rejected = await tools.execute(fixture.tools, 'enough', { decision: ' \n ' }, context())
  assert.notEqual(accepted, rejected)
  assert.equal(await tools.execute(tools.withoutJournal(toolModule), 'enough', args, context()), accepted)
  assert.equal(fixture.reads, 0)
  assert.deepEqual(fixture.appends, [])
})

test.todo('WHAT[attention-regulation-001] GAP-118 review prompt meaning and actual participant behavior at materially new facts; no word scan proves cognitive stopping')
