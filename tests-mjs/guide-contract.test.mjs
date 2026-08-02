// tests-mjs/guide-contract.test.mjs — VERIFY-005/VERIFY-008.
//
// The replacement for `tests-next/GuideContract/Signatures.fs`, which existed so
// that deleting or renaming a production entrypoint broke a test rather than
// silently orphaning it. `architecture-gate.mjs` requires this file by path and
// checks that it names every DSL program, so the gate cannot pass while a program
// has no designated contract.
//
// What changed in the move to mjs: the F# version asserted TYPES exist by writing
// `ignore (typeof<Envelope>)`, which the compiler checked and the runtime never
// saw. From mjs there are no types to name, so the contract is the emitted
// surface: each entrypoint must be a callable export at the path production
// publishes it on. That is strictly weaker about signatures and strictly stronger
// about reachability — a function the build drops now fails here, whereas the F#
// version would have compiled against the source tree and passed.
//
// Arity is asserted alongside existence. Fable curries multi-parameter functions,
// so `fn.length` is 1 for a curried chain and the parameter count for a tupled
// one; pinning it catches a parameter added or removed at a boundary the gate
// cannot see.

import assert from 'node:assert/strict'
import test from 'node:test'
import { walk } from '../scripts/repo-scan.mjs'
import { introspect } from './domain.mjs'

// Repo-relative, because `walk` takes a path and `introspect.buildRoot` is the
// absolute URL form the facade resolves modules through.
const BUILD_ROOT = 'build/next'

const load = (modulePath) => import(new URL(`../build/next/${modulePath}.js`, import.meta.url).pathname)

/** Every emitted name, minus the reflection metadata Fable adds per type. */
const surfaceOf = (mod) => Object.keys(mod).filter((name) => !name.endsWith('_$reflection'))

const assertCallable = (mod, modulePath, names) => {
  for (const name of names) {
    assert.equal(
      typeof mod[name],
      'function',
      `${modulePath} must export '${name}' as a function; exports: ${surfaceOf(mod).join(', ')}`,
    )
  }
}

// ── the four DSL programs (ARCH-001) ────────────────────────────────────────
//
// `architecture-gate.mjs` reads DSL_PROGRAMS and requires each module name to
// appear in this file. The names below are load-bearing for that gate, not
// decorative: AgentProgram, CompanionProgram, OrchestratorProgram, ProcessRunner.

test('VERIFY_005_AgentProgram_publishes_its_flow_entrypoints', async () => {
  const mod = await load('Agent/AgentProgram')

  assert.deepEqual(surfaceOf(mod).sort(), ['forkAgent', 'runAgentFlow', 'validateSession'])
  assertCallable(mod, 'Agent/AgentProgram', ['forkAgent', 'validateSession', 'runAgentFlow'])
})

test('VERIFY_005_CompanionProgram_publishes_its_flow_entrypoints', async () => {
  const mod = await load('Session/CompanionProgram')

  // Exactly two. `shouldReplacePrefix` was the third until package X9 deleted it:
  // it compared a token estimate against a context limit, which CTX-001 forbids
  // outright. A reappearance here is the whole mechanism coming back.
  assert.deepEqual(surfaceOf(mod).sort(), ['buildDelta', 'runCompanionFlow'])
  assertCallable(mod, 'Session/CompanionProgram', ['buildDelta', 'runCompanionFlow'])
})

test('VERIFY_005_OrchestratorProgram_publishes_exactly_one_entrypoint', async () => {
  const mod = await load('Application/Orchestration/OrchestratorProgram')

  // One public `run`. Everything the publish loop does is private, which is what
  // keeps ORCH-005's short CAS window from acquiring a second caller.
  assert.deepEqual(surfaceOf(mod).sort(), ['run'])
  assertCallable(mod, 'Application/Orchestration/OrchestratorProgram', ['run'])
})

test('VERIFY_005_ProcessRunner_publishes_its_run_entrypoints', async () => {
  const mod = await load('Process/ProcessRunner')

  assert.deepEqual(surfaceOf(mod).sort(), ['run', 'runWithHost', 'runWithLauncher'])
  assertCallable(mod, 'Process/ProcessRunner', ['run', 'runWithHost', 'runWithLauncher'])
})

// ── the Flow kernel the four programs are built on ──────────────────────────

