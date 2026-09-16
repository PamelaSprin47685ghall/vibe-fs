import assert from 'node:assert/strict'
import test from 'node:test'

import * as SemanticVocabularySurface from '../../../dist/Application/SemanticVocabularySurface.js'

test('WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary', () => {
  const forbidden = ['useMiddleware', 'registerFilter', 'applyInterceptors']
  const exported = Object.keys(SemanticVocabularySurface)
  for (const name of forbidden) {
    assert.equal(exported.includes(name), false, `Must not export generic middleware: ${name}`)
  }
})
