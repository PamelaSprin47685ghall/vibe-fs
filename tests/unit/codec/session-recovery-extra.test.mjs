// Domain/SessionRecovery remaining branches: family→SessionRecovery mapping
// (handle + job families), validateClosurePure cycle detection, authorize
// Blocked/Waiting/Ready aggregation, RecoveryReceipt accessors.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, resultOf, sessionRecovery, sessionId } from '../support/domain.mjs'

const {
  sessionRecoveryOfHandleFamily: ofHandleFamily,
  sessionRecoveryOfJobFamily: ofJobFamily,
  validateClosurePure,
  authorizeFamilyResume,
  RecoveryReceiptModule_create: makeReceipt,
  RecoveryReceiptModule_sessionId: receiptSessionId,
  RecoveryReceiptModule_journalSequence: receiptJournalSequence,
  RecoveryReceiptModule_snapshotDigest: receiptSnapshotDigest,
  RecoveryReceiptModule_resolvedClaims: receiptResolvedClaims,
  RecoveryReceiptModule_restoredHandles: receiptRestoredHandles,
  NonEmpty_one: nonEmptyOne,
  NonEmpty_ofList: nonEmptyOfList,
  NonEmpty_toList: nonEmptyToList,
  NonEmpty_map: nonEmptyMap,
  HandleFamilyRecovery,
  JobFamilyRecovery,
  RecoveryNode,
  RecoveryClosure,
  ValidatedClosure,
  FamilyRecoveryPermit,
  SessionRecovery,
  RecoveryBlock,
  RecoveredHandle,
  HandleRecoveryWait,
  HandleRecoveryBlock,
} = await import('../../../dist/Domain/SessionRecovery.js')

const { SessionIdModule_create: sid, AgentHandleIdModule_create: handleId, ManagerJobIdModule_create: jobId } = await import('../../../dist/Kernel/Identity.js')

const { toList } = await import('../support/domain.mjs')

test('MISC_recovery_of_handle_family_all_branches', () => {
  const s = sid('s1')
  // NoLinkedHandles → NoRecoveryRequired
  let out = ofHandleFamily(s, 7n, HandleFamilyRecovery.NoLinkedHandles)
  assert.equal(caseOf(out), 'NoRecoveryRequired')
  assert.deepEqual([...out.fields[0].RestoredHandles], [])

  // HandlesRecovered → Recovered with restored handles
  const rec = new RecoveredHandle(handleId('h1'), sid('c1'), 'terminal')
  out = ofHandleFamily(s, 8n, new HandleFamilyRecovery(1, [nonEmptyOne(rec)]))
  assert.equal(caseOf(out), 'Recovered')
  assert.deepEqual([...out.fields[0].RestoredHandles], [handleId('h1')])

  // HandlesWaiting → Waiting with per-child blocks
  const wait = new HandleRecoveryWait(handleId('h2'), sid('c2'), 'still running')
  out = ofHandleFamily(s, 9n, new HandleFamilyRecovery(2, [nonEmptyOne(wait)]))
  assert.equal(caseOf(out), 'Waiting')
  const waitBlocks = [...nonEmptyToList(out.fields[0])]
  assert.equal(waitBlocks.length, 1)
  assert.match(waitBlocks[0].fields[1], /handle .* waiting: still running/)

  // HandlesBlocked → Blocked
  const block = new HandleRecoveryBlock(handleId('h3'), sid('c3'), 'linkage conflict')
  out = ofHandleFamily(s, 10n, new HandleFamilyRecovery(3, [nonEmptyOne(block)]))
  assert.equal(caseOf(out), 'Blocked')
  const blockBlocks = [...nonEmptyToList(out.fields[0])]
  assert.equal(blockBlocks.length, 1)
  assert.match(blockBlocks[0].fields[1], /handle .* blocked: linkage conflict/)
})

