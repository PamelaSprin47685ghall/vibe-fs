// requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
//
// DSL-013 / DSL-014 / DSL-015 (G4R-CE / rabbit.md): the business layer tells
// its story through named Semantic Vocabulary that declares a full business
// promise — never implementation-action names (execute/process/handle/...),
// never an anonymous middleware pipeline. The vocabulary lives in Application
// (shape/dsl-structured-program.md): it is not a Domain pure rule, not an
// Infrastructure adapter.
//
// Source-tree proof: each vocabulary name is a `let` in its owner Application
// module. Build-verification (guide-contract.test.mjs) proves the emitted
// modules load and the names are callable. This semantic test proves the
// naming contract without loading internal dist modules.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { buildTraceGraph } from '../../../scripts/lib/requirement-trace.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname
const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

/**
 * Production named-vocabulary surface, one entry per Application module.
 * Names are the DSL-013 contract: the call site must reveal what the caller
 * is waiting for from the name + arguments + return type alone.
 */
const VOCABULARY_SURFACES = {
  'Mission/Manager/Workflow': ['observe', 'observeIdle'],
  'Participant/Provider/Attempt/Fallback/Ledger': ['recordAuthorizedFailure'],
  'Participant/Provider/Attempt/Fallback/Workflow': ['continueAfterConfirmedFailure'],
  'Execution/Session/Recovery/Workflow': ['recoverFamilyDirect'],
  'Change/Program': ['run'],
}

/** DSL-013 rejected shapes: implementation-action names, not business promises. */
const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('WHAT[STRUCTURED-WORKFLOW-007] SW_011_named_vocabulary_surface_exists_in_Application', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-007] SW_011_vocabulary_names_declare_business_promises_not_implementation_actions', () => {
  const bad = []
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    for (const name of names) {
      if (REJECTED_PREFIX.test(name)) bad.push(`${modulePath}.${name}`)
    }
  }
  assert.deepEqual(bad, [], 'vocabulary names must not be implementation-action labels')
})

test('WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary', () => {
  // DSL-015: semantic decorators must be named Vocabulary or a named call
  // site. A global DecoratorBase / MiddlewarePipeline / IWorkflowDecorator
  // framework is banned. Assert the production vocabulary modules define no
  // such framework shape.
  const frameworkNames = ['DecoratorBase', 'MiddlewarePipeline', 'IWorkflowDecorator', 'WorkflowBuilder']
  const bad = []
  for (const modulePath of Object.keys(VOCABULARY_SURFACES)) {
    const source = readSrc(`src/Wanxiangshu/${modulePath}.fs`)
    for (const name of frameworkNames) {
      if (new RegExp(`\\b(?:type|let) ${name}\\b`).test(source)) {
        bad.push(`${modulePath} must not define ${name}`)
      }
    }
  }
  assert.deepEqual(bad, [], bad.join('; '))
})

test('WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof', () => {
  const OBLIGATIONS = [
    ['ManagerWorkflow.observe', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['ManagerWorkflow.observeIdle', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['FallbackLedger.recordAuthorizedFailure', 'Participant/Provider/Attempt/Fallback/Ledger.fs', 'Participant.Provider'],
    ['ProviderRecoveryWorkflow.continueAfterConfirmedFailure', 'Participant/Provider/Attempt/Fallback/Workflow.fs', 'Participant.Provider'],
    ['SessionRecoveryWorkflow.recoverFamilyDirect', 'Execution/Session/Recovery/Workflow.fs', 'Execution.Session'],
    ['OrchestratorProgram.run', 'Change/Program.fs', 'Change'],
  ]

  const howPath = join(ROOT, 'requirements/structured-workflow/HOW.md')
  const how = readFileSync(howPath, 'utf8')
  const lines = how.split('\n')
  const tableStart = lines.findIndex((line) => line.startsWith('### 3.3 '))
  const tableEnd = lines.findIndex((line) => line.startsWith('### 3.3.1'))
  const rows = lines
    .slice(tableStart + 1, tableEnd)
    .map((text, offset) => ({ text, line: tableStart + offset + 2 }))
    .filter(({ text }) => text.startsWith('| `'))
  assert.equal(rows.length, OBLIGATIONS.length, 'the obligation table must contain exactly the registered vocabulary')

  const graph = buildTraceGraph(join(ROOT, 'requirements'))
  for (const [vocab, file, owner] of OBLIGATIONS) {
    const row = rows.find(({ text }) => text.includes(`\`${vocab}\``))
    assert.ok(row, `HOW §3.3 must register ${vocab}`)
    const columns = row.text.split('|').map((column) => column.trim()).filter(Boolean)
    assert.equal(columns.length, 5, `${vocab} must bind vocabulary, owner/path, WHAT, relation, proof`)
    assert.ok(columns[1].includes(owner) && columns[1].includes(file), `${vocab} must name exact owner and source`)

    const whatId = columns[2].replaceAll('`', '')
    assert.ok(['STRUCTURED-WORKFLOW-007', 'STRUCTURED-WORKFLOW-008'].includes(whatId), `${vocab} must bind its primary workflow law`)
    assert.equal(graph.whats.get(whatId)?.package, 'structured-workflow', `${vocab} law must belong to the semantic-vocabulary owner`)
    assert.ok(columns[3].length > 12, `${vocab} must declare a non-empty trace relation`)

    const proofEdges = graph.proofEdges.filter(
      (edge) => edge.proofFile === howPath && edge.proofLine === row.line && edge.whatId === whatId && edge.state === 'active',
    )
    assert.equal(proofEdges.length, 1, `${vocab} must resolve one exact active proof-title edge for ${whatId}`)
    assert.ok(proofEdges[0].title && proofEdges[0].file.endsWith('.test.mjs'), `${vocab} proof edge must resolve to an executable test`)

    const short = vocab.split('.').pop()
    const production = readSrc(`src/Wanxiangshu/${file}`)
    assert.match(production, new RegExp(`\\blet(?: rec)?(?: private)? ${short}\\b`), `${vocab} must exist in ${file}`)
  }
})
