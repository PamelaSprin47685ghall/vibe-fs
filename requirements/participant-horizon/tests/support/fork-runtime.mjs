import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as forkTool from '../../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(kind, extra),
  optional: () => schemaNode(kind, extra),
  int: () => schemaNode(kind, extra),
  nonnegative: () => schemaNode(kind, extra),
})
export const toolModule = { tool: { schema: {
  string: () => schemaNode('string'),
  number: () => schemaNode('number'),
  enum: (values) => schemaNode('enum', { values }),
  array: (inner) => schemaNode('array', { inner }),
} } }

export const withForkRuntime = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-horizon-contract-'))
  const owner = 'manager-horizon-contract'
  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    await body({ runtime, owner, directory })
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
}

export const placeEngineer = async (runtime, owner, byname) => {
  const pending = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', byname, 'VISIBLE-CHARGE')
  await forkTool.awaitPromptCount(runtime, 1)
  assert.equal(forkTool.acceptPrompt(runtime, 0), true)
  const result = await pending
  assert.ok(result.includes(byname))
  return forkTool.child(runtime)
}
