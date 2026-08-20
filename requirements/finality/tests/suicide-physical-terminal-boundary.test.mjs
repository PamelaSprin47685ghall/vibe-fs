import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const tool = readFileSync(join(root, 'src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs'), 'utf8')
const handoff = readFileSync(join(root, 'src/Wanxiangshu/Mission/Manager/JobHandoff.fs'), 'utf8')

test('WHAT[FINALITY-017] second suicide completes Life without publishing a provider terminal from the tool step', () => {
  const start = tool.indexOf('let private completeBlessedLife')
  const end = tool.indexOf('let private renderOutcome', start)
  assert.ok(start >= 0 && end > start, 'completeBlessedLife must remain a named finality boundary')

  const completion = tool.slice(start, end)
  assert.match(completion, /ManagerLifeWorkflow\.completeBlessedLife/)
  assert.match(completion, /restInPeaceInstructions/)
  assert.doesNotMatch(completion, /NotifyTerminal|TerminalOutcome|AgentRunResult/)
})

test('WHAT[FINALITY-017] archived Manager Life hands off only on the final completed turn', () => {
  assert.match(handoff, /let private managerLifeArchived[\s\S]*ManagerLifecycleProjection\.isLifeArchived/)

  const start = handoff.indexOf('let private isTransferred')
  const end = handoff.indexOf('let private completeInProgress', start)
  assert.ok(start >= 0 && end > start, 'Manager handoff transfer decision must remain named')

  const transfer = handoff.slice(start, end)
  assert.match(transfer, /\| ReconcileProgram\.TurnInProgress -> orchestrationOwnsTurn job/)
  assert.match(
    transfer,
    /\| ReconcileProgram\.TurnCompleted ->[\s\S]*managerLifeArchived journal sessionId/,
    'LifeCompleted may publish only through the true final completed turn',
  )
})
