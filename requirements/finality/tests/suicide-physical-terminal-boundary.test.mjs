import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const tool = readFileSync(join(root, 'src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs'), 'utf8')

test('WHAT[FINALITY-017] second suicide completes Life without publishing a provider terminal from the tool step', () => {
  const start = tool.indexOf('let private completeBlessedLife')
  const end = tool.indexOf('let private renderOutcome', start)
  assert.ok(start >= 0 && end > start, 'completeBlessedLife must remain a named finality boundary')

  const completion = tool.slice(start, end)
  assert.match(completion, /ManagerLifeWorkflow\.completeBlessedLife/)
  assert.match(completion, /restInPeaceInstructions/)
  assert.doesNotMatch(completion, /NotifyTerminal|TerminalOutcome|AgentRunResult/)
})
