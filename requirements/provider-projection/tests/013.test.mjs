import assert from 'node:assert/strict'
import test from 'node:test'
import { runOutputAssertions } from '../../../scripts/checks/llm-facing-format-gate.mjs'

test('WHAT[provider-projection-013] synthetic representation proves escaping, empty values, outcome distinction, required fields and isolation without filename whitelist', async () => {
  const result = await runOutputAssertions()
  if (result.skipped) {
    return
  }
  assert.deepEqual(
    result.issues,
    [],
    `synthetic representation output assertions must hold: ${result.issues.map((i) => i.message).join('; ')}`,
  )
})

test.todo('WHAT[provider-projection-013] all synthetic payloads use the common typed representation owner; the gate currently checks direct SyntheticToml access only (GAP-082)')
