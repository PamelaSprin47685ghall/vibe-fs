import assert from 'node:assert/strict'
import { basename, join, resolve } from 'node:path'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { planOwnerCompile, compileOwnerProject } from '../../../scripts/lib/owner-compile.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')

const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')

const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })

const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })

assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))

const projects = [...subsystemInventory.projects.values()]

const projectByPath = new Map(projects.map((project) => [resolve(project.projectPath), project]))

const requireShard = (shard) => {
  const matches = projects.filter((project) => project.shard === shard)
  assert.equal(matches.length, 1, `${shard} must resolve to exactly one compile shard`)
  return matches[0]
}

const planShard = (shard) => {
  const project = requireShard(shard)
  return {
    project,
    plan: planOwnerCompile({ projectPath: project.projectPath, aggregatePath: null }),
  }
}

const productionSources = (plan) => plan.compileItems
  .filter((path) => path.endsWith('.fs'))
  .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))

const CONTRACT_SHARDS = [
  'eventstore-model-contract',
  'eventstore-port-contract',
  'eventstore-event-vocabulary-contract',
  'eventstore-git-contract',
  'strength-event-vocabulary-contract',
  'casebook-event-vocabulary-contract',
  'js-transaction-event-vocabulary-contract',
]

const FOCUSED_RUNTIME_SHARDS = [
  'eventstore-core-runtime',
  'eventstore-git-runtime',
]

// B4 expansion (2026-10-04, volume-02 build direction): the locality
// flat-compile probe set extended beyond EventStore into shards the
// W1/W3/W4 construction cards actually touch. Selection: (a) entry
// shards of active construction areas — interaction-authority (C1/IA018
// ledger+fact, IA017 surface), dispatch-protocol (DP002/session008),
// durable journal (DE023), context companion, and the session-ontology
// sync delegate whose missing edge was repaired in the previous batch
// (the positive now guards that edge); (b) shards whose closure the
// lexical audit flags for namespaces opened from outside it
// (Wanxiangshu.Participant.Provider et al.); (c) closure size within
// the 022 budget tiers — contract kind ≤100, focused runtime kind ≤185.
// Shards surveyed but over the focused budget stay out of the set
// (delegation-runtime-surface 474, execution-delegation-
// hostturnobservedsurface 448, join-guard-surface 453,
// composition-turn-scheduler 239, opencode-host-chatadmission-
// transaction 375, context-compression-runtime-surface 281,
// persistence-journal-surface 303, persistence-journal-
// obligationsurface 305, durable-runtime-surface 352,
// opencode-host-workspaceeventstore 529, opencode-host-
// turnruntimepreparation 528) — recorded in the package README.
// authority-runtime-surface joined the set (2026-10-04,
// shard-regrouping card) after its five out-of-closure namespace opens
// were resolved as dead opens: Child.fs / CompletedTurn.fs reference no
// symbol from Change, Context.Trace, Enforcer (incl. Enforcer.Guidance),
// Execution.Fission, or Persistence.EventStore, so deleting those open
// lines is a zero-behavior structural fix. The closure stays at 118
// production sources — inside the 185 focused-runtime ratchet (delegation
// WHAT-028) — with no ProjectReference edge added.
const EXTENDED_CONTRACT_LOCALITIES = [
  'interaction-authority-fold',
  'execution-session-syncdelegaterole',
  'journal-outcome-contract',
]

const EXTENDED_FOCUSED_RUNTIME_LOCALITIES = [
  'interaction-authority-fact',
  'interaction-authority-ledger',
  'authority-runtime-surface',
  'dispatch-runtime',
  'context-companion-fold',
  'persistence-journal-agentjournal',
]

