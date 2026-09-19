// requirements/repository-programming/tests/027.test.mjs
//
// Law: repository-programming-027
// Scenario: Dynamic generation of js-engineer and js-devops with unified file tools and sandbox boundaries.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as jsGenerator from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'

test('WHAT[repository-programming-027] JS027_js_engineer_and_js_devops_generated_with_unified_file_tools_and_sandbox', () => {
  assert.equal(typeof jsGenerator.generateSurfaceForRole, 'function', 'must export generateSurfaceForRole')

  const engineerSurface = jsGenerator.generateSurfaceForRole('Engineer', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(engineerSurface.toolName, 'js-engineer')
  assert.match(engineerSurface.description, /edit\(path,\s*changes\)/)
  assert.match(engineerSurface.description, /file\(path/)
  assert.match(engineerSurface.description, /glob\(pattern\)/)
  assert.match(engineerSurface.description, /grep\(needle,\s*pattern\)/)

  const devopsSurface = jsGenerator.generateSurfaceForRole('DevOps', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(devopsSurface.toolName, 'js-devops')
  assert.match(devopsSurface.description, /edit\(path,\s*changes\)/)
})
