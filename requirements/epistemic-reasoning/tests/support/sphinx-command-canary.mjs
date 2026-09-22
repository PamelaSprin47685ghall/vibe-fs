import { before, configure } from '../../../../dist/OpenCode/Plugin/SphinxCommandSurface.js'
import { installDefaultResources } from '../../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

export default {
  id: 'sphinx-command-canary',
  server: async ({ client, directory }) => {
    installDefaultResources()
    return {
      config: configure,
      'command.execute.before': (input, output) => before(async (_session, question) => ({
        question, synthesis: { text: 'SPHINX_NATIVE_ANSWER', findingKeys: [] },
        epistemicBasis: { findings: [], evidence: [], hypotheses: [] },
        uncertainties: [], stopReason: 'policy-exhausted', revision: 0,
      }), client, directory, input, output),
    }
  },
}
