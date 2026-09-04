import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const root = new URL('../../../', import.meta.url).pathname
const read = (path) => readFileSync(join(root, path), 'utf8')

const workflowPath = 'src/Wanxiangshu/Mission/Obligation/LedgerWorkflow.fs'

test('WHAT[OBLIGATION-LEDGER-018] business sequencing is a direct F# CE, not a second runtime', () => {
  assert.equal(existsSync(join(root, workflowPath)), true, `${workflowPath} must own the business workflow`)
  const source = read(workflowPath)

  assert.match(source, /taskResult\s*\{|task\s*\{/, 'business sequencing is a direct F# CE (task/taskResult), not a second runtime')
  assert.match(source, /let!|match!|return!/)
  assert.doesNotMatch(source, /type\s+\w*(Command|Reply|Stage|Phase|NextAction|ProgramCounter)\b/)
  assert.doesNotMatch(source, /module\s+\w*Interpreter\b|\binterpret\b|\bfromTask\b|Flow\.lift/)
})

test('WHAT[OBLIGATION-LEDGER-018] hot-path queries use incremental projection facts, never AcceptedOrder replay', () => {
  const projection = read('src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs')
  for (const field of [
    'FirstAcceptedCheckpoint',
    'LatestAcceptedCheckpoint',
    'FirstPlanCommitment',
    'LatestCommittedCheckpoint',
    'PreviousCommittedCheckpoint',
  ]) {
    assert.match(projection, new RegExp(`\\b${field}\\b`), `projection must incrementally carry ${field}`)
  }

  assert.doesNotMatch(projection, /\bAcceptedOrder\b|\bAcceptedIds\b|\bacceptedOrder\b/, 'production projection no longer stores an accepted-history query chain')

  for (const path of [
    'src/Wanxiangshu/Mission/Manager/Workflow.fs',
    'src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs',
    'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs',
  ]) {
    assert.doesNotMatch(read(path), /\.AcceptedOrder\b|acceptedOrder\s+/, `${path} must consume O(1) projection queries`)
  }
})

test('WHAT[OBLIGATION-LEDGER-018] recovery contract is fact reentry, not a resumable workflow position', () => {
  const facts = read('src/Wanxiangshu/Mission/Obligation/Todo/Facts.fs')
  const projection = read('src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs')

  assert.doesNotMatch(facts, /PlanningStage|ReviewStage|NextAction|ResumeAt|ProgramCounter|AwaitingReview\s*:/)
  assert.doesNotMatch(projection, /PlanningStage|ReviewStage|NextAction|ResumeAt|ProgramCounter/)
  assert.match(projection, /foldPrepared/)
  assert.match(projection, /foldAccepted/)
})

test('WHAT[OBLIGATION-LEDGER-018] Manager authority root on incumbency opening is derived from durable Relay facts, not transient PromptAuthority profiles', () => {
  const fold = read('src/Wanxiangshu/Mission/Relay/Fold.fs')
  assert.match(
    fold,
    /AuthorityMessageIds = \[ authorityMessageId \]/,
    'AuthorityMessageIds on Road/Incumbency opening must be derived from durable authority facts',
  )
  assert.doesNotMatch(
    fold,
    /PromptAuthorityLedger/,
    'Relay Fold must not reconstruct authority root from transient PromptAuthorityLedger profile',
  )
})

test('WHAT[OBLIGATION-LEDGER-018] ObligationLedgerWorkflow is isolated from foreign domain dependencies', () => {
  const workflow = read('src/Wanxiangshu/Mission/Obligation/LedgerWorkflow.fs')
  assert.doesNotMatch(
    workflow,
    /open\s+Wanxiangshu\.(Change|Interaction|Mission\.Finality|Mission\.Manager|Mission\.Review|Mission\.WorkRecord|Participant|Strength)/,
    'ObligationLedgerWorkflow must not import foreign domains',
  )
})
