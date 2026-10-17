import assert from 'node:assert/strict'
import { Trace } from 'wanxiangli/trace'
import * as trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'
import { withReopenableJournal } from './journal.mjs'

await withReopenableJournal(async (handle, reopen) => {
  const session = 's'.repeat(1200) + '\0\ud800'
  const providerRun = 'r'.repeat(1200) + '\0\ud801'
  const toolName = 't'.repeat(1200) + '\0\ud802'
  const messages = [
    { hostMessageId: 'm'.repeat(1000), message: { info: { role: 'user' }, parts: [{ id: 'p', type: 'text', text: 'historical original' }] } },
    { hostMessageId: providerRun, message: { info: { id: providerRun, role: 'assistant' }, parts: [{
      id: 'actual-part', type: 'tool', tool: toolName, callID: 'actual-call', state: { status: 'running', input: 'actual input' },
    }] } },
  ]
  const captured = await trace.captureObservedMessages(handle, session, messages)
  assert.equal(captured.ok, true, captured.error)
  assert.equal(captured.receipt.capturedPartCount, 2)
  await trace.captureTerminalText(handle, session, 'formal statement', providerRun)
  const projection = trace.snapshot(handle, session)
  const records = trace.traceRecords(projection)
  const head = trace.traceHeadCut(projection)
  const owner = new Trace(head.traceId, records)
  const view = owner.materialize(owner.cut(0), owner.head())
  assert.equal(view.materials[1].providerRun, providerRun)
  assert.equal(view.materials[1].toolName, toolName)
  assert.equal(view.terminals[0].providerRun, providerRun)
  const canonical = await trace.currentProjection(handle, session)
  const cold = await reopen()
  assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
  assert.deepEqual(await trace.currentProjection(cold, session), canonical)
  assert.equal((await trace.captureObservedMessages(cold, session, messages)).receipt.capturedPartCount, 0)
  console.log(JSON.stringify({ captured: captured.receipt.capturedPartCount, terminals: view.terminals.length, cold: true }))
})
