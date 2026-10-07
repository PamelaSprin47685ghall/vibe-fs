import { pathToFileURL } from 'node:url'

const { default: production } = await import(pathToFileURL(process.env.WXS_USER_INPUT_PLUGIN).href)
const collector = process.env.WXS_USER_INPUT_COLLECTOR
const emit = async (kind, value) => {
  const response = await fetch(collector, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ kind, value }),
  })
  if (!response.ok) throw new Error(`collector rejected ${kind}: ${response.status}`)
}

export default {
  id: 'wanxiangshu-user-input-canary',
  async server(input) {
    const hooks = await production.server(input)
    const abort = input.client.session.abort.bind(input.client.session)
    input.client.session.abort = async (...args) => {
      await emit('host.abort', { sessionID: args[0]?.path?.id ?? args[0]?.sessionID })
      return abort(...args)
    }
    return {
      ...hooks,
      tool: {
        ...hooks.tool,
        'js-manager': {
          ...hooks.tool['js-manager'],
          execute: async (args, context) => {
            if (args.program.includes('USER_INPUT_HELD_TOOL')) {
              await emit('tool.held', { sessionID: context.sessionID, callID: context.callID })
              const response = await fetch(`${collector}/release/tool`)
              if (!response.ok) throw new Error('tool release failed')
              if (context.abort.aborted) throw new Error('user input cancelled the non-join tool')
            }
            return hooks.tool['js-manager'].execute(args, context)
          },
        },
      },
      'chat.headers': async (request, output) => {
        await hooks['chat.headers']?.(request, output)
        output.headers['x-wxs-canary-session'] = request.sessionID
        output.headers['x-wxs-canary-message'] = request.message.id
      },
      'chat.message': async (request, output) => {
        await emit('message.received', {
          sessionID: request.sessionID, messageID: request.messageID ?? output.message.id,
        })
        await hooks['chat.message'](request, output)
        await emit('message.accepted', {
          sessionID: request.sessionID, messageID: request.messageID ?? output.message.id,
        })
      },
      event: async (request) => {
        await hooks.event?.(request)
        if (['message.updated', 'session.error', 'session.idle'].includes(request.event?.type)) {
          await emit('event.completed', request.event)
        }
      },
    }
  },
}
