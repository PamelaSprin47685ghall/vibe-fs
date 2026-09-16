import assert from 'node:assert/strict'
import test from 'node:test'
import * as canaries from '../../../dist/OpenCode/Host/MagicTodoHostCanariesSurface.js'
import * as codec from '../../../dist/OpenCode/Host/MagicTodoHostCodecSurface.js'

test('WHAT[OBLIGATION-LEDGER-015] obligations project to the original V1 decoder shape', async () => {
  const r = await canaries.testProjectOriginalV1Shape()
  assert.equal(r.ok, true)
})

test('WHAT[OBLIGATION-LEDGER-015] projection helper mutates original args in place', () => {
  assert.equal(canaries.testProjectionMutatesArgsInPlace(), true)
})

test('WHAT[OBLIGATION-LEDGER-015] workingOn projects to in_progress and every other obligation to pending', () => {
  assert.equal(codec.testWorkingOnProjectsToInProgress(), true)
})

test('WHAT[OBLIGATION-LEDGER-015] projects obligations into a non-enumerable V1 compatibility view', () => {
  assert.equal(codec.testNonEnumerableV1CompatView(), true)
})
