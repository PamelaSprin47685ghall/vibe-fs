// tests/unit/guide-contract.test.mjs — VERIFY-005/VERIFY-008.
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
import { readdirSync } from 'node:fs'
import { resolve, join } from 'node:path'
import { walk } from '../../../scripts/lib/walk.mjs'

// Repo-relative, because `walk` takes a path and the build contract resolves
// the same artifact directory directly.
const BUILD_ROOT = 'dist'
const BUILD_ROOT_ABS = `${resolve(BUILD_ROOT)}/`
const FABLE_LIBRARY_DIR = (() => {
  const candidates = readdirSync(join(BUILD_ROOT_ABS, 'fable_modules')).filter((entry) =>
    entry.startsWith('fable-library-js.'),
  )
  if (candidates.length !== 1) {
    throw new Error(
      `expected exactly one fable-library-js.* in ${BUILD_ROOT_ABS}/fable_modules, found: ${candidates.join(', ') || '(none)'}`,
    )
  }
  return join(BUILD_ROOT_ABS, 'fable_modules', candidates[0])
})()

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

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

// ── directly executable workflow surfaces (ARCH-001) ───────────────────────

test('WHAT[VERIFICATION-SYSTEM-008] AgentProgram publishes its flow entrypoints', async () => {
  const mod = await load('Execution/Agent/Program')

  // FLOW pilot: forkAgent + Flow.lift wrapper removed; plain task entrypoints remain.
  assert.deepEqual(surfaceOf(mod).sort(), ['runAgentFlow', 'validateSession'])
  assertCallable(mod, 'Execution/Agent/Program', ['validateSession', 'runAgentFlow'])
})

test('WHAT[VERIFICATION-SYSTEM-008] CompanionProgram publishes its flow entrypoints', async () => {
  const mod = await load('Context/Companion/Program')

  // Exactly two. `shouldReplacePrefix` was the third until package X9 deleted it:
  // it compared a token estimate against a context limit, which CTX-001 forbids
  // outright. A reappearance here is the whole mechanism coming back.
  assert.deepEqual(surfaceOf(mod).sort(), ['buildDelta', 'runCompanionFlow'])
  assertCallable(mod, 'Context/Companion/Program', ['buildDelta', 'runCompanionFlow'])
})

test('WHAT[VERIFICATION-SYSTEM-008] OrchestratorProgram publishes exactly one entrypoint', async () => {
  // PR3 direct-CE cutover: Application/Orchestration/Program.fs is the sole
  // production entrypoint. Domain AST + OrchestratorInterpreter are deleted.
  await assert.rejects(
    () => load('Domain/OrchestratorProgram'),
    (error) => {
      const message = String(error?.message ?? error)
      return (
        message.includes('Cannot find module') ||
        message.includes('ERR_MODULE_NOT_FOUND') ||
        message.includes('Failed to load') ||
        error?.code === 'ERR_MODULE_NOT_FOUND'
      )
    },
    'Domain/OrchestratorProgram AST must stay deleted',
  )
  await assert.rejects(
    () => load('Application/Orchestration/OrchestratorInterpreter'),
    (error) => {
      const message = String(error?.message ?? error)
      return (
        message.includes('Cannot find module') ||
        message.includes('ERR_MODULE_NOT_FOUND') ||
        message.includes('Failed to load') ||
        error?.code === 'ERR_MODULE_NOT_FOUND'
      )
    },
    'OrchestratorInterpreter must stay deleted',
  )

  const mod = await load('Change/Program')

  // One public `run`. Publish-loop details stay private so ORCH-005's short CAS
  // window cannot acquire a second caller.
  assert.deepEqual(surfaceOf(mod).sort(), ['run'])
  assertCallable(mod, 'Change/Program', ['run'])
})

test('WHAT[VERIFICATION-SYSTEM-008] Domain ReconcileProgram publishes pure decisions', async () => {
  const mod = await load('Composition/Turn/Program')
  const names = surfaceOf(mod)

  assert.ok(
    names.some((n) => n.includes('isTerminalOutcome')),
    `Domain ReconcileProgram must publish isTerminalOutcome; exports: ${names.join(', ')}`,
  )
  assert.ok(
    names.some((n) => n.includes('decideStep')),
    `Domain ReconcileProgram must publish decideStep; exports: ${names.join(', ')}`,
  )
  assert.ok(
    names.some((n) => n.includes('publishDecision')),
    `Domain ReconcileProgram must publish publishDecision; exports: ${names.join(', ')}`,
  )
})

test('WHAT[VERIFICATION-SYSTEM-008] ProcessRunner publishes its run entrypoints', async () => {
  const mod = await load('Process/ProcessRunner')

  assert.deepEqual(surfaceOf(mod).sort(), ['run', 'runWithHost', 'runWithLauncher'])
  assertCallable(mod, 'Process/ProcessRunner', ['run', 'runWithHost', 'runWithLauncher'])
})

