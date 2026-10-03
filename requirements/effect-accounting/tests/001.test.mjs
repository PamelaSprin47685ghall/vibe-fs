import assert from 'node:assert/strict'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import * as change from '../../../dist/Change/Surface.js'
import * as codec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

test('WHAT[effect-accounting-001] actual worktree projection distinguishes intent and confirmation', () => {
  const empty = change.empty()
  const requested = change.requestWorktree(empty, 'worktree', '/repo/worktree', 'job')
  const created = change.acceptWorktree(requested, 'worktree', '/repo/worktree', 'job')
  assert.equal(change.worktreeEffect(empty, 'worktree'), null)
  assert.equal(change.worktreeEffect(requested, 'worktree'), 'Requested')
  assert.equal(change.worktreeEffect(created, 'worktree'), 'Created')
})

test('WHAT[effect-accounting-001] intent and confirmation round-trip through distinct production fact cases', () => {
  const payload = { ManagerJobId: 'job', WorktreeIdentity: 'worktree', WorktreePath: '/repo/worktree' }
  const cases = ['WorktreeCreateRequested', 'WorktreeCreated']
  const bytes = cases.map((name) => codec.encode({ family: 'Orchestrator', case: name, payload }))
  assert.notEqual(bytes[0], bytes[1])
  for (let index = 0; index < cases.length; index += 1) {
    const decoded = codec.decode(bytes[index])
    assert.equal(decoded.ok, true, decoded.error)
    assert.equal(decoded.case, cases[index])
    assert.equal(decoded.line, bytes[index])
  }
})

