import { run as executeRun, queryShell as executeQueryShell, describeRun, describeQueryShell } from '../../../../dist/OpenCode/Tools/ExecutorToolSurface.js'

const schema = kind => ({ kind, describe() { return this }, optional() { return this } })
export const toolModule = { tool: { schema: {
  string: () => schema('string'),
  number: () => schema('number'),
  boolean: () => schema('boolean'),
} } }
export const context = { sessionID: 'ses-process-contract' }
export const run = (args, options = {}) => executeRun(toolModule, options.sessions ?? {}, args, options.context ?? context, options.recovery ?? 'ready')
export const queryShell = (args, options = {}) => executeQueryShell(toolModule, options.sessions ?? {}, args, options.context ?? context, options.recovery ?? 'ready')
export const describe = () => ({ run: describeRun(toolModule), queryShell: describeQueryShell(toolModule) })
export const shellQuote = value => `'${value.replaceAll("'", "'\\''")}'`
export const nodeCommand = code => `${shellQuote(process.execPath)} -e ${shellQuote(code)}`
