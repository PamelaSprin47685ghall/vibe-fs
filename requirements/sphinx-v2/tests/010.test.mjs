import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

// Every assertion reaches the v2 Core through the one registered conformance surface,
// so the test proves the surface contract rather than an implementation module's
// internals. There is no Fable interop in this file: collections are built through
// `Core.setOf`/`Core.mapOf`, and results are read through `Core.isOk`/`Core.okValue`.

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

const goalSpec = (text, revision) =>
  Core.goalCreate({
    GoalId: Core.goalIdCreate('goal_1'),
    Revision: ok(Core.revisionTryCreate(BigInt(revision))),
    OriginalText: text,
    Constraints: Core.setOf([]),
    MaterialRefs: Core.setOf([]),
    AuthorizationRef: 'auth_1',
    CreatedBy: 'user',
    Amendments: Core.listOfItems([]),
  })

const resourceSpec = (name, unit, limit) => ({
  Name: name,
  Kind: Core.resourceKindCreate('consumed', unit),
  AuthorizedLimit: limit,
})

// WHAT[sphinx-v2-001]: only an authorized amendment moves the goal.
describe('sphinx-v2 goal ownership', () => {
  it('keeps the user text byte-exact', () => {
    const goal = goalSpec('讨论这个系统的新设计并给出可实施方案', 0)
    assert.equal(Core.goalTextOf(goal), '讨论这个系统的新设计并给出可实施方案')
  })

  it('refuses an amendment with no authorizer', () => {
    const before = goalSpec('原目标', 0)
    assert.equal(Core.isError(Core.goalTryAmend('', Core.listOfItems([]), null, before)), true)
  })

  it('advances the revision on an authorized replacement', () => {
    const before = goalSpec('原目标', 0)
    const amended = ok(Core.goalTryAmend('user', Core.listOfItems(['约束']), '新目标', before))

    assert.equal(Number(Core.goalRevisionValue(amended)), 1)
    assert.equal(Core.goalTextOf(amended), '新目标')
  })
})

// WHAT[sphinx-v2-010]: identifier hygiene.
describe('sphinx-v2 identities', () => {
  it('refuses a blank work id', () => {
    assert.equal(Core.isError(Core.workIdTryCreate('')), true)
  })

  it('refuses an id with interior whitespace', () => {
    assert.equal(Core.isError(Core.workIdTryCreate('w 1')), true)
  })

  it('refuses a negative revision', () => {
    assert.equal(Core.isError(Core.revisionTryCreate(-1n)), true)
  })

  it('refuses a zero attempt', () => {
    assert.equal(Core.isError(Core.attemptTryCreate(0n)), true)
  })
})

// WHAT[sphinx-v2-020]: the registered event vocabulary.
describe('sphinx-v2 event vocabulary', () => {
  it('registers exactly one v2 transition type', () => {
    assert.equal(Core.eventTransitionType, 'sphinx/v2-transition@1')

    const all = Core.eventAllTypes
    assert.equal(Core.listCount(all), 1)
    assert.equal(Core.listHead(all), 'sphinx/v2-transition@1')
  })
})

// WHAT[sphinx-v2-003]: the fence belongs to its attempt.
describe('sphinx-v2 work lifecycle', () => {
  const spec = (id, attemptNumber, fenceValue) => ({
    Id: Core.workIdCreate(id),
    Attempt: ok(Core.attemptTryCreate(BigInt(attemptNumber))),
    Fence: Core.fenceCreate(fenceValue),
    RoundId: null,
    PlanId: Core.planIdCreate('plan_1'),
    Producer: 'producer',
    Capability: 'cap',
    Input: null,
    OutputSchema: null,
    Dependencies: Core.setOf([]),
    ConflictKeys: Core.setOf([]),
    PhysicalRef: null,
    Reserved: Core.mapOf([]),
  })

  it('accepts a fence bound to its own attempt', () => {
    assert.equal(Core.isOk(Core.workValidateSpec(spec('w_1', 1, 'fence:1'))), true)
  })

  it('refuses a fence from a different attempt', () => {
    assert.equal(Core.isError(Core.workValidateSpec(spec('w_1', 2, 'fence:1'))), true)
  })

  it('refuses work that depends on itself', () => {
    const selfDependent = spec('w_1', 1, 'fence:1')
    selfDependent.Dependencies = Core.workIdSetOf(['w_1'])

    assert.equal(Core.isError(Core.workValidateSpec(selfDependent)), true)
  })
})

