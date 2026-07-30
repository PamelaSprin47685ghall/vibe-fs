// Every verdict passes, and then the process cannot leave.
//
// The failure mode the previous runner could not express: it awaited `stream.on('end')`, which does
// arrive here (node:test finished its work), so it would have exited 0 on a green ledger while the
// suite left a handle open. A green ledger is not a green run.
import test from 'node:test'
import assert from 'node:assert/strict'

test('passes and leaks', () => {
  setInterval(() => {}, 50)
  assert.equal(1, 1)
})
