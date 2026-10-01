import assert from 'node:assert/strict'
import { acceptAuthorityRoot, activateLife } from '../../../verification-system/tests/support/plugin-fixture.mjs'

export const admit = async (runtime, session, role = 'engineer') => {
  await acceptAuthorityRoot(runtime, session, role, `root-${session}`)
  await activateLife(runtime, session, `root-${session}`)
}
export const context = (sessionID, callID, agent = 'engineer') => ({ sessionID, callID, agent, messageID: `run-${sessionID}` })
export const user = sessionID => ({ info: { id: `root-${sessionID}`, sessionID, role: 'user', model: { providerID: 'anthropic', modelID: 'fixture' } }, parts: [{ type: 'text', text: 'Investigate current facts.' }] })
export const toolBatch = (sessionID, suffix) => ['pending', 'completed'].map(status => ({
  info: { id: `${status}-${suffix}`, sessionID, role: 'assistant', providerID: 'anthropic' },
  parts: [{
    type: 'tool', tool: 'read', callID: `read-${suffix}`,
    state: {
      status, input: { filePath: 'README.md' },
      ...(status === 'completed' ? { output: 'observed file', time: { start: 0, end: 0 } } : { time: { start: 0 } }),
    },
  }],
}))
export const transform = async (hooks, sessionID, messages) => {
  const output = { messages: structuredClone(messages) }
  // HOST-BOUNDARY-008: the provider attempt plan freeze requires an accepted
  // execution, which chat.message establishes for the physical user message.
  for (const message of output.messages) {
    if (message.info?.role === 'user') {
      await hooks['chat.message'](
        { sessionID, messageID: message.info.id },
        { message: message.info, parts: message.parts },
      )
    }
  }
  await hooks['experimental.chat.messages.transform']({ sessionID }, output)
  assert.ok(Array.isArray(output.messages))
  return output.messages
}
export const hints = messages => messages.flatMap(message => message.parts.flatMap(part => {
  const content = part.state?.output ?? part.state?.error ?? part.text ?? ''
  return typeof content === 'string' ? content.split('\0\uFEFF').slice(1) : []
}))