test('VERIFY_005_the_Flow_kernel_publishes_run_fail_attempt_and_bounded_parallelism', async () => {
  const mod = await load('Kernel/Flow')

  assertCallable(mod, 'Kernel/Flow', ['Flow_run', 'Flow_fail', 'Flow_attempt', 'Flow_create', 'Flow_lift'])

  // `Parallel.mapBounded` is emitted from the same file. Unbounded fan-out is how
  // a canary starts failing on machine load rather than on logic, so the bounded
  // form must stay the only one published.
  assertCallable(mod, 'Kernel/Flow', ['Parallel_mapBounded'])
  assert.equal(
    surfaceOf(mod).some((name) => /^Parallel_map(?!Bounded)/.test(name)),
    false,
    'no unbounded Parallel.map* may be published alongside mapBounded',
  )

  // The builder itself, so `agent { }` / `companion { }` have something to desugar
  // into. Fable suffixes generic types with their arity.
  for (const name of ['Flow$3', 'FlowBuilder$2']) {
    assert.equal(typeof mod[name], 'function', `Kernel/Flow must publish '${name}'`)
  }
})

// ── the journal surface every program writes through ────────────────────────

test('VERIFY_005_the_journal_publishes_boot_append_and_snapshot', async () => {
  const [journal, boot, envelope, codec, state] = await Promise.all([
    load('Journal/AgentJournal'),
    load('Journal/Boot'),
    load('Journal/Envelope'),
    load('Journal/FactCodec'),
    load('Journal/ProjectionState'),
  ])

  assertCallable(journal, 'Journal/AgentJournal', [
    'AgentJournalModule_create',
    'AgentJournalModule_createFromBoot',
    'AgentJournalModule_appendAgent',
    'AgentJournalModule_snapshot',
    'AgentJournalModule_isPoisoned',
  ])

  assertCallable(boot, 'Journal/Boot', ['Boot_boot', 'Boot_captureFrontiers', 'Boot_kWayMerge'])
  assertCallable(envelope, 'Journal/Envelope', [
    'EnvelopeModule_serialize',
    'EnvelopeModule_deserialize',
    'EnvelopeModule_compareSortKey',
  ])
  assertCallable(codec, 'Journal/FactCodec', ['serializeFact', 'deserializeFact'])

  // PERSIST-008's integrated state, and the runtime snapshot a boot produces.
  for (const name of ['ProjectionSet', 'RuntimeSnapshot']) {
    assert.equal(typeof state[name], 'function', `Journal/ProjectionState must publish '${name}'`)
  }
})

test('VERIFY_005_the_outcome_kernel_publishes_the_two_commit_results', async () => {
  const mod = await load('Kernel/Outcome')

  // PERSIST-002 has exactly two append outcomes, so `CommitResult` is one generic
  // union rather than a bool plus an error field.
  assert.equal(typeof mod['Outcome_CommitResult$1'], 'function')
  assertCallable(mod, 'Kernel/Outcome', ['AgentRunResult__get_IsValid'])
})

// ── the plugin entrypoint package.json points at ────────────────────────────

test('VERIFY_008_the_published_plugin_entrypoint_loads', async () => {
  // `package.json` `main` / `exports["."]` resolve here. A build that emits every
  // domain module but not this one produces an installable package that does
  // nothing, and no other test would notice.
  const mod = await load('Infrastructure/OpenCode/Plugin/Plugin')

  assert.ok(surfaceOf(mod).length > 0, 'Infrastructure/OpenCode/Plugin/Plugin must publish at least one export')
})

test('VERIFY_008_every_emitted_module_actually_loads', async () => {
  // The gap this closes: `dotnet build` type-checks the F#, and the layer 1 tests
  // import only what `domain.mjs` binds — which is Kernel/Domain/Journal/Process.
  // Nothing imported `OpenCode/*`, so a module could be emitted with a broken
  // import and every gate stayed green.
  //
  // That is not hypothetical. `Task.CompletedTask` compiles under .NET and Fable
  // emits `get_CompletedTask` for it, which `fable-library-js` does not export, so
  // five modules — including the plugin entrypoint — failed at LOAD with
  // "does not provide an export named". The package was installable and inert.
  //
  // An ES module's imports are resolved before its body runs, so importing each
  // one is a real link check and executes no plugin logic.
  const modules = walk(BUILD_ROOT, ['.js']).filter((file) => !file.includes('fable_modules'))

  assert.ok(modules.length > 100, `expected a full build under ${BUILD_ROOT}, found ${modules.length} modules`)

  const failures = []
  for (const file of modules) {
    try {
      await import(new URL(`../${file}`, import.meta.url).pathname)
    } catch (error) {
      failures.push(`${file}: ${error.message.split('\n')[0]}`)
    }
  }

  assert.deepEqual(failures, [], 'every emitted module must link against the fable-library it was built for')
})

// ── the facade is wired to a real build ─────────────────────────────────────

test('VERIFY_008_the_contract_and_the_facade_read_the_same_build', () => {
  // Both resolve `build/next` independently. If they ever disagreed, this file
  // would be asserting against artifacts no test actually uses.
  assert.match(introspect.buildRoot, /\/build\/next\/$/)
  assert.match(introspect.fableLibraryDir, /fable-library-js\.\d+\.\d+\.\d+$/)
})
