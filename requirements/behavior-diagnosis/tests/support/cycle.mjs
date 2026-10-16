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
      whatToDoNow: '  take the next concrete step  ',
      whyThisMatters: '  the boundary decides later work  ',
      repairStrategy: '  smallest complete repair first  ',
      decisionBranches: '  follow the branch that fits  ',
      commonWrongFixes: '  skipping the boundary fails later  ',
      verification: '  the check must catch a wrong fix  ',
      doneWhen: '  the boundary holds under the check  ',
    },
  },
  ...overrides,
})
export const message = (parts, id = 'run-1') => ({
  info: { role: 'assistant', id, time: { completed: 1 } }, parts,
})
export const decode = (parts, id) => blog.decodeCycle([message(parts, id)])
