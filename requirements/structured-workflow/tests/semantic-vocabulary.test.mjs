// requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
//
// DSL-013 / DSL-014 / DSL-015 (G4R-CE / rabbit.md): the business layer tells
// its story through named Semantic Vocabulary that declares a full business
// promise — never implementation-action names (execute/process/handle/...),
// never an anonymous middleware pipeline. The vocabulary lives in Application
// (shape/dsl-structured-program.md): it is not a Domain pure rule, not an
// Infrastructure adapter.

import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

/**
 * Production named-vocabulary surface, one entry per Application module.
 * Names are the DSL-013 contract: the call site must reveal what the caller
 * is waiting for from the name + arguments + return type alone.
 */
const VOCABULARY_SURFACES = {
  'Mission/Manager/Background': ['ensureSettled'],
  'Mission/Manager/Idle': ['encourageLabor'],
  'Mission/Manager/JobHandoff': ['completeIfTransferred'],
  'Mission/Review/Judgement/Continuation': ['ensurePerfectConfirmed', 'ensureVerdictSubmitted'],
  'Mission/Review/Judgement/Evidence': ['classifyNeed'],
  'Participant/Provider/Attempt/Fallback/Workflow': ['continueAfterConfirmedFailure'],
}

/** DSL-013 rejected shapes: implementation-action names, not business promises. */
const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('WHAT[STRUCTURED-WORKFLOW-011] SW_011_named_vocabulary_surface_exists_in_Application', async () => {
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    for (const name of names) {
      assert.equal(typeof mod[name], 'function', `${modulePath} must export '${name}'`)
    }
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] SW_011_vocabulary_names_declare_business_promises_not_implementation_actions', async () => {
  const bad = []
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    for (const name of names) {
      if (REJECTED_PREFIX.test(name)) bad.push(`${modulePath}.${name}`)
    }
  }
  assert.deepEqual(bad, [], 'vocabulary names must not be implementation-action labels')
})

test('WHAT[STRUCTURED-WORKFLOW-013] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary', async () => {
  // DSL-015: semantic decorators must be named Vocabulary or a named call
  // site. A global DecoratorBase / MiddlewarePipeline / IWorkflowDecorator
  // framework is banned. Assert the production vocabulary modules expose no
  // such framework shape.
  const frameworkNames = ['DecoratorBase', 'MiddlewarePipeline', 'IWorkflowDecorator', 'WorkflowBuilder']
  for (const modulePath of Object.keys(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    for (const name of frameworkNames) {
      assert.equal(name in mod, false, `${modulePath} must not export ${name}`)
    }
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] every obligation-table vocabulary is a real production definition', async () => {
  // DSL-014 / STRUCTURED-WORKFLOW-012: compressed Semantic Vocabulary must be
  // backed by its own temporal/behavioral proof. The proof obligation table
  // (HOW §3.4) is that registration: each row names a compressed vocabulary
  // and what it must prove. The machine floor is that the table is not a
  // fiction — every registered vocabulary must have a real definition in the
  // production tree (the compression actually happens at a named call site),
  // and the table must cover exactly the vocabularies it registers. A row
  // naming a nonexistent definition would be compression without proof.
  const root = new URL('../../../', import.meta.url).pathname

  const OBLIGATIONS = [
    ['ManagerBackground.ensureSettled', 'Mission/Manager/Background.fs'],
    ['ManagerIdle.encourageLabor', 'Mission/Manager/Idle.fs'],
    ['ReviewerContinuation.ensurePerfectConfirmed', 'Mission/Review/Judgement/Continuation.fs'],
    ['ReviewBarrierWorkflow.reverify', 'Mission/Review/Barrier/Reverify.fs'],
    ['FallbackLedger.recordConfirmedFailure', 'Participant/Provider/Attempt/Fallback/Ledger.fs'],
    ['ProviderRecoveryWorkflow.continueAfterConfirmedFailure', 'Participant/Provider/Attempt/Fallback/Workflow.fs'],
    ['FinalityCohort.reviewUntilFirstRevisionOrAllConfirmed', 'Mission/Finality/Cohort.fs'],
    ['SessionRecoveryWorkflow.recoverFamilyDirect', 'Execution/Session/Recovery/Workflow.fs'],
    ['Orchestrator.publishEventually', 'Change/Program.fs'],
  ]

  // Table completeness: HOW §3.4 registers exactly these nine.
  const how = readFileSync(join(root, 'requirements/structured-workflow/HOW.md'), 'utf8')
  const tableSection = how.slice(how.indexOf('### 3.4'), how.indexOf('### 3.4.1'))
  for (const [vocab] of OBLIGATIONS) {
    assert.match(tableSection, new RegExp(`\\| \`${vocab.replace('.', '\\.')}\``), `HOW §3.4 must register ${vocab}`)
  }

  // Every registered vocabulary is a real definition in production, at the
  // owner path the table names — the compression is observable, not fictional.
  for (const [vocab, file] of OBLIGATIONS) {
    const short = vocab.split('.').pop()
    const production = readFileSync(join(root, `src/Wanxiangshu/${file}`), 'utf8')
    assert.match(
      production,
      new RegExp(`\\blet(?: rec)?(?: private)? ${short}\\b`),
      `${vocab} must exist in production (${file})`,
    )
  }
})
