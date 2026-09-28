import * as blog from '../../../../dist/Enforcer/BlogSurface.js'
import { runtimeInstallFromPackage } from '../../../../dist/Resources/PromptSurface.js'

runtimeInstallFromPackage()
export const call = (overrides = {}) => ({
  type: 'tool', tool: 'chronicle', callID: 'call-1',
  state: { status: 'completed', input: { entry: '  work  ', tip: 'primitive-obsession', evidence: 'proof' } },
  ...overrides,
})
export const message = (parts, id = 'run-1') => ({
  info: { role: 'assistant', id, time: { completed: 1 } }, parts,
})
export const decode = (parts, id) => blog.decodeCycle([message(parts, id)])
