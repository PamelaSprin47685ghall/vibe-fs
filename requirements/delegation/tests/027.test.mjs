import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'



test('WHAT[DELEG-027] active fork assignment never becomes BusyAgentNudge', () => {
  const forkTool = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url),
    'utf8',
  )

  const active = forkTool.slice(forkTool.indexOf('let private reuseWhileActive'), forkTool.indexOf('let private commitIdleReuse'))
  assert.doesNotMatch(active, /runtime\.Reuse|BusyAgentNudge|ChargeCarried/)
})
