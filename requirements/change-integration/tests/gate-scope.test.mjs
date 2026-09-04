import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[CHGINT-001] fresh quality candidate runs the full publish lifecycle to Published', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.verdict, { kind: 'Published', detail: 'rebased-1' })
  assert.deepEqual(observation.invalidations, ['InitialRebaseRequired'])
  assert.deepEqual(observation.successors, ['PostRebaseIndependentAssessment'])

  assert.deepEqual(observation.timeline, [
    'await:QualityCandidateAccepted',
    'fact:CandidateReady',
    'invalidate:InitialRebaseRequired',
    'git:rebase',
    'fact:RebasedCandidateReady',
    'successor:PostRebaseIndependentAssessment',
    'await:QualityCandidateAccepted',
    'gate:acquire',
    'fact:PublishClaimed',
    'git:ff',
    'fact:Published',
    'relay:terminate',
    'gate:release',
    'relay:terminate',
  ])
})

test('WHAT[CHGINT-010] rebase work holds the gate only for the ff mutation', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [true])
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
  assert.equal(observation.gateHeldAfterRun, false)
})

test('WHAT[CHGINT-005] rebase conflict records machine fact and requests ordinary successor', async () => {
  const observation = await change.observeRelayProgram('rebase-conflict')

  assert.deepEqual(observation.facts, ['CandidateReady', 'ConflictDetected'])
  assert.deepEqual(observation.invalidations, ['InitialRebaseRequired'])
  assert.deepEqual(observation.successors, ['RebaseConflict'])
})

test('WHAT[CHGINT-010] conflict resolution never acquires the publish gate', async () => {
  const observation = await change.observeRelayProgram('rebase-conflict')

  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateHeldAfterRun, false)
})

test('WHAT[CHGINT-013] target movement before publish invalidates the certificate and requests a successor without entering the gate', async () => {
  const observation = await change.observeRelayProgram('target-moved')

  assert.deepEqual(observation.invalidations, ['TargetAdvanced'])
  assert.deepEqual(observation.successors, ['PostRebaseIndependentAssessment'])
  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-013] CAS miss invalidates certificate rebases and requests successor after releasing the gate', async () => {
  const observation = await change.observeRelayProgram('cas-miss')

  assert.deepEqual(observation.ffGateHeld, [true])
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
  assert.deepEqual(observation.invalidations, ['PublishCasMissed'])
  assert.deepEqual(observation.successors, ['PostRebaseIndependentAssessment'])
  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.equal(observation.facts.includes('Published'), false)

  const release = observation.timeline.indexOf('gate:release')
  const invalidate = observation.timeline.indexOf('invalidate:PublishCasMissed')
  const rebase = observation.timeline.indexOf('git:rebase')
  const successor = observation.timeline.indexOf('successor:PostRebaseIndependentAssessment')
  assert.ok(release < invalidate && invalidate < rebase && rebase < successor)
})

test('WHAT[CHGINT-014] stale certificate never reaches publish gate', async () => {
  const observation = await change.observeRelayProgram('stale-certificate')

  assert.deepEqual(observation.invalidations, ['WorkspaceChangedAfterAssessment'])
  assert.deepEqual(observation.successors, ['WorkspaceChangedAfterAssessment'])
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.deepEqual(observation.facts, [])
})

test('WHAT[CHGINT-005] artifact conflict requests ordinary successor outside the gate', async () => {
  const observation = await change.observeRelayProgram('artifact-conflict')

  assert.deepEqual(observation.facts, ['ConflictDetected'])
  assert.deepEqual(observation.invalidations, ['ArtifactAdmissionUnmerged'])
  assert.deepEqual(observation.successors, ['ArtifactAdmissionUnmerged'])
})

test('WHAT[CHGINT-014] Git conflict facts override model-perfect publication', async () => {
  const observation = await change.observeRelayProgram('artifact-conflict')

  assert.deepEqual(observation.facts, ['ConflictDetected'])
  assert.equal(observation.facts.includes('Published'), false)
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
})

test('WHAT[CHGINT-001] retirement without a valid certificate creates an ordinary successor', async () => {
  const observation = await change.observeRelayProgram('retired')

  assert.deepEqual(observation.successors, ['IndependentAssessmentRequired'])
  assert.deepEqual(observation.invalidations, [])
  assert.equal(observation.timeline[0], 'await:IncumbencyRetired')
  assert.equal(observation.timeline[1], 'successor:IndependentAssessmentRequired')
})
