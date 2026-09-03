import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import {
  checkOwnerProjects,
  projectArchitectureViolations,
  validateProjectContractEvidence,
} from '../../../scripts/checks/owner-projects.mjs'
import { buildTraceGraph } from '../../../scripts/lib/requirement-trace.mjs'
import { planOwnerCompile, materializeOwnerCompile, compileOwnerProject } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')

test('WHAT[STRUCTURED-WORKFLOW-011] flattened Fable emitter mirrors owner-locality source coverage', () => {
  const rootProject = readFileSync(join(SRC, 'Wanxiangshu.fsproj'), 'utf8')
  assert.match(rootProject, /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
  assert.doesNotMatch(rootProject, /<ProjectReference Include=/, 'emit project must not source-merge owner project graph')

  const ownerProjects = readdirSync(SRC).filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  assert.ok(ownerProjects.length > 1, '57.15 requires independent owner-locality projects')

  for (const project of ownerProjects) {
    const xml = readFileSync(join(SRC, project), 'utf8')
    assert.match(xml, /<WanxiangshuSemanticOwner>[^<]+<\/WanxiangshuSemanticOwner>/)
    assert.match(xml, /<WanxiangshuOwnerLocality>[^<]+<\/WanxiangshuOwnerLocality>/)
  }

  const props = readFileSync(join(SRC, 'Directory.Build.props'), 'utf8')
  assert.match(props, /<DisableTransitiveProjectReferences>true<\/DisableTransitiveProjectReferences>/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] owner-locality project graph is complete, authorized, and acyclic', () => {
  const result = checkOwnerProjects()
  assert.equal(result.ok, true, result.violations.join('\n'))
  assert.ok(result.sourceCount > 0, 'owner-locality graph must cover production sources')
  assert.equal(result.contractLeakSourceCount, 0, 'published contract compile closure must contain no runtime/private source')
})

test('WHAT[STRUCTURED-WORKFLOW-011] owner-project authorization rejects a comment-only semantic proof independently', () => {
  const result = validateProjectContractEvidence(
    {
      contracts: [
        {
          path: 'src/Wanxiangshu/ExternalInvestigation/Cursor.fs',
          owner: 'external-investigation',
          kind: 'semantic-evidence',
          consumers: ['consumer'],
          symbols: ['Wanxiangshu.ExternalInvestigation.Cursor.current'],
          law: 'WHAT[EXTERNAL-INVESTIGATION-010]',
          proof: {
            path: 'requirements/external-investigation/tests/browser-provenance-canary.test.mjs',
            title: 'WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office',
            what_id: 'EXTERNAL-INVESTIGATION-010',
          },
        },
      ],
    },
    buildTraceGraph(join(ROOT, 'requirements')),
  )

  assert.equal(result.contractManifest.contracts.length, 0)
  assert.match(result.violations.join('\n'), /invalid-semantic-evidence-metadata/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] GitGateway exact contract has an isolated compiler boundary', () => {
  const contracts = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contract = contracts.contracts.find((entry) => entry.path === 'src/Wanxiangshu/Git/Gateway.fs')
  assert.ok(contract)
  assert.deepEqual(contract.consumers, ['durable-convergence'])
  assert.deepEqual(contract.symbols, [
    'Wanxiangshu.Git.GitGateway.converge',
    'Wanxiangshu.Git.GitGateway.createDefaultRunner',
    'Wanxiangshu.Git.GitGatewayRunner',
  ])

  const providerName = 'Wanxiangshu.Owner.change-integration.git-gateway.fsproj'
  const provider = readFileSync(join(SRC, providerName), 'utf8')
  assert.match(provider, /<WanxiangshuOwnerLocality>git-gateway<\/WanxiangshuOwnerLocality>/)
  assert.match(provider, /<Compile Include="Git\/Gateway\.fsi"\s*\/>\s*<Compile Include="Git\/Gateway\.fs"\s*\/>/)
  assert.equal([...provider.matchAll(/<Compile Include="[^"]+\.fs"\s*\/>/g)].length, 1)

  const consumer = readFileSync(join(SRC, 'Wanxiangshu.Owner.durable-convergence.git-hook-sync.fsproj'), 'utf8')
  assert.match(consumer, /<ProjectReference Include="Wanxiangshu\.Owner\.change-integration\.git-gateway\.fsproj"\s*\/>/)
  assert.doesNotMatch(consumer, /ProjectReference Include="Wanxiangshu\.Owner\.change-integration\.git-integrationgate\.fsproj"/)

  const signature = readFileSync(join(SRC, 'Git/Gateway.fsi'), 'utf8')
  assert.doesNotMatch(signature, /SyncActiveEnv|discoverRemote/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] review opening prompt has a source-pure compiler boundary', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contract = manifest.contracts.find(
    (entry) => entry.path === 'src/Wanxiangshu/Mission/Review/Prompt.fs',
  )
  assert.ok(contract)
  assert.deepEqual(contract.consumers, ['change-integration', 'finality'])
  assert.deepEqual(contract.symbols, ['Wanxiangshu.Mission.Review.HostReviewPrompt.Opening'])

  const promptProject = 'Wanxiangshu.Owner.review-judgement.mission-review-prompt.fsproj'
  const mixedProject = 'Wanxiangshu.Owner.review-judgement.mission-review-fact.fsproj'
  const provider = readFileSync(join(SRC, promptProject), 'utf8')
  const compileItems = [...provider.matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(([, path]) => path)
  assert.match(provider, /<WanxiangshuOwnerLocality>mission-review-prompt<\/WanxiangshuOwnerLocality>/)
  assert.match(provider, /<WanxiangshuOwnerLocalityKind>contract<\/WanxiangshuOwnerLocalityKind>/)
  assert.deepEqual(compileItems, ['Mission/Review/Prompt.fsi', 'Mission/Review/Prompt.fs'])

  const mixed = readFileSync(join(SRC, mixedProject), 'utf8')
  assert.doesNotMatch(mixed, /<Compile Include="Mission\/Review\/Prompt\.(?:fsi|fs)"\s*\/>/)

  const references = (projectName) =>
    [...readFileSync(join(SRC, projectName), 'utf8').matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )
  const changeRefs = references('Wanxiangshu.Owner.change-integration.git-integrationgate.fsproj')
  const finalityRefs = references('Wanxiangshu.Owner.finality.mission-finality-prompt.fsproj')
  assert.ok(changeRefs.includes(promptProject))
  assert.ok(!changeRefs.includes(mixedProject))
  assert.ok(finalityRefs.includes(promptProject))
})

test('WHAT[STRUCTURED-WORKFLOW-011] review request identity and challenge have source-pure compiler boundaries', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contractAt = (path) => manifest.contracts.find((entry) => entry.path === path)
  const projectSource = (projectName) => readFileSync(join(SRC, projectName), 'utf8')
  const compileItems = (projectName) =>
    [...projectSource(projectName).matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(([, path]) => path)
  const references = (projectName) =>
    [...projectSource(projectName).matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )

  const mixedProject = 'Wanxiangshu.Owner.review-judgement.mission-review-fact.fsproj'
  const requestProject =
    'Wanxiangshu.Owner.review-judgement.mission-review-judgement-requestidentity.fsproj'
  const challengeProject = 'Wanxiangshu.Owner.review-judgement.mission-review-judgement-challenge.fsproj'
  const mixedSource = projectSource(mixedProject)

  assert.doesNotMatch(
    mixedSource,
    /<Compile Include="Mission\/Review\/Judgement\/(?:RequestIdentity|Challenge)\.(?:fsi|fs)"\s*\/>/,
  )
  assert.doesNotMatch(
    mixedSource,
    /ProjectReference Include="Wanxiangshu\.Owner\.provider-projection\.participant-provider-projection-model\.fsproj"/,
  )
  assert.deepEqual(compileItems(requestProject), [
    'Mission/Review/Judgement/RequestIdentity.fsi',
    'Mission/Review/Judgement/RequestIdentity.fs',
  ])
  assert.deepEqual(compileItems(challengeProject), [
    'Mission/Review/Judgement/Challenge.fsi',
    'Mission/Review/Judgement/Challenge.fs',
  ])
  assert.deepEqual(references(requestProject), [
    'Wanxiangshu.Owner.dispatch-protocol.foundation-identity.fsproj',
  ])
  assert.deepEqual(references(challengeProject), [
    'Wanxiangshu.Owner.provider-projection.participant-provider-projection-model.fsproj',
  ])

  assert.deepEqual(
    contractAt('src/Wanxiangshu/Mission/Review/Judgement/RequestIdentity.fs')?.consumers,
    ['host-boundary', 'managed-session-lifecycle'],
  )
  assert.deepEqual(
    contractAt('src/Wanxiangshu/Mission/Review/Judgement/RequestIdentity.fs')?.symbols,
    [
      'Wanxiangshu.Mission.Review.Judgement.JudgementRequestIdentity.belongsTo',
      'Wanxiangshu.Mission.Review.Judgement.JudgementRequestIdentity.key',
    ],
  )
  assert.deepEqual(contractAt('src/Wanxiangshu/Mission/Review/Judgement/Challenge.fs')?.consumers, [
    'review-assurance',
  ])
  assert.deepEqual(contractAt('src/Wanxiangshu/Mission/Review/Judgement/Challenge.fs')?.symbols, [
    'Wanxiangshu.Mission.Review.Judgement.ReviewChallenge.Path',
    'Wanxiangshu.Mission.Review.Judgement.ReviewChallenge.promptOf',
  ])

  const requestSignature = readFileSync(join(SRC, 'Mission/Review/Judgement/RequestIdentity.fsi'), 'utf8')
  assert.deepEqual(
    [...requestSignature.matchAll(/^\s*val ([A-Za-z0-9_]+):/gm)].map(([, name]) => name),
    ['key', 'belongsTo'],
  )
  const challengeSignature = readFileSync(join(SRC, 'Mission/Review/Judgement/Challenge.fsi'), 'utf8')
  assert.deepEqual(
    [...challengeSignature.matchAll(/^\s*val ([A-Za-z0-9_]+):/gm)].map(([, name]) => name),
    ['Path', 'promptOf'],
  )

  const hostRefs = references('Wanxiangshu.Owner.host-boundary.opencode-host-sharedstatesurface.fsproj')
  assert.ok(hostRefs.includes(requestProject))
  assert.ok(!hostRefs.includes(mixedProject))
  assert.ok(!hostRefs.includes(challengeProject))
  assert.ok(hostRefs.includes('Wanxiangshu.Owner.work-record.mission-workrecord-materialize-runtime.fsproj'))

  const managedRefs = references(
    'Wanxiangshu.Owner.managed-session-lifecycle.opencode-host-pluginruntimescope.fsproj',
  )
  assert.ok(managedRefs.includes(requestProject))
  assert.ok(!managedRefs.includes(mixedProject))
  assert.ok(!managedRefs.includes(challengeProject))

  const assuranceRefs = references('Wanxiangshu.Owner.review-assurance.mission-review-barrier-workflow.fsproj')
  assert.ok(assuranceRefs.includes(challengeProject))
  assert.ok(assuranceRefs.includes(mixedProject))
  assert.ok(!assuranceRefs.includes(requestProject))

  const judgeRefs = references('Wanxiangshu.Owner.review-judgement.mission-review-opencode-judgetool.fsproj')
  assert.ok(judgeRefs.includes(requestProject))
  assert.ok(judgeRefs.includes(challengeProject))
  assert.ok(!judgeRefs.includes(mixedProject))

  for (const unrelated of [
    'Wanxiangshu.Owner.durable-events.runtime.fsproj',
    'Wanxiangshu.Owner.finality.mission-finality-prompt.fsproj',
    'Wanxiangshu.Owner.change-integration.git-integrationgate.fsproj',
  ]) {
    assert.ok(!references(unrelated).includes(requestProject))
    assert.ok(!references(unrelated).includes(challengeProject))
  }

  const localities = manifest.compiler_boundary_localities
    .filter((entry) => entry.owner === 'review-judgement')
    .map((entry) => entry.locality)
  assert.ok(localities.includes('mission-review-judgement-requestidentity'))
  assert.ok(localities.includes('mission-review-judgement-challenge'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] review witness has a source-pure compiler boundary', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contract = manifest.contracts.find(
    (entry) => entry.path === 'src/Wanxiangshu/Mission/Review/Judgement/Witness.fs',
  )
  assert.ok(contract)
  assert.deepEqual(contract.consumers, ['durable-events', 'finality', 'review-assurance'])
  assert.deepEqual(contract.symbols, [
    'BarrierId',
    'First',
    'FirstPhysicalUserMessageId',
    'GitTreeHash',
    'Report',
    'Second',
    'SecondPhysicalUserMessageId',
    'Wanxiangshu.Mission.Review.Judgement.CandidateVerificationFailure',
    'Wanxiangshu.Mission.Review.Judgement.CandidateVerificationFailure.IncompleteCohort',
    'Wanxiangshu.Mission.Review.Judgement.CandidateVerificationFailure.StaleWitness',
    'Wanxiangshu.Mission.Review.Judgement.ConfirmedReviewWitness',
    'Wanxiangshu.Mission.Review.Judgement.ConfirmedReviewWitnessModule.create',
    'Wanxiangshu.Mission.Review.Judgement.ConfirmedReviewWitnessModule.gitTreeHash',
    'Wanxiangshu.Mission.Review.Judgement.ConfirmedReviewWitnessModule.lifeId',
    'Wanxiangshu.Mission.Review.Judgement.ConfirmedReviewWitnessModule.requestId',
    'Wanxiangshu.Mission.Review.Judgement.ReviewCandidate.isWitnessValidForTree',
    'Wanxiangshu.Mission.Review.Judgement.ReviewCandidate.verifyCandidate',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitness',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitness.Confirmed',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitness.NoReview',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitness.RevisionWitness',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.attemptIdentity',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.confirm',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.confirmedReviewer',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.gitTreeHash',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.isConfirmed',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.isDistinctAttempt',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.isRevision',
    'Wanxiangshu.Mission.Review.Judgement.ReviewWitnessModule.isValidForTree',
    'Wanxiangshu.Mission.Review.Judgement.VerdictWitness',
    'Wanxiangshu.Mission.Review.Judgement.VerdictWitness.GitTreeHash',
    'Wanxiangshu.Mission.Review.Judgement.VerdictWitness.ProviderRun',
    'Wanxiangshu.Mission.Review.Judgement.VerdictWitness.ReviewerSessionId',
    'Wanxiangshu.Mission.Review.Judgement.VerdictWitness.ToolCallId',
  ])

  const witnessProject = 'Wanxiangshu.Owner.review-judgement.mission-review-judgement-witness.fsproj'
  const mixedProject = 'Wanxiangshu.Owner.review-judgement.mission-review-fact.fsproj'
  const projectSource = (projectName) => readFileSync(join(SRC, projectName), 'utf8')
  const compileItems = (projectName) =>
    [...projectSource(projectName).matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(([, path]) => path)
  const references = (projectName) =>
    [...projectSource(projectName).matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )

  const provider = projectSource(witnessProject)
  assert.match(provider, /<WanxiangshuOwnerLocality>mission-review-judgement-witness<\/WanxiangshuOwnerLocality>/)
  assert.match(provider, /<WanxiangshuOwnerLocalityKind>contract<\/WanxiangshuOwnerLocalityKind>/)
  assert.deepEqual(compileItems(witnessProject), [
    'Mission/Review/Judgement/Witness.fsi',
    'Mission/Review/Judgement/Witness.fs',
  ])
  assert.deepEqual(references(witnessProject), [
    'Wanxiangshu.Owner.dispatch-protocol.foundation-identity.fsproj',
  ])
  assert.doesNotMatch(
    projectSource(mixedProject),
    /<Compile Include="Mission\/Review\/Judgement\/Witness\.(?:fsi|fs)"\s*\/>/,
  )

  const durableRefs = references('Wanxiangshu.Owner.durable-events.runtime.fsproj')
  const finalityRefs = references('Wanxiangshu.Owner.finality.mission-finality-prompt.fsproj')
  const projectionRefs = references('Wanxiangshu.Owner.review-assurance.mission-review-barrier-projection.fsproj')
  const workflowRefs = references('Wanxiangshu.Owner.review-assurance.mission-review-barrier-workflow.fsproj')
  const foldRefs = references('Wanxiangshu.Owner.review-judgement.mission-review-reviewfactfold.fsproj')
  assert.ok(durableRefs.includes(witnessProject) && durableRefs.includes(mixedProject))
  assert.ok(finalityRefs.includes(witnessProject) && !finalityRefs.includes(mixedProject))
  assert.ok(projectionRefs.includes(witnessProject) && !projectionRefs.includes(mixedProject))
  assert.ok(workflowRefs.includes(witnessProject) && workflowRefs.includes(mixedProject))
  assert.ok(foldRefs.includes(witnessProject) && foldRefs.includes(mixedProject))

  const signature = readFileSync(join(SRC, 'Mission/Review/Judgement/Witness.fsi'), 'utf8')
  assert.doesNotMatch(signature, /\bval (?:isQualifiedConfirmationFor|witnesses):/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] review fact routing has exact per-consumer authorization', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const factPath = 'src/Wanxiangshu/Mission/Review/Fact.fs'
  const contracts = manifest.contracts.filter((entry) => entry.path === factPath)
  const allFactSymbols = [
    'Wanxiangshu.Mission.Review.ReviewFact.ConfirmedReviewWitness',
    'Wanxiangshu.Mission.Review.ReviewFact.ReviewAttemptClosed',
    'Wanxiangshu.Mission.Review.ReviewFact.ReviewBarrierStarted',
    'Wanxiangshu.Mission.Review.ReviewFact.ReviewVerdictRecorded',
  ]
  assert.deepEqual(
    contracts.map(({ consumers, symbols }) => ({ consumers, symbols })),
    [
      { consumers: ['durable-events'], symbols: allFactSymbols },
      {
        consumers: ['review-assurance'],
        symbols: ['Wanxiangshu.Mission.Review.ReviewFact.ReviewBarrierStarted'],
      },
    ],
  )

  const factProject = 'Wanxiangshu.Owner.review-judgement.mission-review-fact.fsproj'
  const projectSource = (projectName) => readFileSync(join(SRC, projectName), 'utf8')
  const provider = projectSource(factProject)
  assert.match(provider, /<WanxiangshuOwnerLocalityKind>composition<\/WanxiangshuOwnerLocalityKind>/)
  assert.deepEqual(
    [...provider.matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(([, path]) => path),
    ['Mission/Review/Fact.fsi', 'Mission/Review/Fact.fs'],
  )
  assert.deepEqual(
    [...provider.matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(([, path]) => path),
    [
      'Wanxiangshu.Owner.dispatch-protocol.foundation-identity.fsproj',
      'Wanxiangshu.Owner.durable-events.composition-durable-fact.fsproj',
      'Wanxiangshu.Owner.review-judgement.mission-review-facts.fsproj',
    ],
  )

  const referencers = readdirSync(SRC)
    .filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name) && name !== factProject)
    .filter((name) => projectSource(name).includes(`ProjectReference Include="${factProject}"`))
    .sort()
  assert.deepEqual(referencers, [
    'Wanxiangshu.Owner.durable-events.runtime.fsproj',
    'Wanxiangshu.Owner.review-assurance.mission-review-barrier-workflow.fsproj',
    'Wanxiangshu.Owner.review-judgement.mission-review-reviewfactfold.fsproj',
  ])

  const locality = manifest.compiler_boundary_localities.find(
    (entry) => entry.owner === 'review-judgement' && entry.locality === 'mission-review-fact',
  )
  assert.match(locality?.justification ?? '', /ReviewFactCases to AgentFact routing/)
  assert.doesNotMatch(locality?.justification ?? '', /Prompt|Witness|Challenge|RequestIdentity/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] NodeFs physical port and tool contracts have isolated compiler boundaries', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contractAt = (path) => manifest.contracts.find((entry) => entry.path === path)
  const compileItems = (projectName) =>
    [...readFileSync(join(SRC, projectName), 'utf8').matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )
  const references = (projectName) =>
    [...readFileSync(join(SRC, projectName), 'utf8').matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )

  assert.deepEqual(contractAt('src/Wanxiangshu/OpenCode/Tools/ManagedAgent.fs')?.consumers, [
    'capability-enforcement',
    'change-integration',
    'context-compression',
    'delegation',
    'execution-model-routing',
    'finality',
    'host-boundary',
    'interaction-authority',
    'managed-session-lifecycle',
    'output-distillation',
    'participant-horizon',
    'process-execution',
    'speculative-investigation',
    'time-capability',
  ])
  assert.deepEqual(contractAt('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')?.consumers, [
    'capability-enforcement',
    'delegation',
    'host-boundary',
    'review-assurance',
    'review-judgement',
  ])

  const nodeFsPath = 'src/Wanxiangshu/OpenCode/Tools/NodeFs.fs'
  const nodeFsContract = contractAt(nodeFsPath)
  assert.equal(nodeFsContract?.kind, 'physical-port')
  assert.deepEqual(nodeFsContract?.consumers, ['repository-programming'])
  assert.deepEqual(nodeFsContract?.symbols, [
    'Wanxiangshu.OpenCode.NodeFs.cpSync',
    'Wanxiangshu.OpenCode.NodeFs.existsSync',
    'Wanxiangshu.OpenCode.NodeFs.readdirSync',
    'Wanxiangshu.OpenCode.NodeFs.renameSync',
    'Wanxiangshu.OpenCode.NodeFs.rmSync',
    'Wanxiangshu.OpenCode.NodeFs.statSync',
  ])

  const fileMutationAdapter = manifest.physical_adapters.find(
    (entry) => entry.path === 'src/Wanxiangshu/OpenCode/Tools/FileMutationTools.fs',
  )
  assert.ok(fileMutationAdapter)
  assert.equal(fileMutationAdapter.owner, 'repository-programming')
  assert.deepEqual(fileMutationAdapter.ports, [
    {
      path: nodeFsPath,
      symbols: nodeFsContract.symbols,
    },
  ])

  const managedProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-managedagent.fsproj'
  const staticProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-statictools.fsproj'
  const nodeFsProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-nodefs.fsproj'
  assert.deepEqual(compileItems(managedProject), ['OpenCode/Tools/ManagedAgent.fsi', 'OpenCode/Tools/ManagedAgent.fs'])
  assert.deepEqual(compileItems(staticProject), ['OpenCode/Tools/StaticTools.fsi', 'OpenCode/Tools/StaticTools.fs'])
  assert.deepEqual(compileItems(nodeFsProject), ['OpenCode/Tools/NodeFs.fsi', 'OpenCode/Tools/NodeFs.fs'])

  const staticSignature = readFileSync(join(SRC, 'OpenCode/Tools/StaticTools.fsi'), 'utf8')
  const staticImplementation = readFileSync(join(SRC, 'OpenCode/Tools/StaticTools.fs'), 'utf8')
  const fileMutationImplementation = readFileSync(join(SRC, 'OpenCode/Tools/FileMutationTools.fs'), 'utf8')
  assert.doesNotMatch(staticSignature, /\bmodule NodeFs\b/)
  assert.doesNotMatch(staticImplementation, /\bmodule NodeFs\b|\[<Import\([^\n]+, "fs"\)>\]/)
  assert.doesNotMatch(fileMutationImplementation, /\bmodule private NodeFs\b|\[<Import\([^\n]+, "fs"\)>\]/)

  const nodeFsSignature = readFileSync(join(SRC, 'OpenCode/Tools/NodeFs.fsi'), 'utf8')
  assert.deepEqual(
    [...nodeFsSignature.matchAll(/^\s*val ([A-Za-z0-9_]+):/gm)].map(([, name]) => name),
    ['readFileSync', 'writeFileSync', 'existsSync', 'statSync', 'readdirSync', 'renameSync', 'rmSync', 'cpSync'],
  )

  const capabilityRefs = references('Wanxiangshu.Owner.capability-enforcement.opencode-host-managedagentconfig.fsproj')
  assert.ok(capabilityRefs.includes(managedProject))
  assert.ok(capabilityRefs.includes(staticProject))
  assert.ok(capabilityRefs.includes('Wanxiangshu.Owner.host-boundary.sphinx-host-adapter.fsproj'))

  const reviewRefs = references('Wanxiangshu.Owner.review-judgement.mission-review-opencode-judgetool.fsproj')
  assert.ok(reviewRefs.includes(staticProject))
  assert.ok(!reviewRefs.includes(managedProject))

  const routingRefs = references('Wanxiangshu.Owner.execution-model-routing.opencode-host-modelroutingsurface.fsproj')
  assert.ok(routingRefs.includes(managedProject))
  assert.ok(!routingRefs.includes(staticProject))

  const actionRuntimeRefs = references('Wanxiangshu.Owner.action-affordance.runtime.fsproj')
  assert.ok(actionRuntimeRefs.includes(staticProject))
  assert.ok(actionRuntimeRefs.includes(nodeFsProject))
  assert.ok(!actionRuntimeRefs.includes(managedProject))

  const repositoryRefs = references('Wanxiangshu.Owner.repository-programming.opencode-tools-filemutationtools.fsproj')
  assert.ok(repositoryRefs.includes(nodeFsProject))

  const ptyToolRefs = references('Wanxiangshu.Owner.process-execution.opencode-tools-ptytool.fsproj')
  assert.ok(ptyToolRefs.includes('Wanxiangshu.Owner.delegation.delegation-pty-adapter.fsproj'))

  const joinToolRefs = references('Wanxiangshu.Owner.delegation.execution-delegation-hostturnobservedsurface.fsproj')
  assert.ok(joinToolRefs.includes('Wanxiangshu.Owner.delegation.delegation-pty-adapter.fsproj'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] request kind and fallback facts have disjoint compiler boundaries', () => {
  const manifest = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contractAt = (path) => manifest.contracts.find((entry) => entry.path === path)
  const compileItems = (projectName) =>
    [...readFileSync(join(SRC, projectName), 'utf8').matchAll(/<Compile Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )
  const references = (projectName) =>
    [...readFileSync(join(SRC, projectName), 'utf8').matchAll(/<ProjectReference Include="([^"]+)"\s*\/>/g)].map(
      ([, path]) => path,
    )

  const requestProject = 'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-requestkind.fsproj'
  const factsProject = 'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-fallback-facts.fsproj'

  assert.deepEqual(compileItems(requestProject), [
    'Participant/Provider/Attempt/RequestKind.fsi',
    'Participant/Provider/Attempt/RequestKind.fs',
  ])
  assert.deepEqual(compileItems(factsProject), [
    'Participant/Provider/Attempt/Fallback/Facts.fsi',
    'Participant/Provider/Attempt/Fallback/Facts.fs',
  ])

  assert.deepEqual(contractAt('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Facts.fs')?.consumers, [
    'durable-events',
    'verification-system',
  ])
  assert.deepEqual(contractAt('src/Wanxiangshu/Participant/Provider/Attempt/RequestKind.fs')?.consumers, [
    'capability-enforcement',
    'cognitive-environment',
    'context-compression',
    'delegation',
    'execution-failure-policy',
    'execution-model-routing',
    'host-boundary',
    'interaction-authority',
    'managed-chat-execution',
    'prefix-stability',
    'speculative-investigation',
  ])

  const capabilityRefs = references(
    'Wanxiangshu.Owner.capability-enforcement.opencode-host-managedagentconfig.fsproj',
  )
  assert.ok(capabilityRefs.includes(requestProject))
  assert.ok(!capabilityRefs.includes(factsProject))

  const durableFactRefs = references('Wanxiangshu.Owner.durable-events.composition-durable-fact.fsproj')
  assert.ok(durableFactRefs.includes(factsProject))
  assert.ok(!durableFactRefs.includes(requestProject))

  const durableCodecRefs = references('Wanxiangshu.Owner.durable-events.persistence-journal-promptfactcodec.fsproj')
  assert.ok(durableCodecRefs.includes(factsProject))
  assert.ok(!durableCodecRefs.includes(requestProject))

  const verificationRefs = references(
    'Wanxiangshu.Owner.verification-system.verification-eventstorewritersurface.fsproj',
  )
  assert.ok(verificationRefs.includes(factsProject))
  assert.ok(!verificationRefs.includes(requestProject))

  const ownerFallbackRefs = references(
    'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-fallback-fact.fsproj',
  )
  assert.ok(ownerFallbackRefs.includes(requestProject))
  assert.ok(ownerFallbackRefs.includes(factsProject))

  const localities = manifest.compiler_boundary_localities
    .filter((entry) => entry.owner === 'provider-attempt-recovery')
    .map((entry) => entry.locality)
  assert.ok(localities.includes('participant-provider-attempt-requestkind'))
  assert.ok(localities.includes('participant-provider-attempt-fallback-facts'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] locality kinds enforce contract purity, direction, and closure budget', () => {
  const contract = resolve('/architecture/Contract.fsproj')
  const nestedContract = resolve('/architecture/NestedContract.fsproj')
  const runtime = resolve('/architecture/Runtime.fsproj')
  const foreignRuntime = resolve('/architecture/ForeignRuntime.fsproj')
  const composition = resolve('/architecture/Composition.fsproj')
  const missing = resolve('/architecture/Missing.fsproj')
  const project = (projectPath, owner, kind, compile, references = []) => ({
    projectPath,
    owner,
    kind,
    compile,
    references,
  })

  const violations = projectArchitectureViolations(
    new Map([
      [runtime, project(runtime, 'provider', 'runtime', ['Runtime.fs'])],
      [foreignRuntime, project(foreignRuntime, 'foreign', 'runtime', ['ForeignRuntime.fs'])],
      [nestedContract, project(nestedContract, 'provider', 'contract', ['NestedA.fs', 'NestedB.fs'], [runtime])],
      [contract, project(contract, 'consumer', 'contract', ['Contract.fs'], [nestedContract, foreignRuntime])],
      [composition, project(composition, 'composition', 'composition', ['Composition.fs'], [foreignRuntime])],
      [missing, project(missing, 'missing', '', ['Missing.fs'])],
    ]),
    { contractSourceBudget: 2 },
  )

  assert.ok(violations.some((violation) => /Missing\.fsproj: missing WanxiangshuOwnerLocalityKind/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure contains non-contract .*Runtime\.fsproj/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure contains non-contract .*ForeignRuntime\.fsproj/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure has 5 production \.fs; budget is 2/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj -> .*ForeignRuntime\.fsproj: only composition may reference foreign runtime/.test(violation)))
  assert.ok(!violations.some((violation) => /\/Composition\.fsproj -> .*ForeignRuntime\.fsproj/.test(violation)))
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection planner produces exact closure and canonical aggregate order', () => {
  const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
  const leakyConsumerPath = join(FIXTURE, 'LeakyConsumer.fsproj')
  const leakyContractPath = join(FIXTURE, 'LeakyContract.fsproj')
  const runtimePath = join(FIXTURE, 'Runtime.fsproj')

  const plan = planOwnerCompile({
    projectPath: leakyConsumerPath,
    aggregatePath,
  })

  // Exact project closure
  const expectedProjects = [leakyConsumerPath, leakyContractPath, runtimePath].sort()
  assert.deepEqual(plan.projectPaths, expectedProjects)

  // Exact compile items filtered in Emitter.fsproj document order
  const expectedCompileItems = [
    join(FIXTURE, 'Runtime.fs'),
    join(FIXTURE, 'LeakyContract.fs'),
    join(FIXTURE, 'LeakyConsumer.fs'),
  ]
  assert.deepEqual(plan.compileItems, expectedCompileItems)

  // Unreferenced files must NOT be in compileItems
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'Provider.fs')))
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'GreenConsumer.fs')))
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'RedConsumer.fs')))

  // Signature order verification: .fsi must precede .fs
  const signedPlan = planOwnerCompile({
    projectPath: join(FIXTURE, 'SignedProvider.fsproj'),
    aggregatePath,
  })
  assert.deepEqual(signedPlan.compileItems, [
    join(FIXTURE, 'SignedProvider.fsi'),
    join(FIXTURE, 'SignedProvider.fs'),
  ])
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materializes zero ProjectReference and isolated scratch props', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-materialize-test-'))
  const rootPropsPath = join(ROOT, 'Directory.Build.props')
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
    const plan = planOwnerCompile({
      projectPath: join(FIXTURE, 'LeakyConsumer.fsproj'),
      aggregatePath,
    })

    const materialized = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })

    // Generated project file must contain zero ProjectReference and strip WanxiangshuEmitProject identity
    const generatedXml = readFileSync(materialized.projectPath, 'utf8')
    assert.doesNotMatch(generatedXml, /<ProjectReference\b/i, 'generated flat project must contain zero ProjectReference')
    assert.match(readFileSync(aggregatePath, 'utf8'), /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
    assert.doesNotMatch(
      generatedXml,
      /<WanxiangshuEmitProject\b/i,
      'generated flat project must strip WanxiangshuEmitProject emitter identity',
    )

    // Generated project must preserve aggregate shell and contain absolute Compile entries in order
    assert.match(generatedXml, /<TargetFramework>net10\.0<\/TargetFramework>/)
    assert.match(generatedXml, /<PackageReference Include="Fable\.Core"/)
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'Runtime.fs')}"\\s*/>`))
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'LeakyContract.fs')}"\\s*/>`))
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'LeakyConsumer.fs')}"\\s*/>`))

    // Scratch Directory.Build.props must define isolated ArtifactsDir and import root props
    const scratchProps = readFileSync(join(materialized.scratchDir, 'Directory.Build.props'), 'utf8')
    assert.match(scratchProps, /<ArtifactsDir>\$\(MSBuildThisFileDirectory\)artifacts\/<\/ArtifactsDir>/)
    assert.match(scratchProps, /<NuGetAudit>false<\/NuGetAudit>/)
    assert.match(scratchProps, new RegExp(`<Import Project="${rootPropsPath}"\\s*/>`))

    // Deterministic fingerprint
    const materializedAgain = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.equal(materializedAgain.fingerprint, materialized.fingerprint)
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat projection rejects missing or stale ProjectReference before compiler invocation', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-reject-test-'))
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')

    // Stale/missing ProjectReference
    const missingRefProject = join(scratchRoot, 'MissingRef.fsproj')
    writeFileSync(
      missingRefProject,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="NonExistentTarget.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Provider.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: missingRefProject, aggregatePath }),
      /Missing ProjectReference.*NonExistentTarget\.fsproj/i,
      'must reject non-existent ProjectReference target before compiler invocation',
    )

    // ProjectReference cycle
    const cycleA = join(scratchRoot, 'CycleA.fsproj')
    const cycleB = join(scratchRoot, 'CycleB.fsproj')
    writeFileSync(
      cycleA,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="CycleB.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Provider.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )
    writeFileSync(
      cycleB,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="CycleA.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Runtime.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: cycleA, aggregatePath }),
      /ProjectReference cycle detected/i,
      'must reject ProjectReference cycle before compiler invocation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materialization escapes XML metacharacters, strips emitter identity, and binds source bytes into isolated fingerprints', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-xml-metachar-proof-'))
  try {
    const signatureFile = join(scratchRoot, 'Special&Signature.fsi')
    writeFileSync(signatureFile, 'namespace Special\nmodule SpecialSource\nval x : int\n', 'utf8')

    const compileFile = join(scratchRoot, 'Special&Source.fs')
    writeFileSync(compileFile, 'namespace Special\nmodule SpecialSource\nlet x = 1\n', 'utf8')

    const rootPropsPath = join(scratchRoot, 'Root&<Props>\'Test".props')
    writeFileSync(rootPropsPath, '<Project />', 'utf8')

    const aggregatePath = join(scratchRoot, 'Emitter.fsproj')
    writeFileSync(
      aggregatePath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuEmitProject>true</WanxiangshuEmitProject>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Special&amp;Signature.fsi" />
    <Compile Include="Special&amp;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    const ownerPath = join(scratchRoot, 'Owner.fsproj')
    writeFileSync(
      ownerPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Special&amp;Signature.fsi" />
    <Compile Include="Special&amp;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    const plan = planOwnerCompile({
      projectPath: ownerPath,
      aggregatePath,
    })

    // Planner resolves the real decoded ampersand filenames in canonical order
    const normalizedSignatureFile = resolve(signatureFile).replace(/\\/g, '/')
    const normalizedCompileFile = resolve(compileFile).replace(/\\/g, '/')
    assert.deepEqual(plan.compileItems, [normalizedSignatureFile, normalizedCompileFile])
    assert.ok(
      plan.compileItems[0].endsWith('/Special&Signature.fsi'),
      'planner must resolve real decoded signature filename',
    )
    assert.ok(
      plan.compileItems[1].endsWith('/Special&Source.fs'),
      'planner must resolve real decoded source filename',
    )

    const initialMaterialized = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })

    const generatedXml = readFileSync(initialMaterialized.projectPath, 'utf8')
    const scratchProps = readFileSync(join(initialMaterialized.scratchDir, 'Directory.Build.props'), 'utf8')

    // Materialized project strips WanxiangshuEmitProject identity
    assert.match(readFileSync(aggregatePath, 'utf8'), /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
    assert.doesNotMatch(
      generatedXml,
      /<WanxiangshuEmitProject\b/i,
      'materialized project must strip WanxiangshuEmitProject emitter identity',
    )

    // Compile Include attribute escaping proof for signature and implementation
    const expectedSigAttr = normalizedSignatureFile
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
    const expectedCompileAttr = normalizedCompileFile
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')

    assert.ok(expectedSigAttr.includes('&amp;'))
    assert.ok(expectedCompileAttr.includes('&amp;'))
    assert.match(
      generatedXml,
      new RegExp(`<Compile Include="${expectedSigAttr.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}"\\s*/>`),
    )
    assert.match(
      generatedXml,
      new RegExp(`<Compile Include="${expectedCompileAttr.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}"\\s*/>`),
    )
    assert.ok(
      !generatedXml.includes(`Include="${normalizedSignatureFile}"`),
      'generated XML must not contain raw unescaped signature Include attribute',
    )
    assert.ok(
      !generatedXml.includes(`Include="${normalizedCompileFile}"`),
      'generated XML must not contain raw unescaped Compile Include attribute',
    )

    // Root props Import attribute escaping proof
    const normalizedRootPropsPath = resolve(rootPropsPath).replace(/\\/g, '/')
    const expectedPropsAttr = normalizedRootPropsPath
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
    assert.ok(expectedPropsAttr.includes('&amp;&lt;Props&gt;&apos;Test&quot;.props'))
    const propsMatch = scratchProps.match(/<Import Project="([^"]*)"\s*\/>/)
    assert.ok(propsMatch, 'Directory.Build.props must contain Import element with Project attribute')
    assert.equal(propsMatch[1], expectedPropsAttr)
    assert.doesNotMatch(propsMatch[1], /[<>'"]/)
    assert.doesNotMatch(propsMatch[1], /&(?!(amp|lt|gt|quot|apos);)/)
    assert.ok(!scratchProps.includes(`Project="${normalizedRootPropsPath}"`), 'Directory.Build.props must not contain raw unescaped Import Project attribute')

    // 1. Invalidation proof: mutating signature file bytes invalidates fingerprint and isolates scratch/output
    writeFileSync(signatureFile, 'namespace Special\nmodule SpecialSource\nval x : int\nval y : string\n', 'utf8')
    const materializedAfterSigChange = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.notEqual(
      materializedAfterSigChange.fingerprint,
      initialMaterialized.fingerprint,
      'signature source byte change must invalidate fingerprint',
    )
    assert.notEqual(
      materializedAfterSigChange.scratchDir,
      initialMaterialized.scratchDir,
      'signature source byte change must isolate scratch directory',
    )
    assert.notEqual(
      materializedAfterSigChange.projectPath,
      initialMaterialized.projectPath,
      'signature source byte change must isolate project path',
    )
    assert.notEqual(
      materializedAfterSigChange.outputPath,
      initialMaterialized.outputPath,
      'signature source byte change must isolate output path',
    )
    assert.notEqual(
      materializedAfterSigChange.assetsPath,
      initialMaterialized.assetsPath,
      'signature source byte change must isolate assets path',
    )

    // 2. Invalidation proof: mutating implementation source file bytes invalidates fingerprint and isolates scratch/output
    writeFileSync(compileFile, 'namespace Special\nmodule SpecialSource\nlet x = 2\nlet y = "hello"\n', 'utf8')
    const materializedAfterSrcChange = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.notEqual(
      materializedAfterSrcChange.fingerprint,
      materializedAfterSigChange.fingerprint,
      'implementation source byte change must invalidate fingerprint',
    )
    assert.notEqual(
      materializedAfterSrcChange.fingerprint,
      initialMaterialized.fingerprint,
      'implementation source byte change must invalidate initial fingerprint',
    )
    assert.notEqual(
      materializedAfterSrcChange.scratchDir,
      materializedAfterSigChange.scratchDir,
      'implementation source byte change must isolate scratch directory',
    )
    assert.notEqual(
      materializedAfterSrcChange.projectPath,
      materializedAfterSigChange.projectPath,
      'implementation source byte change must isolate project path',
    )
    assert.notEqual(
      materializedAfterSrcChange.outputPath,
      materializedAfterSigChange.outputPath,
      'implementation source byte change must isolate output path',
    )
    assert.notEqual(
      materializedAfterSrcChange.assetsPath,
      materializedAfterSigChange.assetsPath,
      'implementation source byte change must isolate assets path',
    )

    // 3. Cache stability proof: unchanged inputs with same plan must produce identical fingerprint and reuse scratch/output
    const materializedStable = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.equal(
      materializedStable.fingerprint,
      materializedAfterSrcChange.fingerprint,
      'unchanged inputs must preserve deterministic fingerprint',
    )
    assert.equal(
      materializedStable.scratchDir,
      materializedAfterSrcChange.scratchDir,
      'unchanged inputs must reuse scratch directory',
    )
    assert.equal(
      materializedStable.projectPath,
      materializedAfterSrcChange.projectPath,
      'unchanged inputs must reuse project path',
    )
    assert.equal(
      materializedStable.outputPath,
      materializedAfterSrcChange.outputPath,
      'unchanged inputs must reuse output path',
    )
    assert.equal(
      materializedStable.assetsPath,
      materializedAfterSrcChange.assetsPath,
      'unchanged inputs must reuse assets path',
    )

    // Fail-closed unknown-entity assertion
    const unknownEntityPath = join(scratchRoot, 'UnknownEntity.fsproj')
    writeFileSync(
      unknownEntityPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Unknown&unknown;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: unknownEntityPath, aggregatePath }),
      /(?:Unknown|Malformed|Invalid) XML (?:entity|character) reference/i,
      'must reject unknown XML entity reference fail-closed before compiler invocation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] failure lifecycle prevents false-green warm cache and enforces success marker contract', async () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-sw011-failure-proof-'))
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
    const projectPath = join(FIXTURE, 'LeakyConsumer.fsproj')
    const rootPropsPath = join(ROOT, 'Directory.Build.props')

    let spawnInvocations = 0
    let run4SawPreservedOutput = false
    let run5SawPreservedOutput = false

    const fakeSpawn = (command, args, options) => {
      spawnInvocations++
      const child = new EventEmitter()
      child.stdout = new EventEmitter()
      child.stderr = new EventEmitter()

      const outIndex = args.indexOf('-o')
      const targetOutputDir = outIndex !== -1 ? args[outIndex + 1] : null

      setImmediate(() => {
        if (spawnInvocations === 1) {
          // First run: writes partial JS output then exits nonzero
          if (targetOutputDir) {
            writeFileSync(join(targetOutputDir, 'Partial.fs.js'), '// partial output from crashed run 1\n', 'utf8')
          }
          child.emit('close', 1, null)
        } else if (spawnInvocations === 2) {
          // Second run: would falsely return zero ONLY if partial output from run 1 survived
          if (targetOutputDir && existsSync(join(targetOutputDir, 'Partial.fs.js'))) {
            child.emit('close', 0, null)
          } else {
            // Correct behavior: partial output from run 1 was cleaned up; this run also writes partial output and fails
            if (targetOutputDir) {
              writeFileSync(join(targetOutputDir, 'Partial2.fs.js'), '// partial output from crashed run 2\n', 'utf8')
            }
            child.emit('close', 2, null)
          }
        } else if (spawnInvocations === 3) {
          // Third run: simulated successful emit writing verified JS files and exiting 0
          if (targetOutputDir) {
            writeFileSync(join(targetOutputDir, 'Runtime.fs.js'), 'export const runtime = true;\n', 'utf8')
            writeFileSync(join(targetOutputDir, 'LeakyConsumer.fs.js'), 'export const consumer = true;\n', 'utf8')
          }
          child.emit('close', 0, null)
        } else if (spawnInvocations === 4) {
          // Fourth run (warm compile): must invoke injected spawn again, preserve output before invocation
          if (
            targetOutputDir &&
            existsSync(join(targetOutputDir, 'Runtime.fs.js')) &&
            existsSync(join(targetOutputDir, 'LeakyConsumer.fs.js'))
          ) {
            run4SawPreservedOutput = true
            writeFileSync(join(targetOutputDir, 'Runtime.fs.js'), 'export const runtime = true; // refreshed\n', 'utf8')
            writeFileSync(join(targetOutputDir, 'LeakyConsumer.fs.js'), 'export const consumer = true; // refreshed\n', 'utf8')
            child.emit('close', 0, null)
          } else {
            child.emit('close', 40, null)
          }
        } else if (spawnInvocations === 5) {
          // Fifth run (warm compile with failure): output preserved before spawn, but compiler fails
          if (
            targetOutputDir &&
            existsSync(join(targetOutputDir, 'Runtime.fs.js')) &&
            existsSync(join(targetOutputDir, 'LeakyConsumer.fs.js'))
          ) {
            run5SawPreservedOutput = true
            child.emit('close', 5, null)
          } else {
            child.emit('close', 50, null)
          }
        } else {
          // Unexpected spawn call
          child.emit('close', 99, null)
        }
      })

      return child
    }

    // Call 1: First compilation fails after partial emit
    const result1 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result1.ok, false, 'first compilation must fail on nonzero child exit')
    assert.equal(result1.code, 1, 'first compilation must propagate exit code 1')
    assert.equal(spawnInvocations, 1, 'first compilation must invoke spawn exactly once')
    assert.equal(
      existsSync(join(result1.outputPath, 'Partial.fs.js')),
      false,
      'partial JS output must be cleaned up after first compilation failure',
    )
    assert.equal(
      existsSync(join(result1.scratchDir, '.success')),
      false,
      'success marker must not exist after first compilation failure',
    )

    // Call 2: Second compilation with same inputs must fail (not falsely return zero due to stale partial output)
    const result2 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result2.ok, false, 'second compilation must fail without valid success marker')
    assert.equal(result2.code, 2, 'second compilation must fail with clean-state failure code')
    assert.equal(spawnInvocations, 2, 'second compilation must invoke spawn because cache is unvalidated')
    assert.equal(
      existsSync(join(result2.outputPath, 'Partial.fs.js')),
      false,
      'stale partial output from run 1 must not exist after second compilation',
    )
    assert.equal(
      existsSync(join(result2.outputPath, 'Partial2.fs.js')),
      false,
      'partial output from run 2 must be cleaned up after failure',
    )
    assert.equal(
      existsSync(join(result2.scratchDir, '.success')),
      false,
      'success marker must not exist after second compilation failure',
    )

    // Call 3: Simulated successful compilation emit creates success marker and preserves JS outputs
    const result3 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result3.ok, true, 'third compilation with valid emit must succeed')
    assert.equal(result3.code, 0, 'third compilation must return exit code 0')
    assert.equal(spawnInvocations, 3, 'third compilation must invoke spawn')
    assert.equal(
      existsSync(join(result3.outputPath, 'Runtime.fs.js')),
      true,
      'emitted Runtime.fs.js must exist on successful compilation',
    )
    assert.equal(
      existsSync(join(result3.outputPath, 'LeakyConsumer.fs.js')),
      true,
      'emitted LeakyConsumer.fs.js must exist on successful compilation',
    )
    const markerFile = join(result3.scratchDir, '.success')
    assert.equal(existsSync(markerFile), true, 'success marker must be created on successful zero-exit compilation')

    // Call 4: Next warm call must invoke injected spawn again, preserve output before invocation, and require a new successful compiler result
    const result4 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result4.ok, true, 'fourth compilation must succeed with new compiler result')
    assert.equal(result4.code, 0, 'fourth compilation must return exit code 0')
    assert.equal(spawnInvocations, 4, 'fourth compilation must invoke injected spawn again (no marker-only cache bypass)')
    assert.equal(run4SawPreservedOutput, true, 'fourth compilation must preserve existing output before spawn invocation')
    assert.equal(
      existsSync(join(result4.outputPath, 'Runtime.fs.js')),
      true,
      'emitted Runtime.fs.js must exist on successful warm compilation',
    )
    assert.equal(
      existsSync(join(result4.outputPath, 'LeakyConsumer.fs.js')),
      true,
      'emitted LeakyConsumer.fs.js must exist on successful warm compilation',
    )
    assert.equal(existsSync(markerFile), true, 'success marker must remain intact after successful warm compilation')

    // Call 5: Warm compilation failure must remove marker and output
    const result5 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result5.ok, false, 'fifth compilation must fail when injected spawn returns nonzero')
    assert.equal(result5.code, 5, 'fifth compilation must propagate exit code 5')
    assert.equal(spawnInvocations, 5, 'fifth compilation must invoke injected spawn')
    assert.equal(run5SawPreservedOutput, true, 'fifth compilation must preserve existing output before spawn invocation')
    assert.equal(
      existsSync(join(result5.outputPath, 'Runtime.fs.js')),
      false,
      'injected warm failure must remove output directory / JS outputs',
    )
    assert.equal(
      existsSync(join(result5.outputPath, 'LeakyConsumer.fs.js')),
      false,
      'injected warm failure must remove output directory / JS outputs',
    )
    assert.equal(
      existsSync(markerFile),
      false,
      'injected warm failure must remove success marker',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
