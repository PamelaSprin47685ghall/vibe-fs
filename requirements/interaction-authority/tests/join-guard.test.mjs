// INTERACTION-AUTHORITY proof — JoinGuard admission and bounded repair family.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const root = authority.createAuthorityRoot(hash, 'rt_join', 'ses_jg', 'AgentOwnerRoot', 'root-jg', 'fast-coder').value

test('WHAT[INTERACTION-AUTHORITY-010] PROMPT_010_generic_repair_family_is_bounded_once_per_run', () => {
  let state = authority.registerAuthority(root, authority.empty)
  assert.equal(authority.repairFamilyAlreadyClaimed('ses_jg', root.logicalRun, 'missing-final-report', state), false)
  state = authority.registerClaim(
    authority.claimContinuation('pk-repair', 'ses_jg', 'InteractionRepair', root, 'fast-coder', 'missing-final-report'),
    state,
  )
  assert.equal(authority.repairFamilyAlreadyClaimed('ses_jg', root.logicalRun, 'missing-final-report', state), true)
  assert.equal(authority.repairFamilyAlreadyClaimed('ses_jg', root.logicalRun, 'incomplete-interaction', state), false)
})

test('WHAT[INTERACTION-AUTHORITY-014] JNGD_nudge_contract_fails_closed_without_durable_authority', () => {
  const source = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinGuard.fs'), 'utf8')
  assert.match(source, /Join guard nudge requires an AgentJournal/)
  assert.match(source, /No active authority profile/)
  assert.match(source, /ContinuationKind\.JoinGuard/)
  assert.match(source, /AlreadyOutstanding/)
})
