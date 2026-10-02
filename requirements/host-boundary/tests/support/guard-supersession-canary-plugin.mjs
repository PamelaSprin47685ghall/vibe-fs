import { pathToFileURL } from 'node:url'

const { default: production } = await import(pathToFileURL(process.env.WXS_SUPERSESSION_PLUGIN).href)
const collector = process.env.WXS_SUPERSESSION_COLLECTOR
const emit = async (kind, value) => {
  const response = await fetch(collector, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ kind, value }),
  })
  if (!response.ok) throw new Error(`collector rejected ${kind}: ${response.status}`)
}

export default {
  id: 'wanxiangshu-guard-supersession-canary',
  async server(input) {
    const hooks = await production.server(input)
    const guards = new Map()
    const pausedSessions = new Set()
    const paused = new Set()
    return {
      ...hooks,
      'chat.headers': async (request, output) => {
        await hooks['chat.headers']?.(request, output)
        output.headers['x-wxs-canary-session'] = request.sessionID
        output.headers['x-wxs-canary-message'] = request.message.id
      },
      'chat.message': async (request, output) => {
        const metadata = output.parts?.find(part => part.metadata?.wanxiangshu_origin)?.metadata
        const value = {
          sessionID: request.sessionID, messageID: request.messageID ?? output.message.id,
          origin: metadata?.wanxiangshu_origin ?? 'HumanMessage',
          text: output.parts?.filter(part => part.type === 'text').map(part => part.text).join('\n'),
        }
        if (value.text?.includes('SUPERSESSION_PAUSED_ROOT') || value.text?.includes('SUPERSESSION_INTERLEAVED_ROOT')) pausedSessions.add(value.sessionID)
        if (value.origin === 'ManagerGuard' && !guards.has(value.sessionID)) guards.set(value.sessionID, value.messageID)
        await emit('message.received', value)
        await hooks['chat.message'](request, output)
        await emit('message.accepted', value)
        if (value.text === 'SUPERSESSION_HUMAN_WAIT') {
          await emit('human.paused', value)
          const response = await fetch(`${collector}/release/${value.messageID}`)
          if (!response.ok) throw new Error('human release failed')
        }
      },
      'experimental.chat.messages.transform': async (request, output) => {
        const user = output.messages?.findLast(row => row.info?.role === 'user')?.info
        const guard = guards.get(user?.sessionID)
        if (guard === user?.id && !paused.has(guard) && pausedSessions.has(user.sessionID)) {
          paused.add(guard)
          await emit('guard.paused', { sessionID: user.sessionID, messageID: guard })
          const response = await fetch(`${collector}/release/${guard}`)
          if (!response.ok) throw new Error('guard release failed')
          try {
            await hooks['experimental.chat.messages.transform'](request, output)
          } catch (error) {
            await emit('guard.late.rejected', { sessionID: user.sessionID, messageID: guard, error: String(error) })
            throw error
          }
          await emit('guard.late.completed', { sessionID: user.sessionID, messageID: guard })
          return
        }
        return hooks['experimental.chat.messages.transform'](request, output)
      },
      event: async (request) => {
        await hooks.event?.(request)
        const event = request.event
        if (['message.updated', 'session.error', 'session.idle'].includes(event?.type)) {
          await emit('event.completed', event)
        }
      },
    }
  },
}