const ALLOWED_CONTRACT_CLOSURE_SHARDS = new Set([
  'eventstore-model-contract',
  'eventstore-port-contract',
  'eventstore-event-vocabulary-contract',
  'eventstore-git-contract',
  'strength-event-vocabulary-contract',
  'casebook-event-vocabulary-contract',
  'js-transaction-event-vocabulary-contract',
  'identity',
  // Pure vocabulary tier retagged during B-series: Foundation.Outcome lives under
  // runtime-platform (the term/outcome root namespace) and is reachable from the
  // canonical model contract. This is a contract-tier provider, not a runtime artifact.
  'outcome',
  'foundation-roles',
  'runtime-platform-language',
  'runtime-platform-assemblyinfo',
])

test('WHAT[durable-events-022] EventStore contracts exclude physical and Strength runtime closure', () => {
  for (const shard of CONTRACT_SHARDS) {
    const { project, plan } = planShard(shard)
    assert.ok(
      ['persistence', 'strength', 'knowledge', 'repository-programming'].includes(project.subsystem),
      `${shard} belongs to expected contract subsystem`,
    )

    for (const projectPath of plan.projectPaths) {
      const provider = projectByPath.get(resolve(projectPath))
      assert.ok(
        ALLOWED_CONTRACT_CLOSURE_SHARDS.has(provider?.shard),
        `${shard} contract closure contains non-contract shard ${provider?.shardKey ?? basename(projectPath)}`,
      )
    }
  }

  const portSources = productionSources(planShard('eventstore-port-contract').plan)
  for (const forbidden of [
    'Persistence/EventStore/GitObjectDatabase.fs',
    'Persistence/EventStore/ProcessGitRawStore.fs',
    'Persistence/EventStore/ProcessEventLog.fs',
    'Persistence/EventStore/Store.fs',
    'Persistence/EventStore/CanonicalIntegrator.fs',
    'OpenCode/Host/WorkspaceEventStore.fs',
  ]) {
    assert.ok(!portSources.includes(forbidden), `EventStore.Port.Contract leaks ${forbidden}`)
  }

  const contractPlan = planShard('eventstore-event-vocabulary-contract').plan
  const contractShards = new Set(
    contractPlan.projectPaths.map((projectPath) => projectByPath.get(resolve(projectPath))?.shard),
  )
  for (const domainShard of [
    'strength-event-vocabulary-contract',
    'casebook-event-vocabulary-contract',
    'js-transaction-event-vocabulary-contract',
  ]) {
    assert.ok(
      !contractShards.has(domainShard),
      `eventstore-event-vocabulary-contract must not contain ${domainShard} in its transitive closure`,
    )
  }

  const contractSources = productionSources(contractPlan)
  assert.ok(!contractSources.includes('Strength/EventVocabulary.fs'))
  assert.ok(!contractSources.includes('Repository/Knowledge/Casebook/EventVocabulary.fs'))
  assert.ok(!contractSources.includes('Repository/Programming/Js/EventVocabulary.fs'))
  assert.ok(!contractSources.includes('Strength/Events.fs'))
  assert.ok(!contractSources.includes('Strength/Runtime.fs'))

  const assemblyPlan = planShard('eventstore-authoritative-vocabulary').plan
  const assemblyShards = new Set(
    assemblyPlan.projectPaths.map((projectPath) => projectByPath.get(resolve(projectPath))?.shard),
  )
  for (const domainShard of [
    'strength-event-vocabulary-contract',
    'casebook-event-vocabulary-contract',
    'js-transaction-event-vocabulary-contract',
  ]) {
    assert.ok(
      assemblyShards.has(domainShard),
      `eventstore-authoritative-vocabulary must contain ${domainShard} in its transitive closure`,
    )
  }

  const assemblySources = productionSources(assemblyPlan)
  assert.ok(assemblySources.includes('Strength/EventVocabulary.fs'))
  assert.ok(assemblySources.includes('Repository/Knowledge/Casebook/EventVocabulary.fs'))
  assert.ok(assemblySources.includes('Repository/Programming/Js/EventVocabulary.fs'))
  assert.ok(!assemblySources.includes('Strength/Events.fs'))
  assert.ok(!assemblySources.some((path) => path.startsWith('Strength/Prediction/')))
  assert.ok(!assemblySources.some((path) => path.startsWith('Strength/Replica/')))
  assert.ok(!assemblySources.includes('Strength/Runtime.fs'))
})

