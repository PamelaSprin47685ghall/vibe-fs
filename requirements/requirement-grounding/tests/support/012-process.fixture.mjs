import assert from 'node:assert/strict'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { startPluginIncarnation, acceptAuthorityRoot, openIncumbency } from '../../../verification-system/tests/support/plugin-fixture.mjs'
import { JournalSurface_snapshot } from '../../../../dist/Persistence/Journal/Surface.js'

const [directory, phase, receiptPath, firstPath, appendedPath] = process.argv.slice(2)
const sessionID = 'grounding-independent-process'
let incarnation
try {
  incarnation = await startPluginIncarnation(directory)
  await incarnation.withRuntime(async runtime => {
    let raw = phase === 'produce' ? [] : JSON.parse(readFileSync(phase === 'replay-appended' ? appendedPath : firstPath, 'utf8')).raw
    const first = phase === 'produce' ? null : JSON.parse(readFileSync(firstPath, 'utf8'))
    if (phase === 'produce') {
      await acceptAuthorityRoot(runtime, sessionID, 'engineer')
      await openIncumbency(runtime, sessionID)
    }
    if (phase === 'produce' || phase === 'append') {
      const path = join(directory, 'src', 'main.fs')
      const callID = phase === 'produce' ? 'read-original' : 'read-changed'
      const args = { filePath: path }
      const input = { tool: 'read', sessionID, callID }
      await incarnation.hooks['tool.execute.before'](input, { args })
      const output = readFileSync(path, 'utf8')
      assert.equal(output, 'source\n')
      await incarnation.hooks['tool.execute.after']({ ...input, args }, { title: 'read', output, metadata: {} })
      raw = [...raw, { info: { id: `result-${callID}`, role: 'assistant', sessionID, providerID: 'anthropic' },
        parts: [{ id: `part-${callID}`, type: 'tool', tool: 'read', callID, state: { status: 'completed', input: args, output, time: { start: 0, end: 1 } } }] }]
    }
    const project = async () => {
      const projection = { messages: structuredClone(raw) }
      await incarnation.hooks['experimental.chat.messages.transform']({}, projection)
      return projection.messages
    }
    const projected = await project()
    if (phase === 'corrupt') throw new Error('Corrupted journal unexpectedly admitted replay')
    assert.equal(projected.length, raw.length)
    for (let index = 0; index < raw.length; index += 1) {
      assert.deepEqual(projected[index].info, raw[index].info)
      assert.equal(projected[index].parts[0].tool, raw[index].parts[0].tool)
      assert.equal(projected[index].parts[0].callID, raw[index].parts[0].callID)
      assert.deepEqual(projected[index].parts[0].state.input, raw[index].parts[0].state.input)
    }
    if (first) assert.deepEqual(projected.slice(0, first.projected.length), first.projected)
    const snapshot = JournalSurface_snapshot(runtime.journal)
    const session = snapshot.sessionProjections[sessionID]
    const receipt = { pid: process.pid, phase, raw, projected, occurrences: session.requirementGrounding.occurrenceCount }
    if (phase === 'append') receipt.repeated = await project()
    writeFileSync(receiptPath, JSON.stringify(receipt))
  })
} catch (error) {
  const message = error?.message
  if (phase !== 'corrupt' || typeof message !== 'string' || !message.startsWith('local EventStore boot failed: local event history read failed: MalformedEnvelope')) throw error
  writeFileSync(receiptPath, JSON.stringify({ pid: process.pid, phase, rejected: true, failure: 'MalformedEnvelope', error: message }))
} finally {
  await incarnation?.hooks.dispose()
}
