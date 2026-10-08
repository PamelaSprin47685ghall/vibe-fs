const collectorUrl = process.env.WXS_NATIVE_READ_CANARY_COLLECTOR
if (!collectorUrl) throw new Error('WXS_NATIVE_READ_CANARY_COLLECTOR is required')

export default {
  id: 'wanxiangshu-native-read-canary-observer',
  async server() {
    return {
      'tool.execute.after': async (input, output) => {
        if (input.tool !== 'read') return
        const response = await fetch(collectorUrl, {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ input, output }),
        })
        if (!response.ok) throw new Error(`native read collector rejected observation: ${response.status}`)
      },
    }
  },
}