// WHAT[sphinx-v2-004]: the ledger is derived, never asserted.
describe('sphinx-v2 resource ledger', () => {
  const specs = Core.listOfItems([resourceSpec('modelCalls', 'call', 10)])

  it('derives available work from the three ledger facts', () => {
    const free = Core.budgetAvailableForNewWork(specs, Core.mapOf([]), Core.mapOf([]), 'modelCalls')
    assert.equal(free, 10)
  })

  it('subtracts settled usage', () => {
    const settled = Core.mapOf([['modelCalls', 4]])
    const free = Core.budgetAvailableForNewWork(specs, settled, Core.mapOf([]), 'modelCalls')
    assert.equal(free, 6)
  })

  it('B-06: reports an overrun rather than absorbing it', () => {
    // Settling 13 against a limit of 10 is an overrun of 3; the ledger records the fact
    // instead of rejecting the usage.
    const oversettled = Core.mapOf([['modelCalls', 13]])
    const overrun = Core.budgetObservedOverrun(specs, oversettled, Core.mapOf([]), 'modelCalls')
    assert.equal(overrun, 3)
  })

  it('B-02: refuses a reservation that would oversubscribe', () => {
    const reserved = Core.mapOf([['modelCalls', 6]])

    const result = Core.budgetTryReserve(specs, Core.mapOf([]), reserved, {
      WorkId: Core.workIdCreate('w_2'),
      Attempt: ok(Core.attemptTryCreate(1n)),
      Resources: Core.mapOf([['modelCalls', 6]]),
      MoneyMinor: null,
    })

    assert.equal(Core.isError(result), true)
    assert.equal(Core.errorCode(result), 'budget-insufficient')
  })

  it('refuses a reservation naming an unknown resource', () => {
    const result = Core.budgetTryReserve(specs, Core.mapOf([]), Core.mapOf([]), {
      WorkId: Core.workIdCreate('w_2'),
      Attempt: ok(Core.attemptTryCreate(1n)),
      Resources: Core.mapOf([['bogus', 1]]),
      MoneyMinor: null,
    })

    assert.equal(Core.isError(result), true)
    assert.equal(Core.errorCode(result), 'unknown-resource')
  })
})

// WHAT[sphinx-v2-005]: a guarantee states what may be claimed.
describe('sphinx-v2 certificate guarantees', () => {
  it('C-07: an empirical summary needs no coverage claim', () => {
    const guarantee = Core.guaranteeCreate('empirical-summary', Core.listOfItems(['assumption']))
    assert.equal(Core.isOk(Core.certificateValidateGuarantee(guarantee)), true)
  })

  it('C-06: a posterior credible mass outside (0,1) is refused', () => {
    const guarantee = Core.guaranteeCreate('posterior-credible', Core.listOfItems(['m', '1.4', 'approx']))
    assert.equal(Core.isError(Core.certificateValidateGuarantee(guarantee)), true)
  })

  it('refuses a frequentist delta outside (0,1)', () => {
    const guarantee = Core.guaranteeCreate('frequentist-coverage', Core.listOfItems(['ref', '0.0', 'scope']))
    assert.equal(Core.isError(Core.certificateValidateGuarantee(guarantee)), true)
  })

  it('accepts a well-formed model estimate', () => {
    const guarantee = Core.guaranteeCreate('posterior-credible', Core.listOfItems(['m', '0.9', 'approx']))
    assert.equal(Core.isOk(Core.certificateValidateGuarantee(guarantee)), true)
  })
})

// WHAT[sphinx-v2-020]: a certificate address is the whole scope.
describe('sphinx-v2 certificate addressing', () => {
  it('addresses a certificate by target, value space, scope and model', () => {
    const key = Core.stateCertificateKey('cand_A', 'value_1', 'scope_1', 'model_1')

    assert.equal(key.includes('cand_A'), true)
    assert.equal(key.includes('value_1'), true)
    assert.equal(key.includes('scope_1'), true)
    assert.equal(key.includes('model_1'), true)
  })
})

// WHAT[sphinx-v2-017]: terminal classification is a pure read, not a lease.
describe('sphinx-v2 status purity', () => {
  it('treats an active status as non-terminal', () => {
    assert.equal(Core.stateIsTerminal(Core.statusCreate('active', '')), false)
  })

  it('treats a completed status as terminal', () => {
    assert.equal(Core.stateIsTerminal(Core.statusCreate('completed', 'model-ranked-stop')), true)
  })

  it('treats a cancelled status as terminal', () => {
    assert.equal(Core.stateIsTerminal(Core.statusCreate('cancelled', 'user-cancelled')), true)
  })

  it('names each status without touching state', () => {
    assert.equal(Core.statusNameOf(Core.statusCreate('active', '')), 'active')
    assert.equal(Core.statusNameOf(Core.statusCreate('completed', 'model-ranked-stop')), 'completed')
  })
})

// WHAT[sphinx-v2-016]: a revision advances by exactly one.
describe('sphinx-v2 revisions', () => {
  it('advances by exactly one', () => {
    const origin = ok(Core.revisionTryCreate(0n))
    const next = Core.revisionNext(origin)
    assert.equal(Number(Core.revisionValue(next)), 1)
  })

  it('keeps the origin at zero', () => {
    assert.equal(Number(Core.revisionValue(Core.revisionOrigin)), 0)
  })
})