test('WHAT[durable-events-022] declared EventStore compile plans stay within source-count budgets', () => {
  for (const shard of CONTRACT_SHARDS) {
    const { plan } = planShard(shard)
    assert.ok(
      productionSources(plan).length <= 100,
      `${shard} contract closure exceeds 100 production sources`,
    )
  }

  for (const shard of FOCUSED_RUNTIME_SHARDS) {
    const { project, plan } = planShard(shard)
    assert.equal(project.subsystem, 'persistence', `${shard} must belong to persistence subsystem`)
    assert.ok(
      productionSources(plan).length <= 185,
      `${shard} runtime closure exceeds 185 production sources`,
    )
  }

  // Extended localities reuse the same 022 budget tiers per declared
  // LocalityKind (contract ≤100, focused runtime ≤185) — the same
  // numbers the EventStore tiers above enforce, no loosening. The
  // EventStore-specific closure whitelist and persistence-subsystem
  // assertions do not apply to the extended tiers; the flat-compile
  // positive and these budget bounds are what carry the 022 contract
  // for the extended set.
  for (const shard of EXTENDED_CONTRACT_LOCALITIES) {
    const { plan } = planShard(shard)
    assert.ok(
      productionSources(plan).length <= 100,
      `${shard} extended contract closure exceeds 100 production sources`,
    )
  }

  for (const shard of EXTENDED_FOCUSED_RUNTIME_LOCALITIES) {
    const { plan } = planShard(shard)
    assert.ok(
      productionSources(plan).length <= 185,
      `${shard} extended focused-runtime closure exceeds 185 production sources`,
    )
  }
})

test('WHAT[durable-events-022] Journal outcome contract excludes the store capability and physical journal', () => {
  const { project, plan } = planShard('journal-outcome-contract')
  assert.equal(project.subsystem, 'persistence')
  const sources = productionSources(plan)
  assert.ok(sources.includes('Persistence/Journal/Outcome.fs'))
  for (const source of [
    'Persistence/EventStore/Port.fs',
    'Persistence/EventStore/Store.fs',
    'Persistence/EventStore/ProcessEventLog.fs',
    'Persistence/Journal/Writer.fs',
    'Persistence/Journal/EventStoreJournalWriter.fs',
    'Persistence/Journal/AgentJournal.fs',
    'OpenCode/Host/SessionHostPort.fs',
  ]) {
    assert.ok(!sources.includes(source), `Journal outcome contract must not compile ${source}`)
  }
  const foundation = planShard('outcome').plan
  assert.ok(!productionSources(foundation).includes('Persistence/Journal/Outcome.fs'))
})

