/**
 * Structural eval: corpus + oracles over synthetic traces. No LLM.
 * Oracles must not appear in production Tools/*.fs.
 */
import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { CASES } from './corpus.mjs'
import { ORACLES, evaluateCase } from './oracles.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../../../..')
const HERE = dirname(fileURLToPath(import.meta.url))

const assertCaseShape = (c) => {
  assert.equal(typeof c.id, 'string', c.id)
  assert.equal(typeof c.setup, 'string', c.id)
  assert.equal(typeof c.pass_if, 'string', c.id)
  assert.ok(c.pass_example?.role && Array.isArray(c.pass_example.toolCalls), `${c.id} pass_example`)
  assert.ok(c.fail_example?.role && Array.isArray(c.fail_example.toolCalls), `${c.id} fail_example`)
  assert.equal(typeof ORACLES[c.id], 'function', `${c.id} oracle`)
  const pass = evaluateCase(c, c.pass_example)
  const fail = evaluateCase(c, c.fail_example)
  assert.equal(pass.ok, true, `${c.id} pass_example: ${pass.reason ?? ''}`)
  assert.equal(fail.ok, false, `${c.id} fail_example must be rejected`)
}

test('WHAT[OFF-009] office_boundary_eval_inspector_refuses_repair_case_is_red_and_green', () => {
  const c = CASES.find((x) => x.id === 'inspector-refuses-repair')
  assertCaseShape(c)
})
