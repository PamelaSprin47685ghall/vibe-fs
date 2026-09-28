import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const terminal = (kind, values = {}) => ({
  kind, ptyId: 'private-terminal', terminalLabel: 'npm test', outcome: 'exit 0', code: '', message: '', ...values,
})

test('WHAT[participant-horizon-005] Join preserves exit measurements without exposing the terminal handle or control codes', () => {
  for (const code of [0, 7]) {
    const text = join.renderBatch('en', [terminal('pty-exited', { outcome: `exit ${code}` })])
    assert.equal(parseToml(text).exit_code, code)
    assert.match(text, /npm test has ended/)
    assert.ok(!text.includes('private-terminal'))
    assert.equal(parseToml(text).status, undefined)
  }
  const failed = join.renderBatch('en', [terminal('pty-failed', { outcome: 'crash', code: 'PRIVATE-CODE', message: 'kaboom' })])
  assert.deepEqual(parseToml(failed), { output: 'kaboom' })
  assert.ok(!failed.includes('PRIVATE-CODE'))
  assert.ok(!failed.includes('private-terminal'))
})
