// Split from tests/unit/session/host-fork-runtime.test.mjs (cutover Wave 2a); owner: crash-reconciliation.
//
// CRASH-011/EXEC-023 + validatePermit：permit 线性序 → join；root mismatch/
// journalSequence stale/lost member 拒绝；family growth 不吊销 permit；valid
// permit 达 join 体；AwaitAgentWithPermit 校验错误映射 NotFound。InstallRun/
// FailRun/CancelAgent/ForkRuntime 面已随 SPLIT@cutover 迁
// requirements/managed-session-lifecycle/tests/host-fork-runtime.test.mjs；join/
// await 调用代数迁 requirements/delegation/tests/host-fork-join-algebra.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  caseOf,
  listItems,
  sessionId,
  setItems,
  stringSet,
  toList,
} from '../../verification-system/tests/support/domain.mjs'

const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const {
  joinWithPermit,
  joinAvailableWithPermit,
  awaitAgentWithPermit,
} = await import('../../../dist/Session/HostForkJoin.js')
const {
  FamilyRecoveryPermit,
  FamilyRecoveryPermitModule_missingFrom,
  RecoveryClosureModule_members,
} = await import('../../../dist/Domain/SessionRecovery.js')
const { AgentJournalModule_revision, AgentJournalModule_snapshot } = await import(
  '../../../dist/Journal/AgentJournal.js'
)
const { JournalRevisionModule_value } = await import('../../../dist/Kernel/Identity.js')
const { discover } = await import('../../../dist/Journal/RecoveryClosureProjection.js')

const PARENT = sessionId('ses_hfrt')

const fakeSessions = () => {
  const calls = []
  return {
    calls,
    CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-1')] }),
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async () => ({ tag: 0, fields: [] }),
    SendPromptAsync: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

/** Real runtime over a real journal with a fake session host. */
const live = async (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hfrt-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  const sessions = fakeSessions()
  const runtime = new HostForkRuntime(PARENT, sessions, opened.journal)
  return {
    runtime,
    sessions,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

/** Permit that validates against the journal's CURRENT closure. */
const closureOf = (j) => {
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(j))
  return discover(PARENT, AgentJournalModule_snapshot(j).AgentProjections, sequence)
}

/** A permit over the family as it stands: its member set, which is what admission reads. */
const validPermit = (j) => {
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(j))
  return new FamilyRecoveryPermit(PARENT, sequence, RecoveryClosureModule_members(closureOf(j)))
}

// ── validatePermit branches (via JoinWithPermit / JoinAvailableWithPermit) ───

test('HFRT_join_with_permit_root_mismatch_is_not_found', async () => {
  const liveCtx = await live()
  const permit = new FamilyRecoveryPermit(sessionId('ses_other'), 0n, stringSet([]))
  const result = await joinWithPermit(liveCtx.runtime, permit, [])
  assert.equal(result.tag, 1)
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], /family recovery permit root mismatch: permit=ses_other runtime=ses_hfrt/)
  liveCtx.cleanup()
})

test('HFRT_join_with_permit_stale_journal_sequence_is_not_found', async () => {
  const liveCtx = await live()
  const current = JournalRevisionModule_value(AgentJournalModule_revision(liveCtx.journal))
  const permit = new FamilyRecoveryPermit(PARENT, current + 1000n, stringSet([]))
  const result = await joinWithPermit(liveCtx.runtime, permit, [])
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], new RegExp(`family recovery permit journalSequence stale: permit=${current + 1000n}`))
  liveCtx.cleanup()
})

test('EXEC_023_permit_whose_recovered_member_is_gone_is_not_found', async () => {
  const liveCtx = await live()
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(liveCtx.journal))
  // A member the family no longer has: recovery closed over something that has since vanished,
  // which is the only thing that may invalidate a permit.
  const permit = new FamilyRecoveryPermit(PARENT, sequence, stringSet(['W:ses_vanished']))
  const result = await joinAvailableWithPermit(liveCtx.runtime, permit, 5, new Promise(() => {}))
  const err = result.fields[0]
  assert.equal(caseOf(err), 'NotFound')
  assert.match(err.fields[0], /closure lost members: missing=W:ses_vanished/)
  liveCtx.cleanup()
})

test('EXEC_023_permit_survives_family_growth_after_recovery_closed', async () => {
  const liveCtx = await live()
  const permit = validPermit(liveCtx.journal)

  // A grandchild appearing mid-join grows the closure. It was created live, so it needed no
  // recovery, and the permit must still admit the join: digest equality refused exactly this and
  // made `temporal-ownership-unhappy-path` fail whenever the fork landed inside the window.
  const grown = stringSet([
    ...setItems(RecoveryClosureModule_members(closureOf(liveCtx.journal))),
    'C:ses_child>ses_grandchild',
  ])
  assert.deepEqual(listItems(FamilyRecoveryPermitModule_missingFrom(grown, permit)), [])

  const result = await joinWithPermit(liveCtx.runtime, permit, [10])
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NothingToJoin', 'growth must not revoke the permit')
  liveCtx.cleanup()
})

test('HFRT_join_with_valid_permit_passes_validation', async () => {
  const liveCtx = await live()
  const permit = validPermit(liveCtx.journal)
  const result = await joinWithPermit(liveCtx.runtime, permit, [10])
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NothingToJoin', 'valid permit must reach the join body')
  liveCtx.cleanup()
})

// ── AwaitAgent ───────────────────────────────────────────────────────────────

test('HFRT_await_agent_with_permit_validation_error_maps_to_not_found', async () => {
  const liveCtx = await live()
  const permit = new FamilyRecoveryPermit(sessionId('ses_other'), 0n, stringSet([]))
  const result = await awaitAgentWithPermit(liveCtx.runtime, permit, 'ag9', [])
  assert.equal(result.tag, 1)
  assert.equal(caseOf(result.fields[0]), 'NotFound')
  liveCtx.cleanup()
})
