import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { AgentTier, Role } from '../../../dist/Foundation/Roles.js'
import { RootAuthorityKind, empty as emptyAuthority } from '../../../dist/Interaction/Authority/Model.js'
import { registerAuthority } from '../../../dist/Interaction/Authority/Run.js'
import { closeCompletedHumanRootManager } from '../../../dist/Interaction/Authority/Ledger.js'
import {
  AuthorityRootUserMessageIdModule_create as authorityRoot,
  LogicalRunIdModule_create as logicalRun,
  SessionIdModule_create as sessionId,
} from '../../../dist/Foundation/Identity.js'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')

const profile = (run, rootId, kind = RootAuthorityKind.HumanRoot) => ({
  SessionId: sessionId('ses-authority-close'),
  LogicalRunId: logicalRun(run),
  AuthorityRootUserMessageId: authorityRoot(rootId),
  AuthorityKind: kind,
  SelectedAgent: 'fast-manager',
  PeerAgent: 'deep-manager',
  CanonicalRole: Role.Manager,
  SelectedTier: AgentTier.Fast,
})

test('IA_018_LifeCompleted_derives_HumanRoot_run_closure_without_a_second_durable_fact', () => {
  const first = profile('run-1', 'root-1')
  const active = registerAuthority(first, emptyAuthority)
  const closed = closeCompletedHumanRootManager(active)

  assert.equal(closed.ActiveLogicalRun, undefined)
  assert.equal(closed.LastAuthorityProfile.LogicalRunId, first.LogicalRunId)

  const second = profile('run-2', 'root-2')
  const reawakened = registerAuthority(second, closed)
  assert.equal(reawakened.ActiveLogicalRun.LogicalRunId, second.LogicalRunId)
  assert.equal(reawakened.ActiveLogicalRun.AuthorityRootUserMessageId, second.AuthorityRootUserMessageId)

  const fold = read('src/Wanxiangshu/Composition/Durable/Fold.fs')
  const facts = read('src/Wanxiangshu/Composition/Durable/Fact.fs')
  assert.match(fold, /ManagerLifecycleFact\.LifeCompleted _[\s\S]{0,300}closeCompletedHumanRootManager/)
  assert.doesNotMatch(facts, /AuthorityLogicalRunClosed/)
})

test('IA_018_AgentOwnerRoot_is_not_closed_by_Manager_LifeCompleted', () => {
  const owner = profile('run-owner', 'root-owner', RootAuthorityKind.AgentOwnerRoot)
  const active = registerAuthority(owner, emptyAuthority)
  const afterLife = closeCompletedHumanRootManager(active)

  assert.equal(afterLife.ActiveLogicalRun.LogicalRunId, owner.LogicalRunId)
  assert.equal(afterLife.ActiveLogicalRun.AuthorityRootUserMessageId, owner.AuthorityRootUserMessageId)
})
