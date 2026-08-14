import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const root = new URL('../../../', import.meta.url).pathname
const read = (path) => readFileSync(join(root, path), 'utf8')

const workflowPath = 'src/Wanxiangshu/Application/Manager/ObligationLedgerWorkflow.fs'

test('OBLIGATION_LEDGER_018 business sequencing is a direct F# CE, not a second runtime', () => {
  assert.equal(existsSync(join(root, workflowPath)), true, `${workflowPath} must own the business workflow`)
  const source = read(workflowPath)

  assert.match(source, /task\s*\{/)
  assert.match(source, /let!|match!|return!/)
  assert.doesNotMatch(source, /type\s+\w*(Command|Reply|Stage|Phase|NextAction|ProgramCounter)\b/)
  assert.doesNotMatch(source, /module\s+\w*Interpreter\b|\binterpret\b|\bfromTask\b|Flow\.lift/)
})

test('OBLIGATION_LEDGER_018 hot-path queries use incremental projection facts, never AcceptedOrder replay', () => {
  const projection = read('src/Wanxiangshu/Journal/MagicTodoProjection.fs')
  for (const field of [
    'FirstAcceptedCheckpoint',
    'LatestAcceptedCheckpoint',
    'PendingReviewCheckpoint',
    'FirstPlanCommitment',
    'LatestCommittedCheckpoint',
    'PreviousCommittedCheckpoint',
    'ReviewerLifeBySession',
  ]) {
    assert.match(projection, new RegExp(`\\b${field}\\b`), `projection must incrementally carry ${field}`)
  }

  assert.doesNotMatch(projection, /\bAcceptedOrder\b|\bAcceptedIds\b|\bacceptedOrder\b/, 'production projection no longer stores an accepted-history query chain')
  assert.doesNotMatch(projection, /ByLife\s*\|>\s*Map\.tryPick|Map\.tryPick/, 'reviewer authority lookup must use ReviewerLifeBySession rather than scan every Life')

  for (const path of [
    'src/Wanxiangshu/Journal/ManagerOpeningFloor.fs',
    'src/Wanxiangshu/Application/Manager/ManagerIdle.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityTool.fs',
    'src/Wanxiangshu/Application/Reconciliation/MagicTodoMembrane.fs',
    'src/Wanxiangshu/Application/Review/DedicatedTodoReviewerRuntime.fs',
  ]) {
    assert.doesNotMatch(read(path), /\.AcceptedOrder\b|acceptedOrder\s+/, `${path} must consume O(1) projection queries`)
  }
})

test('OBLIGATION_LEDGER_018 recovery contract is fact reentry, not a resumable workflow position', () => {
  const facts = read('src/Wanxiangshu/Domain/MagicTodoFacts.fs')
  const projection = read('src/Wanxiangshu/Journal/MagicTodoProjection.fs')

  assert.doesNotMatch(facts, /PlanningStage|ReviewStage|NextAction|ResumeAt|ProgramCounter|AwaitingReview\s*:/)
  assert.doesNotMatch(projection, /PlanningStage|ReviewStage|NextAction|ResumeAt|ProgramCounter/)
  assert.match(projection, /foldPrepared/)
  assert.match(projection, /foldAccepted/)
})
