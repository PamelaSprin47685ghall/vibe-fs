// Transition entry: re-exports the family adapters under domain/.
//
// tests/unit/support/domain.mjs was split by family (Wave 1, Proposal ch. 19):
// the anti-corruption boundary is now the domain/ directory, not one physical
// file. This module stays as a zero-migration facade — every existing
// `import ... from '../support/domain.mjs'` keeps working unchanged.
//
// New tests should import from '../support/domain/<family>.mjs' directly.
// The Fable mechanics (loading, emitted-name resolution, map/list/set helpers)
// live in domain/interop.mjs; family adapters build on it.

export * from './domain/interop.mjs'
export * from './domain/identity.mjs'
export * from './domain/journal.mjs'
export * from './domain/persist.mjs'
export * from './domain/context.mjs'
export * from './domain/execution.mjs'
export * from './domain/prompt.mjs'
export * from './domain/enforcer.mjs'
export * from './domain/orchestrator.mjs'
export * from './domain/host.mjs'
