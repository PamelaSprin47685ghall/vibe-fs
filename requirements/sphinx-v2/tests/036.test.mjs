import assert from 'node:assert/strict'
import test from 'node:test'
import {Surface_isTool as isTool} from '../../../dist/Sphinx/V2/Wire/Surface.js'

const allowed = ['sphinx_inquiry_start', 'sphinx_work_next', 'sphinx_work_submit', 'sphinx_inquiry_status', 'sphinx_inquiry_cancel', 'sphinx_inquiry_export', 'sphinx_goal_amend']

test('WHAT[sphinx-v2-036] actual tool contract admits the v2 names and rejects retired stage aliases', () => {
  for (const name of allowed) assert.equal(isTool(name), true, name)
  for (const name of ['sphinx_assess', 'sphinx_propose', 'sphinx_investigate', 'sphinx_synthesize', 'sphinx', 'sphinx_inquiry_start_extra', 'SPHINX_WORK_NEXT']) {
    assert.equal(isTool(name), false, name)
  }
})

test.todo('WHAT[sphinx-v2-036] registered MCP tools route their actual arguments through the single runtime and status/export create no work or state changes')
