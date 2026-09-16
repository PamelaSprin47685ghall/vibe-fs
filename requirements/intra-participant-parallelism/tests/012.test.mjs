import assert from 'node:assert/strict'
import test from 'node:test'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility resolves OfficeRole admission across all roles', () => {
  for (const role of ['Manager', 'Coder', 'Inspector', 'Browser', 'Inquiry']) {
    assert.equal(tr.rolePredicate('fission', role), true, `${role} must have fission permission`)
  }
  for (const role of ['Orchestrator', 'DevOps', 'Reviewer', 'Blogger', 'Distiller']) {
    assert.equal(tr.rolePredicate('fission', role), false, `${role} must not have fission permission`)
  }
})
