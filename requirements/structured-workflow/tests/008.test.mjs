import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const ROOT = new URL('../../../', import.meta.url).pathname

const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const VOCABULARY_SURFACES = {
  'Mission/Manager/Workflow': ['observe', 'observeIdle'],
  'Participant/Provider/Attempt/Fallback/Ledger': ['recordAuthorizedFailure'],
  'Participant/Provider/Attempt/Fallback/Workflow': ['continueAfterConfirmedFailure'],
  'Change/Program': ['run'],
}

const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('WHAT[structured-workflow-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary', () => {
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
