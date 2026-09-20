import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../../', import.meta.url).pathname

const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const VOCABULARY_SURFACES = {
  'Mission/Manager/Workflow': ['observe', 'observeIdle'],
  'Participant/Provider/Attempt/Fallback/Ledger': ['recordAuthorizedFailure'],
  'Participant/Provider/Attempt/Fallback/Workflow': ['continueAfterConfirmedFailure'],
  'Change/Program': ['run'],
}

// 代码常量形式的语义词汇证明义务注册表（迁自原 HOW.md §3.3 五列表格）
const SEMANTIC_VOCABULARY_REGISTRY = [
  {
    vocabulary: 'ManagerWorkflow.observe',
    ownerPath: 'relay / Mission.Manager / Mission/Manager/Workflow.fs',
    file: 'Mission/Manager/Workflow.fs',
    owner: 'Mission.Manager',
    whatLaw: 'structured-workflow-007',
    traceRelation: 'one admission → one settled/no-effect outcome',
    executableProof: 'requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof',
  },
  {
    vocabulary: 'ManagerWorkflow.observeIdle',
    ownerPath: 'relay / Mission.Manager / Mission/Manager/Workflow.fs',
    file: 'Mission/Manager/Workflow.fs',
    owner: 'Mission.Manager',
    whatLaw: 'structured-workflow-007',
    traceRelation: 'one idle observation → at most one encouragement',
    executableProof: 'requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof',
  },
  {
    vocabulary: 'FallbackLedger.recordAuthorizedFailure',
    ownerPath: 'provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs',
    file: 'Participant/Provider/Attempt/Fallback/Ledger.fs',
    owner: 'Participant.Provider',
    whatLaw: 'structured-workflow-007',
    traceRelation: 'one policy licence + duplicate observation → one durable cursor advance',
    executableProof: 'requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof',
  },
  {
    vocabulary: 'ProviderRecoveryWorkflow.continueAfterConfirmedFailure',
    ownerPath: 'provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs',
    file: 'Participant/Provider/Attempt/Fallback/Workflow.fs',
    owner: 'Participant.Provider',
    whatLaw: 'structured-workflow-008',
    traceRelation: 'R_fallback: confirmed failure → bounded ordinary CE re-entry',
    executableProof: 'requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary',
  },
  {
    vocabulary: 'OrchestratorProgram.run',
    ownerPath: 'change / Change / Change/Program.fs',
    file: 'Change/Program.fs',
    owner: 'Change',
    whatLaw: 'structured-workflow-008',
    traceRelation: 'R_publish: finite retry → one accepted or typed failed result',
    executableProof: 'requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[structured-workflow-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary',
  },
]

const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('WHAT[structured-workflow-007] SW_011_named_vocabulary_surface_exists_in_Application', () => {
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const source = readSrc(`src/Wanxiangshu/${modulePath}.fs`)
    for (const name of names) {
      assert.match(
        source,
        new RegExp(`\\blet(?: rec)?(?: private)? ${name}\\b`),
        `${modulePath} must define '${name}' as a let binding`,
      )
    }
  }
})

test('WHAT[structured-workflow-007] SW_011_vocabulary_names_declare_business_promises_not_implementation_actions', () => {
  const bad = []
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    for (const name of names) {
      if (REJECTED_PREFIX.test(name)) bad.push(`${modulePath}.${name}`)
    }
  }
  assert.deepEqual(bad, [], 'vocabulary names must not be implementation-action labels')
})

test('WHAT[structured-workflow-007] every vocabulary binds owner_law_relation_and_executable_proof', () => {
  const OBLIGATIONS = [
    ['ManagerWorkflow.observe', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['ManagerWorkflow.observeIdle', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['FallbackLedger.recordAuthorizedFailure', 'Participant/Provider/Attempt/Fallback/Ledger.fs', 'Participant.Provider'],
    ['ProviderRecoveryWorkflow.continueAfterConfirmedFailure', 'Participant/Provider/Attempt/Fallback/Workflow.fs', 'Participant.Provider'],
    ['OrchestratorProgram.run', 'Change/Program.fs', 'Change'],
  ]

  // 不再读取 HOW.md，直接使用代码常量注册表进行完整断言
  assert.equal(
    SEMANTIC_VOCABULARY_REGISTRY.length,
    OBLIGATIONS.length,
    'the obligation registry must contain exactly the registered vocabulary',
  )

  for (const [vocab, file, owner] of OBLIGATIONS) {
    const row = SEMANTIC_VOCABULARY_REGISTRY.find((entry) => entry.vocabulary === vocab)
    assert.ok(row, `Registry must register ${vocab}`)
    assert.ok(row.ownerPath.includes(owner) && row.ownerPath.includes(file), `${vocab} must name exact owner and source`)

    const whatId = row.whatLaw.replaceAll('`', '').toLowerCase()
    assert.ok(
      ['structured-workflow-007', 'structured-workflow-008'].includes(whatId),
      `${vocab} must bind its primary workflow law`,
    )
    assert.ok(row.traceRelation.length > 12, `${vocab} must declare a non-empty trace relation`)
    assert.ok(row.executableProof.length > 0, `${vocab} must declare an executable proof`)

    const short = vocab.split('.').pop()
    const production = readSrc(`src/Wanxiangshu/${file}`)
    assert.match(production, new RegExp(`\\blet(?: rec)?(?: private)? ${short}\\b`), `${vocab} must exist in ${file}`)
  }
})
