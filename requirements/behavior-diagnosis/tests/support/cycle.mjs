import * as blog from '../../../../dist/Enforcer/BlogSurface.js'
import { runtimeInstallFromPackage } from '../../../../dist/Resources/PromptSurface.js'

runtimeInstallFromPackage()
export const call = (overrides = {}) => ({
  type: 'tool', tool: 'chronicle', callID: 'call-1',
  state: {
    status: 'completed',
    input: {
      charge: '  resolve the current question  ',
      occurrence: '  work  ',
      settlement: '  result settled  ',
      consequence: '  continue on the settled path  ',
      tipName: 'primitive-obsession',
      opinion: '  the boundary decides later work  ',
      advice: '  smallest complete repair first  ',
    },
  },
  ...overrides,
})
export const message = (parts, id = 'run-1') => ({
  info: { role: 'assistant', id, time: { completed: 1 } }, parts,
})
export const decode = (parts, id) => blog.decodeCycle([message(parts, id)])
