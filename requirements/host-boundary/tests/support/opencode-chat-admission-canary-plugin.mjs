const collectorUrl = process.env.WANXIANGSHU_CHAT_CANARY_COLLECTOR
if (!collectorUrl) throw new Error('WANXIANGSHU_CHAT_CANARY_COLLECTOR is required')

const emit = async (kind, value) => {
  const response = await fetch(collectorUrl, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ kind, value }),
  })
  if (!response.ok) throw new Error(`canary collector rejected ${kind}: ${response.status}`)
}

const keys = (value) => Object.keys(value ?? {}).sort()

const publicShape = (value, key = '') => {
  if (value === null) return null
  if (Array.isArray(value)) return value.map((item) => publicShape(item))
  if (typeof value === 'object') {
    return Object.fromEntries(keys(value).map((childKey) => [childKey, publicShape(value[childKey], childKey)]))
  }
  if (key === 'type' || key === 'name') return value
  return `<${typeof value}>`
}

export default {
  id: 'wanxiangshu-opencode-chat-admission-canary',
  async server() {
    return {
      'chat.message': async (input, output) => {
        await emit('chat.message', {
          input: {
            keys: keys(input),
            sessionID: input.sessionID,
            messageID: input.messageID ?? null,
          },
          output: {
            keys: keys(output),
            messageKeys: keys(output.message),
            messageID: output.message?.id ?? null,
            sessionID: output.message?.sessionID ?? null,
            partCount: output.parts?.length ?? null,
          },
        })
        if (output.parts?.some((part) => part?.type === 'text' && part.text.includes('CANARY_REJECT'))) {
          await emit('chat.message.rejection', { sessionID: input.sessionID, messageID: input.messageID ?? null })
          return Promise.reject(new Error('public chat.message canary rejection'))
        }
      },
      'chat.params': async (input) => {
        await emit('chat.params', {
          inputKeys: keys(input),
          sessionID: input.sessionID,
          messageID: input.message?.id ?? null,
          messageSessionID: input.message?.sessionID ?? null,
          modelKeys: keys(input.model),
          modelID: input.model?.id ?? null,
          providerKeys: keys(input.provider),
          providerInfoKeys: keys(input.provider?.info),
        })
      },
      'experimental.chat.messages.transform': async (_input, output) => {
        const latest = output.messages?.at(-1)?.info
        await emit('experimental.chat.messages.transform', {
          messageID: latest?.id ?? null,
          sessionID: latest?.sessionID ?? null,
          role: latest?.role ?? null,
        })
      },
      event: async ({ event }) => {
        if (event.type === 'message.updated') {
          const info = event.properties?.info
          await emit(event.type, {
            keys: keys(event),
            propertyKeys: keys(event.properties),
            infoKeys: keys(info),
            info: {
              id: info?.id ?? null,
              sessionID: info?.sessionID ?? event.properties?.sessionID ?? null,
              parentID: info?.parentID ?? null,
              role: info?.role ?? null,
              timeKeys: keys(info?.time),
              created: info?.time?.created ?? null,
              completed: info?.time?.completed ?? null,
              finish: info?.finish ?? null,
            },
          })
          return
        }
        if (event.type !== 'session.idle' && event.type !== 'session.error') return
        await emit(event.type, {
          keys: keys(event),
          properties: publicShape(event.properties),
          propertyKeys: keys(event.properties),
        })
      },
    }
  },
}
