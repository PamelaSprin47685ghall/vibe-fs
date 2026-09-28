import assert from 'node:assert/strict'
import test from 'node:test'
import { scanRepo } from '../../../scripts/checks/language-parity-gate.mjs'

test('WHAT[provider-language-008] the existing resource and selected-source language scan accepts the repository', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test.todo('WHAT[provider-language-008] actual provider tool descriptions and calling-contract prose must match the bound session; resource existence and source scanning do not prove delivery')
