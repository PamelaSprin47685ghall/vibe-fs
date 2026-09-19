import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as outcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[structured-workflow-001] SW_001_workflow_entrypoints_are_the_exported_surface', () => {
  // Source-tree proof: each workflow module defines its named entrypoint as a
  // `let` — the direct-CE contract (structured-workflow-001). Build-verification
  // (guide-contract.test.mjs) proves the emitted modules load and the
  // entrypoints are callable.
  const entrypoints = [
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observe'],
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observeIdle'],
    ['src/Wanxiangshu/Composition/Turn/Workflow.fs', 'observe'],
  ]
  const missing = []
  for (const [file, name] of entrypoints) {
    const source = readSrc(file)
    if (!new RegExp(`\\blet(?: rec)?(?: private)? ${name}\\b`).test(source)) {
      missing.push(`${file}: ${name}`)
    }
  }
  assert.deepEqual(missing, [], `workflow entrypoints must exist in production source: ${missing.join('; ')}`)
})
