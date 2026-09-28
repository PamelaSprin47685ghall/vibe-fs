import { configure, installDefaultResources } from '../../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const collector = process.env.WANXIANGSHU_RETRY_CANARY_COLLECTOR
const emit = async (kind, value) => {
  const response = await fetch(collector, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ kind, value }),
  })
  if (!response.ok) throw new Error(`retry canary observation rejected: ${response.status}`)
}

export default {
  id: 'wanxiangshu-host-retry-canary',
  async server() {
    return {
      config: async config => {
        installDefaultResources()
        config.experimental ??= {}
        config.experimental.chatMaxRetries = 9
        const result = configure(config)
        if (!result.ok) throw new Error(result.error)
        await emit('config', { retries: config.experimental.chatMaxRetries })
      },
      event: async ({ event }) => {
        const properties = event.properties ?? {}
        if (event.type === 'message.updated') {
          const info = properties.info
          if (info?.role !== 'assistant') return
          await emit('assistant', {
            id: info.id, parent: info.parentID, session: info.sessionID,
            completed: info.time?.completed != null, errored: info.error != null,
          })
        } else if (event.type === 'session.error' || event.type === 'session.idle') {
          await emit(event.type, { session: properties.sessionID })
        }
      },
    }
  },
}
