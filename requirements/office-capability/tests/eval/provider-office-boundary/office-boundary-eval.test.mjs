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

test('office_boundary_eval_corpus_has_id_setup_oracles_and_synthetic_traces', () => {
  assert.equal(CASES.length, 4)
  assert.deepEqual(
    CASES.map((c) => c.id).sort(),
    [
      'coder-inspect-ownership',
      'devops-does-not-choose-among-valid-behaviors',
      'inspector-refuses-repair',
      'manager-mixed-mission',
    ],
  )
  for (const c of CASES) {
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
})

test('office_boundary_eval_coder_inspect_oracle_is_charge_text_not_a_filter_module', () => {
  const c = CASES.find((x) => x.id === 'coder-inspect-ownership')
  assert.ok(c.fail_if_inspect_charge_matches instanceof RegExp)
  assert.match(c.notes, /not a production filter/)
})

test('office_boundary_eval_oracles_are_not_wired_into_production_tools', () => {
  const sources = [
    readFileSync(join(HERE, 'oracles.mjs'), 'utf8'),
    readFileSync(join(HERE, 'corpus.mjs'), 'utf8'),
  ]
  for (const text of sources) {
    assert.doesNotMatch(text, /src\/Wanxiangshu|Infrastructure\/OpenCode\/Tools/)
  }

  const dirs = [
    join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Tools'),
    join(ROOT, 'src/Wanxiangshu/Tools'),
  ]
  for (const dir of dirs) {
    for (const name of readdirSync(dir)) {
      if (!name.endsWith('.fs')) continue
      const text = readFileSync(join(dir, name), 'utf8')
      assert.doesNotMatch(text, /fail_if_inspect_charge_matches/, name)
      assert.doesNotMatch(text, /coder-inspect-ownership/, name)
      assert.doesNotMatch(text, /\\b\(fix\|edit\|implement\|write\|modify\)\\b/, name)
    }
  }
})
