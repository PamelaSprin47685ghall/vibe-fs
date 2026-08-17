// Join guard role policy remains owner-typed; tests observe source laws only.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const model = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Model.fs', import.meta.url), 'utf8')
const policy = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Host/TerminalPolicy.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-019] JOIN_GUARD_roles_are_explicit', () => {
  assert.match(model, /Role: Role|Role\.Coder|Role\.Inspector|Role\.Manager/)
  assert.match(policy, /TerminalPolicy/)
})
test('WHAT[DELEG-019] JOIN_GUARD_unknown_role_is_not_silently_manager', () => {
  assert.doesNotMatch(model, /default.*Manager|unknown.*Manager/i)
})
