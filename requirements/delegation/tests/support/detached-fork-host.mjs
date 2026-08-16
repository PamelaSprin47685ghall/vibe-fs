const { OpenCodePort_SdkClientPort: SdkClientPort } = await import('../../../../dist/OpenCode/Host/OpenCodePort.js')
const { InjectedSessionPort } = await import('../../../../dist/OpenCode/Host/Sessions.js')

/**
 * Real OpenCode session adapter whose Host prompt_async Promise never settles
 * until the test explicitly releases it. This keeps Fable/SDK representation
 * knowledge out of the semantic test while exercising the production boundary.
 */
export const nonSettlingForkSessions = () => {
  let releasePrompt
  const hostRun = new Promise((resolve) => {
    releasePrompt = resolve
  })
  const sent = []
  const client = {
    session: {
      create: async () => ({ data: { id: 'child-deep-devops' } }),
      promptAsync: (payload) => {
        sent.push(payload)
        return hostRun
      },
      abort: async () => ({}),
    },
  }
  const eventPort = {
    SubscribeTerminalListener: () => ({ Dispose: () => {} }),
  }

  return {
    sessions: new InjectedSessionPort(new SdkClientPort(client, undefined), eventPort),
    sent,
    release: () => releasePrompt?.({}),
  }
}
