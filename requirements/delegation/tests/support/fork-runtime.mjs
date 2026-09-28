import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as forkTool from '../../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

const schemaNode = (kind, extra = {}) => ({
  kind, ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})

export const toolModule = { tool: { schema: {
  string: () => schemaNode('string'),
  number: () => schemaNode('number'),
  enum: values => schemaNode('enum', { values }),
  array: inner => schemaNode('array', { inner }),
} } }

export async function withForkRuntime(owner, run) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-delegation-fork-'))
  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    return await run(runtime)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
}