// B4 compile isolation for durable-events-022: the durable-events-023 probe
// pattern applied to the locality dimension. Positive: every bounded
// locality the WHAT-022 budgets name — the seven EventStore contract
// localities (100 production sources), the two focused EventStore runtime
// localities (185), plus the extended locality tiers (2026-10-04:
// contract tier interaction-authority-fold and
// execution-session-syncdelegaterole; focused tier
// interaction-authority-fact, interaction-authority-ledger,
// authority-runtime-surface, dispatch-runtime, context-companion-fold,
// persistence-journal-agentjournal) — flattens its declared ProjectReference
// closure into exactly one
// zero-ProjectReference project and compiles under a single Fable
// invocation. Negative: a temporary consumer that references the real Git
// runtime implementation or the real Host adapter must fail inside a
// contract locality's closure, because that reversed dependency is not part
// of the closure. Each probe first compiles green in the closure that
// legitimately owns the symbol, so the only remaining cause of each negative
// failure is the locality boundary — not probe syntax, not a missing
// reference, not toolchain drift.
integrationTest('WHAT[durable-events-022] each actual bounded locality compiles as one flat project and a reversed dependency is rejected', async () => {
  const { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } = await import('node:fs')
  const { createHash } = await import('node:crypto')
  const { dirname: dirnameOf, join: joinPath, relative: relativeOf } = await import('node:path')
  const { tmpdir } = await import('node:os')
  const { materializeOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')

  const LOCALITIES = [
    ...CONTRACT_SHARDS,
    ...FOCUSED_RUNTIME_SHARDS,
    ...EXTENDED_CONTRACT_LOCALITIES,
    ...EXTENDED_FOCUSED_RUNTIME_LOCALITIES,
  ]

  const scratch = mkdtempSync(joinPath(tmpdir(), 'wxs-de022-compile-'))
  try {
    // Positive: one flat project per locality. planOwnerCompile derives the
    // ordered compile list purely from the declared ProjectReference closure;
    // materializeOwnerCompile emits the flat fsproj; compileOwnerProject runs
    // one `dotnet tool run fable` invocation against it. A locality whose
    // closure is missing a declared edge fails here for real — that is a
    // shard-graph gap, not a test defect, and it must surface as red.
    for (const shard of LOCALITIES) {
      const project = requireShard(shard)
      const plan = planOwnerCompile({ projectPath: project.projectPath, aggregatePath: null })
      const materialized = materializeOwnerCompile(plan, { scratchRoot: scratch })
      const flatXml = readFileSync(materialized.projectPath, 'utf8')
      assert.ok(
        !flatXml.includes('<ProjectReference'),
        `${shard} flat project must carry zero ProjectReference`,
      )
      assert.equal(
        (flatXml.match(/<Compile Include=/g) ?? []).length,
        plan.compileItems.length,
        `${shard} flat project must carry the full closure compile list`,
      )
      const result = await compileOwnerProject({
        projectPath: project.projectPath,
        aggregatePath: null,
        scratchRoot: scratch,
        stdio: 'pipe',
        compilePlan: plan,
      })
      assert.equal(
        result.ok,
        true,
        `${shard} locality must compile as one flat project\n${result.stdout}\n${result.stderr}`,
      )
    }

    // Negative probes (WHAT-022: a business consumer's transitive compile
    // input must contain neither the Git object/ref implementation nor the
    // Host adapter; IGitRawStore stays in the physical-port contract and is
    // never exposed through EventStore.Port.Contract). Each probe names a
    // real public member of the higher layer.
    const GIT_RUNTIME_PROBE = [
      'namespace Wanxiangshu.Probe',
      '',
      'open Wanxiangshu.Persistence.EventStore',
      '',
      'module ProbeGitRuntimeUsage =',
      '    let runner = ProcessGitRawStore.createDefaultRunner',
      '',
    ].join('\n')

    const HOST_ADAPTER_PROBE = [
      'namespace Wanxiangshu.Probe',
      '',
      'open Wanxiangshu.OpenCode',
      '',
      'module ProbeHostAdapterUsage =',
      '    let program = WorkspaceEventStore.programWithoutRegistration',
      '',
    ].join('\n')

    // Source isolation: every compile input resolves to a copy under a temp
    // root, so the real workspace is never written and a crash mid-test
    // cannot leave the tree mutated. compileOwnerProject's scratchRoot only
    // isolates outputs; the compile items themselves are remapped here.
    const isolate = (shard, probeSource) => {
      const plan = planOwnerCompile({ projectPath: requireShard(shard).projectPath, aggregatePath: null })
      const iso = mkdtempSync(joinPath(tmpdir(), 'wxs-de022-iso-'))
      const items = plan.compileItems.map((item) => {
        const dest = joinPath(iso, 'src', relativeOf(SOURCE_ROOT, item))
        mkdirSync(dirnameOf(dest), { recursive: true })
        cpSync(item, dest)
        return dest
      })
      const probe = joinPath(iso, 'probe-reversed-dependency.fs')
      writeFileSync(probe, probeSource)
      return { shard, plan: { ...plan, compileItems: [...items, probe] }, iso }
    }

    const digestOf = (file) => ({
      hash: createHash('sha256').update(readFileSync(file)).digest('hex'),
      mtime: statSync(file).mtimeMs,
    })

    // Isolation evidence: the real owner sources keep their bytes and mtime.
    const realStoreTypes = joinPath(SOURCE_ROOT, 'Persistence/EventStore/StoreTypes.fs')
    const realModel = joinPath(SOURCE_ROOT, 'Persistence/EventStore/Model.fs')
    const realGitRuntime = joinPath(SOURCE_ROOT, 'Persistence/EventStore/ProcessGitRawStore.fs')
    const realHostAdapter = joinPath(SOURCE_ROOT, 'OpenCode/Host/WorkspaceEventStore.fs')
    const before = [digestOf(realStoreTypes), digestOf(realModel), digestOf(realGitRuntime), digestOf(realHostAdapter)]

    // Negative A: the port contract must not admit the Git runtime
    // implementation. Positive control: the same probe compiles in the
    // eventstore-git-runtime closure, so the symbol and its qualified usage
    // are valid.
    const gitRuntimePositive = isolate('eventstore-git-runtime', GIT_RUNTIME_PROBE)
    const gitRuntimeNegative = isolate('eventstore-port-contract', GIT_RUNTIME_PROBE)
    // Negative B: the model contract must not admit the Host adapter.
    // Positive control: the same probe compiles in the host workspace
    // event-store closure that owns the adapter.
    const hostAdapterPositive = isolate('opencode-host-workspaceeventstore', HOST_ADAPTER_PROBE)
    const hostAdapterNegative = isolate('eventstore-model-contract', HOST_ADAPTER_PROBE)
    try {
      const compileIsolated = async ({ shard, plan }) => compileOwnerProject({
        projectPath: requireShard(shard).projectPath,
        aggregatePath: null,
        scratchRoot: scratch,
        stdio: 'pipe',
        compilePlan: plan,
      })

      const gitPositive = await compileIsolated(gitRuntimePositive)
      assert.equal(
        gitPositive.ok,
        true,
        'probe compiles in the git-runtime closure (symbol and usage are valid): '
          + String(gitPositive.stdout ?? '').slice(-400),
      )

      const gitNegative = await compileIsolated(gitRuntimeNegative)
      assert.equal(
        gitNegative.ok,
        false,
        'the git-runtime probe must fail in the eventstore-port-contract closure',
      )
      const gitOutput = String(gitNegative.stdout ?? '') + String(gitNegative.stderr ?? '')
      assert.match(gitOutput, /ProcessGitRawStore/, 'the diagnostic names the git runtime implementation module')
      assert.match(gitOutput, /is not defined/, 'the diagnostic is a not-defined rejection, not a syntax or toolchain error')

      const hostPositive = await compileIsolated(hostAdapterPositive)
      assert.equal(
        hostPositive.ok,
        true,
        'probe compiles in the host workspace-event-store closure (symbol and usage are valid): '
          + String(hostPositive.stdout ?? '').slice(-400),
      )

      const hostNegative = await compileIsolated(hostAdapterNegative)
      assert.equal(
        hostNegative.ok,
        false,
        'the host-adapter probe must fail in the eventstore-model-contract closure',
      )
      const hostOutput = String(hostNegative.stdout ?? '') + String(hostNegative.stderr ?? '')
      assert.match(hostOutput, /OpenCode|WorkspaceEventStore/, 'the diagnostic names the host adapter namespace or module')
      assert.match(hostOutput, /is not defined/, 'the diagnostic is a not-defined rejection, not a syntax or toolchain error')

      // Isolation evidence: the real owner sources were never touched.
      const after = [digestOf(realStoreTypes), digestOf(realModel), digestOf(realGitRuntime), digestOf(realHostAdapter)]
      assert.deepEqual(after, before, 'the real owner sources are untouched (bytes and mtime)')
    } finally {
      rmSync(gitRuntimePositive.iso, { recursive: true, force: true })
      rmSync(gitRuntimeNegative.iso, { recursive: true, force: true })
      rmSync(hostAdapterPositive.iso, { recursive: true, force: true })
      rmSync(hostAdapterNegative.iso, { recursive: true, force: true })
    }
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
})

integrationTest('WHAT[durable-events-022] real Fable compiles the journal observation owner using its declared closure', async () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wxs-journal-owner-compile-'))
  try {
    const result = await compileOwnerProject({
      projectPath: join(SOURCE_ROOT, 'Wanxiangshu.Owner.verification-system.verification-eventstorewritersurface.fsproj'),
      aggregatePath: null,
      scratchRoot,
      rootPropsPath: join(ROOT, 'Directory.Build.props'),
      stdio: 'pipe',
    })
    assert.equal(result.ok, true, `journal observation owner must compile without undeclared siblings\n${result.stdout}\n${result.stderr}`)
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

integrationTest('WHAT[durable-events-022] Snapshot recovery flat compile preserves bounded Journal outcomes and the private Host witness owner', async (t) => {
  const { cpSync, mkdirSync, readFileSync, statSync, writeFileSync } = await import('node:fs')
  const { createHash } = await import('node:crypto')
  const { dirname, relative } = await import('node:path')
  const { materializeOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wxs-snapshot-boundary-'))
  const copies = []
  const sourceDigests = new Map()

  const SNAPSHOT_PROBE = [
    'namespace Wanxiangshu.Probe',
    'open Wanxiangshu.Foundation.Identity',
    'open Wanxiangshu.OpenCode',
    'module ProbeSnapshotConsumer =',
    '    let read (port: ISessionSnapshotPort) (session: SessionId) =',
    '        port.GetMessages(session)',
    '',
  ].join('\n')

  const HOST_ACCEPTANCE_PROBE = [
    'namespace Wanxiangshu.Probe',
    'open Wanxiangshu.OpenCode',
    'open Wanxiangshu.Execution.Session.ChatExecution',
    'module ProbeHostAcceptance =',
    '    let retire (port: IExternalInputSupersessionPort)',
    '               (prior: ChatExecutionKey)',
    '               (replacement: ManagedChatAcceptanceWitness) =',
    '        port.InterruptSupersededAttempt(prior, replacement)',
    '',
  ].join('\n')

  const JOURNAL_OUTCOME_PROBE = [
    'namespace Wanxiangshu.Probe',
    'open Wanxiangshu.Foundation.Identity',
    'open Wanxiangshu.Persistence.Journal.JournalOutcome',
    'module ProbeJournalOutcome =',
    '    let eventId = EventId.create "contract-probe"',
    '    let outcomes: CommitResult<int> list =',
    '        [ Committed 1; Rejected(eventId, "cut");',
    '          NotAttempted(eventId, WriterClosing);',
    '          CommitUnknown(eventId, WriteFailed "write") ]',
    '    let failures =',
    '        [ WriteUnknown(eventId, WriteFailed "write");',
    '          WriteUnknown(eventId, FlushFailed "flush");',
    '          WriterUnavailable(eventId, WriterPoisoned "first");',
    '          WriterUnavailable(eventId, WriterClosing);',
    '          WriterUnavailable(eventId, WriterDisposed);',
    '          FactRejected(eventId, { Fact = "fact"; Reason = "cut" }) ]',
    '    let diagnostics = failures |> List.map JournalAppendFailure.describe',
    '',
  ].join('\n')

  const digestOf = (file) => ({
    hash: createHash('sha256').update(readFileSync(file)).digest('hex'),
    mtime: statSync(file).mtimeMs,
  })

  const compileProbe = async (shard, probeSource) => {
    const { project, plan } = planShard(shard)
    const copyRoot = mkdtempSync(join(tmpdir(), 'wxs-snapshot-input-'))
    copies.push(copyRoot)
    const items = plan.compileItems.map((file) => {
      if (!sourceDigests.has(file)) sourceDigests.set(file, digestOf(file))
      const destination = join(copyRoot, 'src', relative(SOURCE_ROOT, file))
      mkdirSync(dirname(destination), { recursive: true })
      cpSync(file, destination)
      return destination
    })
    const probe = join(copyRoot, 'probe.fs')
    writeFileSync(probe, probeSource)
    const isolatedPlan = { ...plan, compileItems: [...items, probe] }
    const flat = materializeOwnerCompile(isolatedPlan, { scratchRoot })
    const flatXml = readFileSync(flat.projectPath, 'utf8')
    assert.ok(!flatXml.includes('<ProjectReference'), `${shard} must compile one flat project`)
    assert.equal((flatXml.match(/<Compile Include=/g) ?? []).length, isolatedPlan.compileItems.length)
    return compileOwnerProject({
      projectPath: project.projectPath,
      aggregatePath: null,
      scratchRoot,
      stdio: 'pipe',
      compilePlan: isolatedPlan,
    })
  }

  try {
    await t.test('WHAT[durable-events-022] full Host contract compiles with the original private acceptance witness', async () => {
      const result = await compileProbe('host-session-contract', HOST_ACCEPTANCE_PROBE)
      assert.equal(result.ok, true, `the full Host owner must retain its real acceptance witness\n${result.stdout}\n${result.stderr}`)
    })

    await t.test('WHAT[durable-events-022] actual recovery compiles with only its Snapshot read capability', async () => {
      const result = await compileProbe('delegation-recovery-runtime', SNAPSHOT_PROBE)
      assert.equal(result.ok, true, `actual recovery must retain the Snapshot read capability\n${result.stdout}\n${result.stderr}`)
    })

    await t.test('WHAT[durable-events-022] actual recovery rejects the identical full Host acceptance probe', async () => {
      const result = await compileProbe('delegation-recovery-runtime', HOST_ACCEPTANCE_PROBE)
      assert.equal(result.ok, false, 'the identical full Host acceptance probe must fail in the actual recovery closure')
      const rejection = String(result.stdout ?? '') + String(result.stderr ?? '')
      assert.match(rejection, /IExternalInputSupersessionPort|ManagedChatAcceptanceWitness|ChatExecution/)
      assert.match(rejection, /is not defined/, 'the real boundary must reject the Host symbol, not probe syntax or the toolchain')
    })

    await t.test('WHAT[durable-events-022] standalone Snapshot contract compiles its real read capability', async () => {
      const result = await compileProbe('host-session-snapshot-contract', SNAPSHOT_PROBE)
      assert.equal(result.ok, true, `the standalone Snapshot contract must compile without full Host admission\n${result.stdout}\n${result.stderr}`)
    })

    await t.test('WHAT[durable-events-022] standalone Journal outcome contract compiles every original result and diagnostic case', async () => {
      const result = await compileProbe('journal-outcome-contract', JOURNAL_OUTCOME_PROBE)
      assert.equal(result.ok, true, `the pure Journal contract must retain its original typed results\n${result.stdout}\n${result.stderr}`)
    })

    await t.test('WHAT[durable-events-022] standalone Journal outcome contract rejects the original full Host acceptance probe', async () => {
      const result = await compileProbe('journal-outcome-contract', HOST_ACCEPTANCE_PROBE)
      assert.equal(result.ok, false, 'the pure Journal result contract must reject the identical full Host acceptance capability')
      const rejection = String(result.stdout ?? '') + String(result.stderr ?? '')
      assert.match(rejection, /IExternalInputSupersessionPort|ManagedChatAcceptanceWitness|ChatExecution/)
      assert.match(rejection, /is not defined/)
    })
  } finally {
    for (const copyRoot of copies) rmSync(copyRoot, { recursive: true, force: true })
    rmSync(scratchRoot, { recursive: true, force: true })
    for (const [file, digest] of sourceDigests) {
      assert.deepEqual(digestOf(file), digest, `${file} must retain its original bytes and mtime`)
    }
  }
})
