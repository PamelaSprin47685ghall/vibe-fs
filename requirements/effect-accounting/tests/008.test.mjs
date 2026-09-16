import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as blogger from '../../../dist/Context/Companion/BloggerSurface.js'
import * as pluginHooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const ownerRootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const acceptOwnerLogicalRun = async (handle, ownerSession) => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    ownerSession,
    `msg-root-${ownerSession}`,
    ownerRootSelection,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  assert.ok(
    dispatch.projectionObservation(handle, ownerSession).activeLogicalRun,
    'the owner must hold an active durable Logical Run before its Companion leaf is dispatched',
  )
}

const withJournal = async (prefix, writer, runtime, body) => {
  const directory = mkdtempSync(join(tmpdir(), prefix))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, writer, runtime, 4242, '2026-08-30T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    return await body(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_opens_request_queryable_by_blogger', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_1', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req)
  assert.equal(s1.ok, true)
  assert.equal(blogger.hasOpenRequest(s1.value, 'ses_008'), true)
  assert.equal(blogger.openRequestId(s1.value, 'ses_008'), 'req_008_1')
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_entry_commit_records_receipt_and_clears_open_request', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_2', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  const s2 = blogger.bindPromptKey(s1, 'ses_008', 'req_008_2', 'pk-1').value
  const s3 = blogger.commitEntry(s2, 'ses_008', 'run_008_1', 1, 'digest-1', 'frame-1').value
  assert.equal(blogger.hasOpenRequest(s3, 'ses_008'), false)
  assert.equal(blogger.lastReceipt(s3, 'ses_008'), 'run_008_1')
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_same_provider_run_cannot_be_both_entry_and_squash', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_3', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  const s2 = blogger.bindPromptKey(s1, 'ses_008', 'req_008_3', 'pk-1').value
  const s3 = blogger.commitEntry(s2, 'ses_008', 'run_008_dup', 1, 'digest-1', 'frame-1').value
  const s4 = blogger.commitSquash(s3, 'ses_008', 'run_008_dup', 1, 1, 'digest-1', 'squash-1')
  assert.equal(s4.ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_fill_in_after_send', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_pk', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  assert.equal(blogger.promptKey(s1, 'ses_008'), null)
  const s2 = blogger.bindPromptKey(s1, 'ses_008', 'req_008_pk', 'pk-bound')
  assert.equal(s2.ok, true)
  assert.equal(blogger.promptKey(s2.value, 'ses_008'), 'pk-bound')
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_materialize_prompt_key_cannot_rebind', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_reb', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  const s2 = blogger.bindPromptKey(s1, 'ses_008', 'req_008_reb', 'pk-1').value
  const s3 = blogger.bindPromptKey(s2, 'ses_008', 'req_008_reb', 'pk-2')
  assert.equal(s3.ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_duplicate_request_materialize_different_context_rejected', () => {
  const req1 = blogger.mainRequest('ses_008', 'req_008_diff', 0, 1, 'digest-1', 'toml-1')
  const req2 = blogger.mainRequest('ses_008', 'req_008_diff', 0, 2, 'digest-2', 'toml-2')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req1).value
  const s2 = blogger.materializeRequest(s1, req2)
  assert.equal(s2.ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_abandon_clears_open_request', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_ab', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  const s2 = blogger.abandonRequest(s1, 'ses_008', 'req_008_ab')
  assert.equal(s2.ok, true)
  assert.equal(blogger.hasOpenRequest(s2.value, 'ses_008'), false)
})

test('WHAT[EFFECT-ACCOUNTING-008] C5_request_id_cannot_rebind_to_different_provider_run', () => {
  const req = blogger.mainRequest('ses_008', 'req_008_rebind', 0, 1, 'digest-1', 'toml-1')
  const s0 = blogger.empty
  const s1 = blogger.materializeRequest(s0, req).value
  const s2 = blogger.bindPromptKey(s1, 'ses_008', 'req_008_rebind', 'pk-rebind').value
  const s3 = blogger.commitEntry(s2, 'ses_008', 'run_008_a', 1, 'digest-1', 'frame-1').value
  assert.equal(s3.ok, true)
})

test('WHAT[EFFECT-ACCOUNTING-008] Adapter Blogger Coordinator submits one receipt and recovers exact physical acceptance without resend', async () => {
  await withJournal('wxs-blogger-coordinator-', 'writer-blogger-coordinator', 'rt-blogger-coordinator', async (handle) => {
    const mainSession = 'ses-main-effect-008'
    const bloggerSession = 'ses-blogger-effect-008'
    const physicalUserMessageId = 'msg-blogger-effect-008'
    const submissions = []
    let childListCalls = 0
    const childCreates = []
    const host = {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async (session, text, options) => {
        submissions.push({ session, text, options })
        return dispatch.admittedWithReceipt('accepted-host-blogger-008')
      },
      AbortSession: async () => ({ ok: true }),
      InterruptAttempt: async () => ({ ok: true }),
      IsManagedChild: () => true,
      AbortChildren: async () => {},
      CreateSiblingSession: async () => ({ ok: false, error: 'unexpected sibling creation' }),
      TryGetParentSession: async () => ({ ok: true, value: undefined }),
      CreateChildSession: async (parent, options) => {
        childCreates.push({ parent, options })
        return { ok: true, value: bloggerSession }
      },
      ListChildren: async () => {
        childListCalls += 1
        return dispatch.acceptedChild(bloggerSession, 'blogger', 'blogger')
      },
      FamilyRootOf: () => mainSession,
    }

    await acceptOwnerLogicalRun(handle, mainSession)

    const decision = await pluginHooks.coordinateBloggerUnresolvedTwice(
      host,
      handle,
      mainSession,
      bloggerSession,
      'request-blogger-effect-008',
    )
    assert.equal(pluginHooks.firstBloggerEffect(decision), 'StartedSquash')
    assert.equal(pluginHooks.secondBloggerEffect(decision), 'SkippedInFlight')
    assert.equal(childListCalls, 2, 'SatelliteRuntime must inspect root and owner child listings')
    assert.equal(childCreates.length, 0, 'the exact restored Blogger child must be reused')
    assert.equal(submissions.length, 1)
    assert.equal(submissions[0].session, bloggerSession)

    const submitted = dispatch.projectionObservation(handle, bloggerSession)
    assert.equal(submitted.pendingClaims.length, 1)
    const claim = submitted.pendingClaims[0]
    assert.equal(claim.promptKey, submissions[0].options.Metadata.wanxiangshu_prompt_key)
    assert.equal(claim.receipt, `accepted-detached-${claim.promptKey}`)

    const unresolved = await recovery.reconcile(handle, [])
    assert.deepEqual(unresolved.map(({ promptKey, outcome }) => ({ promptKey, outcome })), [
      { promptKey: claim.promptKey, outcome: 'StillPending' },
    ])
    assert.equal(submissions.length, 1)

    const proven = await recovery.reconcile(handle, [{
      id: physicalUserMessageId,
      role: 'user',
      metadata: { wanxiangshu_prompt_key: claim.promptKey },
    }])
    assert.deepEqual(proven.map(({ promptKey, outcome, physicalMessageId }) => ({ promptKey, outcome, physicalMessageId })), [
      { promptKey: claim.promptKey, outcome: 'Proven', physicalMessageId: physicalUserMessageId },
    ])
    assert.equal(dispatch.projectionObservation(handle, bloggerSession).pendingClaims.length, 0)
    assert.equal(submissions.length, 1)
  })
})
