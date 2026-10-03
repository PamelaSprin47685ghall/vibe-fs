import assert from 'node:assert/strict'
import { acceptAuthorityRoot, activateLife } from '../../../verification-system/tests/support/plugin-fixture.mjs'

export const admit = async (runtime, session, role = 'engineer') => {
  await acceptAuthorityRoot(runtime, session, role, `root-${session}`)
  await activateLife(runtime, session, `root-${session}`)
}
export const context = (sessionID, callID, agent = 'engineer') => ({ sessionID, callID, agent, messageID: `run-${sessionID}` })
export const user = (sessionID, messageID = `root-${sessionID}`) => ({ info: { id: messageID, sessionID, role: 'user', model: { providerID: 'anthropic', modelID: 'fixture' } }, parts: [{ type: 'text', text: 'Investigate current facts.' }] })
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
export const transform = async (hooks, host, sessionID, messages) => {
  const output = { messages: structuredClone(messages) }
  // Each new SDK physical message crosses chat.message once. Historical
  // replay keeps the stored ingress; admission errors propagate unchanged.
  for (const message of output.messages) {
    if (message.info?.role === 'user') {
      const physicalID = message.info.id
      const hasPhysical = host.messages.some(candidate =>
        (candidate.info ?? candidate).sessionID === sessionID &&
        (candidate.info ?? candidate).id === physicalID)
      if (!hasPhysical) {
        await hooks['chat.message'](
          { sessionID, messageID: physicalID },
          { message: message.info, parts: message.parts },
        )
        host.pushHostMessage(sessionID, structuredClone(message))
      }
      const providerID = `provider-${physicalID}`
      const hasProvider = host.messages.some(candidate =>
        (candidate.info ?? candidate).sessionID === sessionID &&
        (candidate.info ?? candidate).id === providerID)
      if (!hasProvider) {
        // The SDK creates this exact unfinished assistant before requesting
        // its provider body; transform must confirm durable ProviderStarted.
        host.pushHostMessage(sessionID, {
          info: { id: providerID, sessionID, role: 'assistant', parentID: physicalID, time: { created: host.messages.length + 1 } },
          parts: [],
        })
      }
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