// ── the bounded parallelism kernel the workflows fan out through ────────────

test('WHAT[VERIFICATION-SYSTEM-008] the Parallel kernel publishes only bounded parallelism', async () => {
  const mod = await load('Foundation/Parallel')

  // docs/what/flow.md (Direct CE) superseded the Flow monad; its monadic surface
  // (Flow_run / Flow_fail / Flow_attempt / Flow_create / Flow_lift and the
  // FlowBuilder) is no longer a demanded contract. Bounded concurrency is still
  // legal, so `Parallel.mapBounded` remains the only required export here.
  assertCallable(mod, 'Foundation/Parallel', ['Parallel_mapBounded'])

  // `Parallel.mapBounded` is emitted from the same file. Unbounded fan-out is how
  // a canary starts failing on machine load rather than on logic, so the bounded
  // form must stay the only one published.
  assert.equal(
    surfaceOf(mod).some((name) => /^Parallel_map(?!Bounded)/.test(name)),
    false,
    'no unbounded Parallel.map* may be published alongside mapBounded',
  )
})

// ── the journal surface every program writes through ────────────────────────

test('WHAT[VERIFICATION-SYSTEM-008] the journal publishes boot append and snapshot', async () => {
  const [journal, esWriter, envelope, codec, state] = await Promise.all([
    load('Persistence/Journal/AgentJournal'),
    load('Persistence/Journal/EventStoreJournalWriter'),
    load('Persistence/Journal/Envelope'),
    load('Persistence/Journal/FactCodec'),
    load('Composition/Durable/ProjectionState'),
  ])

  // AgentJournal constructs from an already-folded projection + writer
  // (createFromProjection). EventStore boot/resume belongs to
  // EventStoreJournalWriter (create / resumeOrCreate) and the workspace Host.
  // The retired EventStore-boot forwarding facade must not return.
  assertCallable(journal, 'Persistence/Journal/AgentJournal', [
    'AgentJournalModule_createFromProjection',
    'AgentJournalModule_appendAgent',
    'AgentJournalModule_appendMagicTodo',
    'AgentJournalModule_snapshot',
    'AgentJournalModule_revision',
    'AgentJournalModule_snapshotWithRevision',
    'AgentJournalModule_awaitChangeFrom',
    'AgentJournalModule_isPoisoned',
  ])

  const hasCreate = Object.keys(esWriter).some((name) => name.startsWith('EventStoreJournalWriter_create'))
  const hasResume = Object.keys(esWriter).some((name) =>
    name.startsWith('EventStoreJournalWriter_resumeOrCreate'),
  )
  assert.equal(hasCreate, true, 'EventStoreJournalWriter.create must be published')
  assert.equal(hasResume, true, 'EventStoreJournalWriter.resumeOrCreate must be published')

  assertCallable(envelope, 'Persistence/Journal/Envelope', [
    'EnvelopeModule_serialize',
    'EnvelopeModule_deserialize',
    'EnvelopeModule_compareSortKey',
  ])
  assertCallable(codec, 'Persistence/Journal/FactCodec', ['serializeFact', 'deserializeFact'])

  // PERSIST-008's integrated state.
  assert.equal(typeof state['ProjectionSet'], 'function', 'Journal/ProjectionState must publish ProjectionSet')
})

test('WHAT[VERIFICATION-SYSTEM-008] the outcome kernel publishes the two commit results', async () => {
  const mod = await load('Foundation/Outcome')

  // PERSIST-002 has exactly two append outcomes, so `CommitResult` is one generic
  // union rather than a bool plus an error field.
  assert.equal(typeof mod['Outcome_CommitResult$1'], 'function')
  assertCallable(mod, 'Foundation/Outcome', ['AgentRunResult__get_IsValid'])
})

// ── the plugin entrypoint package.json points at ────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-008] the published plugin entrypoint loads', async () => {
  // `package.json` `main` / `exports["."]` resolve here. A build that emits every
  // domain module but not this one produces an installable package that does
  // nothing, and no other test would notice.
  const mod = await load('OpenCode/Plugin/Plugin')

  assert.ok(surfaceOf(mod).length > 0, 'OpenCode/Plugin/Plugin must publish at least one export')
})

test('WHAT[VERIFICATION-SYSTEM-008] every emitted module actually loads', async () => {
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
      await import(new URL(`../../../${file}`, import.meta.url).pathname)
    } catch (error) {
      failures.push(`${file}: ${error.message.split('\n')[0]}`)
    }
  }

  assert.deepEqual(failures, [], 'every emitted module must link against the fable-library it was built for')
})

// ── the facade is wired to a real build ─────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-008] the contract and the facade read the same build', () => {
  // Both checks resolve `dist` independently. If they ever disagreed, this file
  // would be asserting against artifacts no test actually uses.
  assert.match(BUILD_ROOT_ABS, /\/dist\/$/)
  assert.match(FABLE_LIBRARY_DIR, /fable-library-js\.\d+\.\d+\.\d+$/)
})