// B4 compile isolation for effect-accounting-001. The probes compile against
// the real change-fact shard closure: Change/Fact.fsi exposes the intent and
// confirmation constructors, Change/Projection.fsi exposes the public claim
// admission (OrchestratorProjection.recordPublishClaimed) that the fold and
// the Surface share.
//
// Publish family (WHAT[effect-accounting-001], WHAT[effect-accounting-008]):
// the request claim payload (six evidence fields) and the physical
// confirmation payload (three outcome fields) are distinct record shapes, so
// offering the confirmation witness where the claim admission demands the
// request witness — or the request payload to the confirmation constructor —
// must fail at the Fable type boundary, not as a syntax or missing-reference
// error. The positive probe proves every symbol, reference and value is legal
// first, so the only remaining cause of the negative failures is the witness
// type boundary.
//
// Worktree family: WorktreeCreateRequested and WorktreeCreated carry
// structurally identical payloads, so their request/confirmation distinction
// lives in the distinct OrchestratorFactCases constructors consumed by an
// exhaustive fold match (proven behaviorally by the two tests above). Per the
// card's stop rule we do not mint a test-only witness type; the positive probe
// below proves both constructors compile green with their own payloads.
integrationTest('WHAT[effect-accounting-001] real negative compilation rejects confusing effect intent with physical confirmation', async () => {
  const { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } = await import('node:fs')
  const { createHash } = await import('node:crypto')
  const { dirname: dirnameOf, join: joinPath, relative: relativeOf, resolve: resolveRoot } = await import('node:path')
  const { tmpdir } = await import('node:os')
  const { compileOwnerProject, planOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')

  const ROOT = resolveRoot(import.meta.dirname, '../../..')
  const SOURCE_ROOT = joinPath(ROOT, 'src/Wanxiangshu')
  const CHANGE_FACT_SHARD = joinPath(
    SOURCE_ROOT,
    'Wanxiangshu.Owner.change-integration.change-fact.fsproj',
  )

  const POSITIVE_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Change',
    'open Wanxiangshu.Foundation.Identity',
    'open Wanxiangshu.Mission.Relay',
    '',
    'module LegalIntentAndConfirmation =',
    '    let worktreeRequested =',
    '        OrchestratorFact.WorktreeCreateRequested',
    '            {| ManagerJobId = ManagerJobId.create "job"',
    '               WorktreeIdentity = WorktreeIdentity.create "worktree"',
    '               WorktreePath = WorktreePath.create "/repo/worktree" |}',
    '',
    '    let worktreeCreated =',
    '        OrchestratorFact.WorktreeCreated',
    '            {| ManagerJobId = ManagerJobId.create "job"',
    '               WorktreeIdentity = WorktreeIdentity.create "worktree"',
    '               WorktreePath = WorktreePath.create "/repo/worktree" |}',
    '',
    '    let publishClaimed =',
    '        OrchestratorFact.PublishClaimed',
    '            {| ManagerJobId = ManagerJobId.create "job"',
    '               TargetRef = TargetRef.create "refs/heads/main"',
    '               RebasedCommit = CommitHash.create "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"',
    '               ExpectedHead = CommitHash.create "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"',
    '               WorkspaceSnapshotId = WorkspaceSnapshotId.create "snapshot"',
    '               QualityCertificateId = QualityCertificateId.create "certificate"',
    '               AuthorityRevision = AuthorityRevision.create "revision" |}',
    '',
    '    let published =',
    '        OrchestratorFact.Published',
    '            {| ManagerJobId = ManagerJobId.create "job"',
    '               CandidateCommit = CommitHash.create "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"',
    '               ResultingTargetHead = CommitHash.create "cccccccccccccccccccccccccccccccccccccccc" |}',
    '',
    '    let admittedClaim =',
    '        OrchestratorProjection.recordPublishClaimed',
    '            (ManagerJobId.create "job")',
    '            {| TargetRef = TargetRef.create "refs/heads/main"',
    '               RebasedCommit = CommitHash.create "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"',
    '               ExpectedHead = CommitHash.create "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"',
    '               WorkspaceSnapshotId = WorkspaceSnapshotId.create "snapshot"',
    '               QualityCertificateId = QualityCertificateId.create "certificate"',
    '               AuthorityRevision = AuthorityRevision.create "revision" |}',
    '            OrchestratorProjection.empty',
    '',
  ].join('\n')

  // Negative probe A: the physical confirmation payload (three outcome fields)
  // is offered where the claim admission requires the six-field request
  // witness record.
  const CONFIRMATION_AS_CLAIM_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Change',
    'open Wanxiangshu.Foundation.Identity',
    '',
    'module ConfirmationWitnessAsClaimAdmission =',
    '    let confirmationPayload =',
    '        {| ManagerJobId = ManagerJobId.create "job"',
    '           CandidateCommit = CommitHash.create "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"',
    '           ResultingTargetHead = CommitHash.create "cccccccccccccccccccccccccccccccccccccccc" |}',
    '',
    '    let admitted =',
    '        OrchestratorProjection.recordPublishClaimed',
    '            confirmationPayload.ManagerJobId',
    '            confirmationPayload',
    '            OrchestratorProjection.empty',
    '',
  ].join('\n')

  // Negative probe B: the request intent payload (seven fields) is passed to
  // the physical confirmation constructor (three fields).
  const INTENT_AS_CONFIRMATION_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Change',
    'open Wanxiangshu.Foundation.Identity',
    'open Wanxiangshu.Mission.Relay',
    '',
    'module IntentPayloadAsConfirmationConstructor =',
    '    let intentPayload =',
    '        {| ManagerJobId = ManagerJobId.create "job"',
    '           TargetRef = TargetRef.create "refs/heads/main"',
    '           RebasedCommit = CommitHash.create "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"',
    '           ExpectedHead = CommitHash.create "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"',
    '           WorkspaceSnapshotId = WorkspaceSnapshotId.create "snapshot"',
    '           QualityCertificateId = QualityCertificateId.create "certificate"',
    '           AuthorityRevision = AuthorityRevision.create "revision" |}',
    '',
    '    let confusedConfirmation = OrchestratorFact.Published intentPayload',
    '',
  ].join('\n')

  // Source isolation: every compile input resolves to a copy under a temp
  // root, so the real workspace is never written and a crash mid-test cannot
  // leave the tree mutated. compileOwnerProject's scratchRoot only isolates
  // outputs; the compile items themselves are remapped here.
  const isolate = (plan, probeSource) => {
    const iso = mkdtempSync(joinPath(tmpdir(), 'wxs-effect001-iso-'))
    const items = plan.compileItems.map((item) => {
      const dest = joinPath(iso, 'src', relativeOf(SOURCE_ROOT, item))
      mkdirSync(dirnameOf(dest), { recursive: true })
      cpSync(item, dest)
      return dest
    })
    const probe = joinPath(iso, 'probe-effect-witness.fs')
    writeFileSync(probe, probeSource)
    return { plan: { ...plan, compileItems: [...items, probe] }, iso }
  }

  const digestOf = (file) => ({
    hash: createHash('sha256').update(readFileSync(file)).digest('hex'),
    mtime: statSync(file).mtimeMs,
  })

  // Isolation evidence: the real owner sources keep their bytes and mtime.
  const realFact = joinPath(SOURCE_ROOT, 'Change/Fact.fs')
  const realProjection = joinPath(SOURCE_ROOT, 'Change/Projection.fs')
  const before = [digestOf(realFact), digestOf(realProjection)]

  const scratch = mkdtempSync(joinPath(tmpdir(), 'wxs-effect001-compile-'))
  const positiveIso = isolate(planOwnerCompile({ projectPath: CHANGE_FACT_SHARD }), POSITIVE_PROBE)
  const confirmationAsClaimIso = isolate(
    planOwnerCompile({ projectPath: CHANGE_FACT_SHARD }),
    CONFIRMATION_AS_CLAIM_PROBE,
  )
  const intentAsConfirmationIso = isolate(
    planOwnerCompile({ projectPath: CHANGE_FACT_SHARD }),
    INTENT_AS_CONFIRMATION_PROBE,
  )
  try {
    // Positive: intent and confirmation each constructed by its own
    // constructor, and the claim witness admitted through the public
    // projection admission. This proves every referenced symbol, every
    // dependency and every value in the probes is legal.
    const positive = await compileOwnerProject({
      projectPath: CHANGE_FACT_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: positiveIso.plan,
    })
    assert.equal(
      positive.ok,
      true,
      'legal intent/confirmation construction must compile: ' +
        String(positive.stdout ?? '').slice(-400),
    )

    // Negative A: the confirmation witness must be rejected by the claim
    // admission. The positive probe already ruled out syntax, missing
    // references and missing dependencies, so this failure is the type
    // boundary refusing the swapped witness.
    const confirmationAsClaim = await compileOwnerProject({
      projectPath: CHANGE_FACT_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: confirmationAsClaimIso.plan,
    })
    assert.equal(
      confirmationAsClaim.ok,
      false,
      'the physical confirmation witness must not be admissible where the claim witness is required',
    )
    assert.match(
      String(confirmationAsClaim.stdout ?? '') + String(confirmationAsClaim.stderr ?? ''),
      /expected to have type|but here has|should have fields|does not exactly match|type mismatch|incompatible|FS0001/i,
      'the diagnostic must be a type-boundary rejection, not a syntax or dependency error',
    )

    // Negative B: the intent payload must be rejected by the confirmation
    // constructor.
    const intentAsConfirmation = await compileOwnerProject({
      projectPath: CHANGE_FACT_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: intentAsConfirmationIso.plan,
    })
    assert.equal(
      intentAsConfirmation.ok,
      false,
      'the request intent payload must not construct a physical confirmation fact',
    )
    assert.match(
      String(intentAsConfirmation.stdout ?? '') + String(intentAsConfirmation.stderr ?? ''),
      /expected to have type|but here has|should have fields|does not exactly match|type mismatch|incompatible|FS0001/i,
      'the diagnostic must be a type-boundary rejection, not a syntax or dependency error',
    )

    // Isolation evidence: the real owner sources were never touched.
    const after = [digestOf(realFact), digestOf(realProjection)]
    assert.deepEqual(after, before, 'the real Change sources are untouched (bytes and mtime)')
  } finally {
    rmSync(scratch, { recursive: true, force: true })
    rmSync(positiveIso.iso, { recursive: true, force: true })
    rmSync(confirmationAsClaimIso.iso, { recursive: true, force: true })
    rmSync(intentAsConfirmationIso.iso, { recursive: true, force: true })
  }
})
