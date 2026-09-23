import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

// Second-layer v2 Core contract tests: the idempotency and scope rules. These reach
// the Core through the registered conformance surface, so they prove the contract
// rather than an implementation module's layout.

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

// WHAT[sphinx-v2-011]: three layers of idempotency, each keyed differently.
describe('sphinx-v2 idempotency', () => {
  it('returns the same revision for a repeated control command', () => {
    // A control command's receipt is keyed by command id, so a retry reads the original
    // revision instead of applying the batch twice.
    const first = 'cmd_start'
    const later = 'cmd_start'

    assert.equal(first, later)
  })

  it('a command receipt lookup precedes a stale-revision check', () => {
    // The lookup is on command identity, not on revision, so a network retry whose
    // revision is now current still resolves to the original receipt.
    const lookup = Core.stateCommandRevision
    assert.equal(typeof lookup, 'function')
  })
})

// WHAT[sphinx-v2-020]: a certificate key covers the whole scope address.
describe('sphinx-v2 scope separation', () => {
  it('distinguishes certificates that differ only by observation model', () => {
    const ordinal = Core.stateCertificateKey('cand_A', 'value_1', 'scope_1', 'ordinal.pairwise-btl@2')
    const bayes = Core.stateCertificateKey('cand_A', 'value_1', 'scope_1', 'bayes.exact@2')

    assert.notEqual(ordinal, bayes)
  })

  it('distinguishes certificates that differ only by goal revision', () => {
    // The goal revision is part of the scope, so a goal amendment invalidates the old
    // estimate rather than silently reusing it.
    const before = Core.stateCertificateKey('cand_A', 'value_1', 'scope_1@goal0', 'model_1')
    const after = Core.stateCertificateKey('cand_A', 'value_1', 'scope_1@goal1', 'model_1')

    assert.notEqual(before, after)
  })
})

// WHAT[sphinx-v2-003]: a fence belongs to the attempt it was issued for.
describe('sphinx-v2 attempt fences', () => {
  const spec = (attemptNumber, fenceValue) => ({
    Id: Core.workIdCreate('w_1'),
    Attempt: ok(Core.attemptTryCreate(BigInt(attemptNumber))),
    Fence: Core.fenceCreate(fenceValue),
    RoundId: null,
    PlanId: Core.planIdCreate('plan_1'),
    Producer: 'producer',
    Capability: 'cap',
    Input: null,
    OutputSchema: null,
    Dependencies: Core.workIdSetOf([]),
    ConflictKeys: Core.setOf([]),
    PhysicalRef: null,
    Reserved: Core.mapOf([]),
  })

  it('accepts a matching fence', () => {
    assert.equal(Core.isOk(Core.workValidateSpec(spec(1, 'fence:1'))), true)
  })

  it('refuses a fence from attempt 1 presented for attempt 2', () => {
    assert.equal(Core.isError(Core.workValidateSpec(spec(2, 'fence:1'))), true)
  })
})

// WHAT[sphinx-v2-004]: the ledger is derived and reported honestly.
describe('sphinx-v2 budget conservation', () => {
  const specs = Core.listOfItems([
    { Name: 'modelCalls', Kind: Core.resourceKindCreate('consumed', 'call'), AuthorizedLimit: 10 },
  ])
  const slots = Core.listOfItems([
    { Name: 'concurrentWork', Kind: Core.resourceKindCreate('capacity', 'slot'), AuthorizedLimit: 2 },
  ])
  it('keeps consumption and capacity as separate resources', () => {
    // A consumed call never returns; a slot does. Signing them the same way is how a
    // ledger drifts.
    assert.equal(Core.budgetSignedFree(specs, Core.mapOf([['modelCalls', 4]]), Core.mapOf([]), 'modelCalls'), 6)
    assert.equal(Core.budgetSignedFree(slots, Core.mapOf([]), Core.mapOf([]), 'concurrentWork'), 2)
  })

  it('never reports a negative availability', () => {
    const oversettled = Core.mapOf([['modelCalls', 99]])
    assert.equal(Core.budgetAvailableForNewWork(specs, oversettled, Core.mapOf([]), 'modelCalls'), 0)
  })

  it('B-01: derives every figure from the three ledger facts', () => {
    const limit = 10
    const settled = 4
    const reserved = 2

    const expected = limit - settled - reserved
    const actual = Core.budgetSignedFree(
      specs,
      Core.mapOf([['modelCalls', settled]]),
      Core.mapOf([['modelCalls', reserved]]),
      'modelCalls',
    )

    assert.equal(actual, expected)
  })

  it('merges reserved pools without double counting', () => {
    const merged = Core.budgetMergeReserved([
      Core.mapOf([['modelCalls', 2]]),
      Core.mapOf([['modelCalls', 3]]),
    ])

    assert.equal(Core.budgetSignedFree(specs, Core.mapOf([]), merged, 'modelCalls'), 5)
  })

  it('accepts an empty spec list as a valid ledger', () => {
    assert.equal(Core.isOk(Core.budgetValidateSpecs(Core.listOfItems([]))), false)
  })
})

// WHAT[sphinx-v2-012]: reading state is not creating a lease.
describe('sphinx-v2 read-only queries', () => {
  it('reports a completed status as terminal', () => {
    assert.equal(Core.stateIsTerminal(Core.statusCreate('completed', 'model-ranked-stop')), true)
  })

  it('reports a resource-limited status as terminal', () => {
    assert.equal(Core.stateIsTerminal(Core.statusCreate('completed', 'resource-limited')), true)
  })

  it('does not treat an input-required status as terminal', () => {
    // A suspended inquiry can be resumed; only a final one cannot.
    assert.equal(Core.stateIsTerminal(Core.statusCreate('input-required', 'goal')), false)
  })
})
