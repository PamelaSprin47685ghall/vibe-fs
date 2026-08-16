const sessionsModule = await import('../../../../dist/OpenCode/Host/Sessions.js')
const { SessionIdModule_create: sessionId } = await import('../../../../dist/Foundation/Identity.js')

const createPort = Object.entries(sessionsModule).find(([name]) => name.startsWith('InjectedSessionPort_$ctor'))?.[1]
if (typeof createPort !== 'function') throw new Error('InjectedSessionPort constructor missing')

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }
const sendOptions = (agent) => ({
  Model: undefined,
  Agent: agent,
  Directory: undefined,
  Metadata: undefined,
  Tools: undefined,
  BindingIntent: preserve,
})

export const runListenerRefcountScenario = async () => {
  const child = sessionId('ses_listener_refcount_child')
  let sends = 0
  const port = createPort(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [child] }),
      SendPrompt: async () => {
        sends += 1
        return { tag: 0, fields: [{ fields: [`accepted-${sends}`] }] }
      },
    },
    eventPort,
  )
  const created = await port.CreateChildSession(sessionId('ses_listener_refcount_parent'), { Agent: 'deep-coder' })
  if (created.tag !== 0) throw new Error(`fixture child creation failed: ${JSON.stringify(created.fields?.[0])}`)

  const realRunListener = port.SubscribeTerminal(child, () => {})
  const temporaryDispatcherListener = port.SubscribeTerminal(child, () => {})
  temporaryDispatcherListener.Dispose()
  const afterOneDispose = await port.SendPrompt(child, 'after temporary dispose', sendOptions('deep-coder'))

  realRunListener.Dispose()
  const afterAllDispose = await port.SendPrompt(child, 'after all dispose', sendOptions('deep-coder'))

  return {
    afterOneDisposeFatal: afterOneDispose.tag === 4,
    afterAllDisposeFatal: afterAllDispose.tag === 4,
    afterAllDisposeMessage: afterAllDispose.tag === 4 ? afterAllDispose.fields[0] : '',
    sends,
  }
}