test('MISC_recovery_of_job_family_all_branches', () => {
  const s = sid('s1')
  let out = ofJobFamily(s, 1n, JobFamilyRecovery.NoRelatedJobs)
  assert.equal(caseOf(out), 'NoRecoveryRequired')

  out = ofJobFamily(s, 2n, new JobFamilyRecovery(1, [nonEmptyOne(jobId('j1'))]))
  assert.equal(caseOf(out), 'Recovered')

  out = ofJobFamily(s, 3n, new JobFamilyRecovery(2, [jobId('j2'), 'no evidence']))
  assert.equal(caseOf(out), 'Waiting')
  assert.match(out.fields[0].Head.fields[1], /job .* unknown: no evidence/)

  const hard = new RecoveryBlock(5, [sid('c9'), 'job dead'])
  out = ofJobFamily(s, 4n, new JobFamilyRecovery(3, [nonEmptyOne(hard)]))
  assert.equal(caseOf(out), 'Blocked')
})

test('MISC_recovery_validate_closure_pure', () => {
  const s1 = sid('a1')
  const s2 = sid('a2')
  const closure = (nodes) => new RecoveryClosure(s1, toList(nodes), 'dig', 5n)

  const ok = validateClosurePure(closure([new RecoveryNode(0, [s1]), new RecoveryNode(1, [s1, s2, handleId('h1')])]))
  assert.equal(resultOf(ok).ok, true)
  assert.equal(resultOf(ok).value.fields[0].Root.fields[0], 'a1')

  // Duplicate session across nodes → cycle error.
  const cycle = validateClosurePure(closure([new RecoveryNode(0, [s1]), new RecoveryNode(1, [s1, s2, handleId('h1')]), new RecoveryNode(3, [s2, s2])]))
  assert.equal(resultOf(cycle).ok, false)
  assert.equal(caseOf(resultOf(cycle).error.Head), 'RecoveryCycle')
})

test('MISC_recovery_authorize_aggregates_blocks_waits_ready', () => {
  const root = sid('root1')
  const child = sid('child1')
  const blocked = new SessionRecovery(3, [nonEmptyOne(new RecoveryBlock(5, [child, 'nope']))])
  const waiting = new SessionRecovery(2, [nonEmptyOne(new RecoveryBlock(5, [child, 'hold']))])
  const recovered = new SessionRecovery(1, [makeReceipt(child, 1n, undefined, toList([]), toList([]))])
  const none = new SessionRecovery(0, [makeReceipt(child, 1n, undefined, toList([]), toList([]))])
  void none

  const closure = (results) => sessionRecovery.recoveredClosure(root, results)

  const blockedOut = authorizeFamilyResume(root, 9n, closure({ child1: blocked }))
  assert.equal(caseOf(blockedOut), 'FamilyBlocked')
  assert.equal(blockedOut.fields[0].Head.fields[0].fields[0], 'child1')

  const waitOut = authorizeFamilyResume(root, 9n, closure({ child1: waiting, other: none }))
  assert.equal(caseOf(waitOut), 'FamilyWaiting')

  const ready = authorizeFamilyResume(root, 9n, closure({ child1: recovered, other: none }))
  assert.equal(caseOf(ready), 'FamilyReady')
  assert.equal(ready.fields[0].fields[0].fields[0], 'root1')
  assert.equal(ready.fields[0].fields[1], 9n)
  assert.equal(ready.fields[0].fields[2], '', 'permit carries the closure digest verbatim')
})

test('MISC_recovery_receipt_accessors_and_nonempty_helpers', () => {
  const s = sid('s1')
  const receipt = makeReceipt(s, 42n, 'snap', toList([1, 2]), toList(['h1']))
  assert.equal(receiptSessionId(receipt).fields[0], 's1')
  assert.equal(receiptJournalSequence(receipt), 42n)
  assert.equal(receiptSnapshotDigest(receipt), 'snap')
  assert.deepEqual([...receiptResolvedClaims(receipt)], [1, 2])
  assert.deepEqual([...receiptRestoredHandles(receipt)], ['h1'])

  const one = nonEmptyOne(5)
  assert.equal(one.Head, 5)
  const ofList = nonEmptyOfList(toList([1, 2, 3]))
  assert.equal(ofList !== undefined, true)
  assert.deepEqual([...nonEmptyToList(ofList)], [1, 2, 3])
  assert.deepEqual([...nonEmptyToList(nonEmptyMap((x) => x * 2, ofList))], [2, 4, 6])
  assert.equal(nonEmptyOfList(toList([])), undefined)
})
