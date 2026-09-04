// INTERACTION-AUTHORITY proof — LifeCompleted derives HumanRoot authority closure.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  manager: 'Lead',
}
const rootSelection = (agent) => {
  const canonicalRole = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: agent,
      peerAgent: agent,
      canonicalRole,
      selectedTier: 'deep',
      persona: personas[agent] ?? 'Lead',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const inheritedSeed = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    hash,
    'rt-owner',
    'ses-owner',
    'HumanRoot',
    `owner-${physical}`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const profile = (run, root, kind = 'HumanRoot') => {
  const seed = kind === 'AgentOwnerRoot' ? inheritedSeed('manager', root) : rootSelection('manager')
  const result = authority.createAuthorityRoot(hash, 'rt-close', 'ses-close', kind, root, seed)
  assert.equal(result.ok, true, result.error)
  return { ...result.value, logicalRun: run, authorityRoot: root }
}

test('WHAT[INTERACTION-AUTHORITY-018] IA_018_human_root_closure_clears_active_run_and_retains_history', () => {
  const first = profile('run-1', 'root-1')
  const active = authority.registerAuthority(first, authority.empty)
  const closed = authority.closeCompletedHumanRootManager(active)
  assert.equal(closed.activeLogicalRun, null)
  assert.equal(closed.lastAuthorityProfile.logicalRun, first.logicalRun)

  const second = profile('run-2', 'root-2')
  const reawakened = authority.registerAuthority(second, closed.value)
  assert.equal(reawakened.activeLogicalRun.logicalRun, second.logicalRun)
  assert.equal(reawakened.activeLogicalRun.authorityRoot, second.authorityRoot)

  const fold = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Composition/Durable/Fold.fs'), 'utf8')
  const facts = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Mission/Relay/Facts.fs'), 'utf8')
  assert.match(fold, /closeCompletedHumanRootManager/)
  assert.doesNotMatch(facts, /AuthorityLogicalRunClosed/)
})

test('WHAT[INTERACTION-AUTHORITY-018] IA_018_agent_owner_root_is_not_closed_by_life_completion', () => {
  const owner = profile('run-owner', 'root-owner', 'AgentOwnerRoot')
  const active = authority.registerAuthority(owner, authority.empty)
  const afterLife = authority.closeCompletedHumanRootManager(active)
  assert.equal(afterLife.activeLogicalRun.logicalRun, owner.logicalRun)
  assert.equal(afterLife.activeLogicalRun.authorityRoot, owner.authorityRoot)
})
