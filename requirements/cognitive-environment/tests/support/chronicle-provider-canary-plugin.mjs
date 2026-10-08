import { pathToFileURL } from 'node:url'

const { default: productionPlugin } = await import(pathToFileURL(process.env.WXS_CHRONICLE_PRODUCTION_PLUGIN).href)

export default {
  id: 'wanxiangshu-chronicle-provider-canary',
  async server(input) {
    const hooks = await productionPlugin.server(input)
    return {
      ...hooks,
      async config(config) {
        const model = config.provider.test.models['test-model']
        config.provider.test.models['step-3.5-flash-canary'] = { ...model, id: 'step-3.5-flash-canary' }
        config.agent.blogger.model = 'test/step-3.5-flash-canary'
        await hooks.config(config)
      },
    }
  },
}
