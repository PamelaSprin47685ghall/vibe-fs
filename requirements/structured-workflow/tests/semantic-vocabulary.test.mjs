// requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
//
// DSL-013 / DSL-014 / DSL-015 (G4R-CE / rabbit.md): the business layer tells
// its story through named Semantic Vocabulary that declares a full business
// promise — never implementation-action names (execute/process/handle/...),
// never an anonymous middleware pipeline. The vocabulary lives in Application
// (shape/dsl-structured-program.md): it is not a Domain pure rule, not an
// Infrastructure adapter.

import assert from 'node:assert/strict'
import test from 'node:test'

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

/**
 * Production named-vocabulary surface, one entry per Application module.
 * Names are the DSL-013 contract: the call site must reveal what the caller
 * is waiting for from the name + arguments + return type alone.
 */
const VOCABULARY_SURFACES = {
  'Application/Manager/ManagerBackground': ['ensureSettled'],
  'Application/Manager/ManagerActivation': ['ensureAccepted'],
  'Application/Manager/ManagerIdle': ['encourageLabor'],
  'Application/Manager/ManagerJobHandoff': ['completeIfTransferred'],
  'Application/Review/ReviewerContinuation': ['ensurePerfectConfirmed', 'ensureVerdictSubmitted'],
  'Application/Review/ReviewerEvidence': ['classifyNeed'],
  'Application/Recovery/ProviderRecoveryWorkflow': ['continueAfterConfirmedFailure'],
}

/** DSL-013 rejected shapes: implementation-action names, not business promises. */
const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('SW_011_named_vocabulary_surface_exists_in_Application', async () => {
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    for (const name of names) {
      assert.equal(typeof mod[name], 'function', `${modulePath} must export '${name}'`)
    }
  }
})

test('SW_011_vocabulary_names_declare_business_promises_not_implementation_actions', async () => {
  const bad = []
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    for (const name of names) {
      if (REJECTED_PREFIX.test(name)) bad.push(`${modulePath}.${name}`)
    }
  }
  assert.deepEqual(bad, [], 'vocabulary names must not be implementation-action labels')
})

test('SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary', async () => {
  // DSL-015: semantic decorators must be named Vocabulary or a named call
  // site. A global DecoratorBase / MiddlewarePipeline / IWorkflowDecorator
  // framework is banned. Assert the production vocabulary modules expose no
  // such framework shape.
  const frameworkShape = /(DecoratorBase|MiddlewarePipeline|IWorkflowDecorator|WorkflowBuilder)$/
  for (const modulePath of Object.keys(VOCABULARY_SURFACES)) {
    const mod = await load(modulePath)
    const names = Object.keys(mod).filter((n) => !n.endsWith('_$reflection'))
    const hits = names.filter((n) => frameworkShape.test(n))
    assert.deepEqual(hits, [], `${modulePath} must not export middleware-framework shapes`)
  }
})
