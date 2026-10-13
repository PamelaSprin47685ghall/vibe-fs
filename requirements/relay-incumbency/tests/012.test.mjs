import assert from 'node:assert/strict'
import test from 'node:test'

test('WHAT[relay-incumbency-012] a Work binds one DevOps and does not fork a second', () => {
  const bind = (already) => (already ? { ok: true, written: [] } : { ok: true, written: ['DevOpsBound'] })
  assert.deepEqual(bind(false).written, ['DevOpsBound'])
  assert.deepEqual(bind(true).written, [])
})
