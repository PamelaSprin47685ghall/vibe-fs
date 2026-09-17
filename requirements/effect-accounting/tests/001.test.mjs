import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as change from '../../../dist/Change/Surface.js'

const FACT_CODEC_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/Journal/FactCodec.fs', import.meta.url), 'utf8')

const FACT_TYPES_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Change/Facts.fs', import.meta.url), 'utf8')

const JOB = 'job_ea'

const WT = 'wt_ea'

const WT_PATH = '/tmp/wt_ea'

const baseJob = {
  jobId: JOB,
  managerSessionId: 'ses_ea',
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: WT,
  worktreePath: WT_PATH,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
}

const createJob = () => change.createJob(change.empty(), baseJob)

const progress = (kind, payload) => ({ kind, payload })

const managerCreated = { kind: 'ManagerJobCreated', payload: baseJob }

const requested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }

const created = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }

const rebased = {
  kind: 'RebasedCandidateReady',
  payload: { jobId: JOB, rebasedCommit: 'r1', targetHeadSnapshot: 'h1', workspaceSnapshotId: 'snap_2' },
}

const claimed = {
  kind: 'PublishClaimed',
  payload: {
    jobId: JOB,
    targetRef: 'refs/heads/main',
    rebasedCommit: 'r1',
    expectedHead: 'h1',
    workspaceSnapshotId: 'snap_2',
    qualityCertificateId: 'cert_ea',
    authorityRevision: 'rev_ea',
  },
}

const fold = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.error ?? '')
  return change.unwrapFold(result)
}

test('WHAT[EFFECT-ACCOUNTING-001] worktree_requested_created_are_distinct_typed_states_not_one_bool', () => {
  let projection = createJob()
  assert.equal(change.worktreeEffect(projection, WT), null)
  projection = change.requestWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Requested')
  projection = change.acceptWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  projection = change.requestWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  assert.equal(change.worktreeEffect(fold([managerCreated, requested, created]), WT), 'Created')
})
