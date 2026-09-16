// ARCH-016 Gate B — provider-facing output must not leak host identity tokens.

import assert from 'node:assert/strict'
import test from 'node:test'
import { FORBIDDEN_TOKENS } from '../../../scripts/checks/provider-leak-gate.mjs'

test('WHAT[PARTICIPANT-HORIZON-002] PROVIDER_IDENTITY_LEAK_gate_b_forbids_agent_and_session_ids', () => {
  for (const token of ['AgentId', 'SessionId', 'ManagerJobId', 'PtyId', 'agent_id', 'session_id', 'pty_id']) {
    assert.ok(FORBIDDEN_TOKENS.includes(token), `missing forbidden token: ${token}`)
  }
})
