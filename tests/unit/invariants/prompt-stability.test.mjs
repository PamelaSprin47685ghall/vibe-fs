// ARCH-016 Gate D — same session must keep system prompt bytes stable across fallback/T1/review/reanchor/Strength.
// Code phase stub: contract pin + explicit todo for behavioral harness.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

test('PROMPT_STABILITY_gate_d_is_wired_in_verify_contract', () => {
  const verify = readFileSync(new URL('../../../docs/proof/verify.md', import.meta.url), 'utf8')
  assert.match(verify, /prompt-stability\.test\.mjs/)
  assert.match(verify, /Gate D/)
  assert.match(verify, /system prompt 字节相同/)
})

test.todo('PROMPT_STABILITY_same_session_fallback_t1_review_reanchor_strength_bytes_identical')
