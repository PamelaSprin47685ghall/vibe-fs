import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

const root = resolve(import.meta.dirname, '../../..')

const read = (p) => readFileSync(resolve(root, p), 'utf8')

const fissionProduction = () => [
  'src/Wanxiangshu/Execution/Fission/Model.fs',
  'src/Wanxiangshu/Execution/Fission/Admission.fs',
  'src/Wanxiangshu/Execution/Fission/Runtime.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
].map(read).join('\n')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility resolves OfficeRole admission across all roles', () => {
  assert.equal(tr.rolePredicate('fission', 'Engineer'), true, 'Engineer must have fission permission')
  for (const role of [
    'Manager',
    'DevOps',
    'Orchestrator',
    'Blogger',
    'Bookkeeper',
    'Predictor',
    'Coder',
    'Inspector',
    'Browser',
    'Inquiry',
    'Reviewer',
    'Distiller',
  ]) {
    assert.equal(tr.rolePredicate('fission', role), false, `${role} must not have fission permission`)
  }
})
