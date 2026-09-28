import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { default: test } = await import('node:test')
const root = '../../../dist'
const Fold = await import(`${root}/Composition/Durable/Fold.js`)
const Lookup = await import(`${root}/Execution/Delegation/DurableChildLookup.js`)
const Recovery = await import(`${root}/OpenCode/Host/SessionBindingRecovery.js`)
const BindingSurface = await import(`${root}/OpenCode/Host/SessionBindingSurface.js`)
const FissionRuntime = await import(`${root}/Execution/Fission/Runtime.js`)
const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
const Roles = await import(`${root}/Foundation/Roles.js`)
const Identity = await import(`${root}/Foundation/Identity.js`)
const Fact = await import(`${root}/Composition/Durable/Fact.js`)

const sessionId = (value) => Identity.SessionIdModule_create(value)
const raw = (value) => (value !== null && typeof value === 'object' && Array.isArray(value.fields) ? value.fields[0] : value)

// WHAT[crash-reconciliation-021]: the in-process registries are a cache of what
// this process currently drives. Existence is answered by the durable projection,
// and a cache miss resolves on demand instead of answering "unknown".
const projectWithChild = () => {
  const linked = Fold.foldFact(
    Fold.empty,
    new Fact.Fact(1, [
      new Fact.AgentFact(3, [
        new DelegationFacts.ExecutionFactCases(0, [
          {
            ParentSessionId: sessionId('ses-road-root'),
            ChildSessionId: sessionId('ses-engineer-child'),
            Handle: new Identity.HandleId(0, [Identity.AgentHandleIdModule_create('w3ci5f')]),
            TargetAgent: 'engineer',
            Byname: 'decision-record-readonly',
            CanonicalRole: Roles.Role.Engineer,
            Ownership: DelegationFacts.HandleOwnership.DurableParentHandle,
          },
        ]),
      ]),
    ]),
  )

  assert.equal(linked.tag, 0, 'the handle link must fold')
  return linked.fields[0].AgentProjections
}

test('WHAT[crash-reconciliation-021] CRASH_021_durable_lookup_answers_without_any_process_registration', () => {
  const projections = projectWithChild()
  const handles = projections.Sessions.get(sessionId('ses-road-root')).Handles

  assert.equal(raw(Lookup.byHandleId(handles, 'w3ci5f')[0]), 'ses-engineer-child')
  assert.equal(raw(Lookup.byByname(handles, 'decision-record-readonly')[0]), 'ses-engineer-child')
  assert.equal(Lookup.byHandleId(handles, 'unknown') ?? null, null)
})

test('WHAT[crash-reconciliation-021] CRASH_021_binding_cache_miss_resolves_from_durable_evidence', () => {
  const projections = projectWithChild()

  BindingSurface.drop('ses-engineer-child')
  Recovery.installFrom(() => projections)

  assert.equal(BindingSurface.tryParent('ses-engineer-child'), 'ses-road-root')
  assert.equal(BindingSurface.tryAgent('ses-engineer-child'), 'engineer')
})

test('WHAT[crash-reconciliation-021] CRASH_021_fission_lane_cache_miss_resolves_from_durable_evidence', () => {
  const lane = sessionId('ses-fission-lane')

  FissionRuntime.FissionRuntime_installDurableLaneEvidence((laneSessionId) =>
    raw(laneSessionId) === 'ses-fission-lane'
      ? { GroupId: 'group-1', OwnerSessionId: sessionId('ses-owner'), LaneIndex: 0, LaneCount: 2 }
      : null,
  )

  const binding = FissionRuntime.FissionRuntime_tryLane(lane)

  assert.ok(binding, 'a cache miss must resolve from durable evidence')
  assert.equal(raw(binding.OwnerSessionId), 'ses-owner')
})
}
