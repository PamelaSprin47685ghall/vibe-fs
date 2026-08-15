import assert from 'node:assert/strict'
import test from 'node:test'
import { AgentTier, Role } from '../../../dist/Foundation/Roles.js'
import { RootAuthorityKind, empty as emptyAuthority } from '../../../dist/Interaction/Authority/Model.js'
import { closeAuthority, registerAuthority } from '../../../dist/Interaction/Authority/Run.js'
import {
  AuthorityRootUserMessageIdModule_create as authorityRoot,
  LogicalRunIdModule_create as logicalRun,
  SessionIdModule_create as sessionId,
} from '../../../dist/Foundation/Identity.js'

const profile = (run, root) => ({
  SessionId: sessionId('ses-authority-close'),
  LogicalRunId: logicalRun(run),
  AuthorityRootUserMessageId: authorityRoot(root),
  AuthorityKind: RootAuthorityKind.HumanRoot,
  SelectedAgent: 'fast-manager',
  PeerAgent: 'deep-manager',
  CanonicalRole: Role.Manager,
  SelectedTier: AgentTier.Fast,
})

test('IA_018_terminal_close_releases_only_active_run_authority_and_preserves_history', () => {
  const first = profile('run-1', 'root-1')
  const active = registerAuthority(first, emptyAuthority)
  const closedResult = closeAuthority(first.LogicalRunId, first.AuthorityRootUserMessageId, active)

  assert.equal(closedResult.tag, 0)
  const closed = closedResult.fields[0]
  assert.equal(closed.ActiveLogicalRun, undefined)
  assert.equal(closed.LastAuthorityProfile.LogicalRunId, first.LogicalRunId)

  const idempotent = closeAuthority(first.LogicalRunId, first.AuthorityRootUserMessageId, closed)
  assert.equal(idempotent.tag, 0, 'same durable close may be observed idempotently')

  const second = profile('run-2', 'root-2')
  const reawakened = registerAuthority(second, closed)
  assert.equal(reawakened.ActiveLogicalRun.LogicalRunId, second.LogicalRunId)
  assert.equal(reawakened.ActiveLogicalRun.AuthorityRootUserMessageId, second.AuthorityRootUserMessageId)
})
