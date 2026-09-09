import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { AUTHORITY_CLASSES, scanEntries, scanRepo } from '../../../scripts/checks/authority-boundary.mjs'

const fixtureRoot = join(dirname(fileURLToPath(import.meta.url)), 'fixtures/authority-boundary')
const entry = (name) => ({ file: name, text: readFileSync(join(fixtureRoot, name), 'utf8') })
const semantics = {
  owner: 'interaction',
  what: 'ENF-013',
  scope: 'exact subject and version',
  freshness: 'current admission only',
  multiplicity: 'declared exactly',
  consume: 'typed Result failure',
  durability: 'process-local unless this is evidence or receipt',
}

const symbols = [
  ['CurrentEvidence', 'Evidence'],
  ['AdmissionDecision', 'Decision'],
  ['CurrentWitness', 'Witness'],
  ['OneShotCapability', 'Capability'],
  ['AppliedReceipt', 'Receipt'],
  ['ProcessPhysicalHandle', 'PhysicalHandle'],
]
const positiveManifest = {
  version: 1,
  methods: [
    ['Effect', 'effect-port.fs', 'Fixture.EffectPort.Commit'],
    ['Effect', 'effect-port.fs', 'Fixture.EffectPort.SendMessage'],
    ['Effect', 'positive-six-classes.fs', 'Fixture.Task.send'],
    ['Admission', 'positive-six-classes.fs', 'Fixture.CurrentAdmission.admit', 'Result'],
    ['DurableSink', 'journal.fs', 'Fixture.GenericJournal.Append'],
  ].map(([classification, file, symbol, result]) => ({
    classification,
    file,
    symbol,
    ...(result ? { result, resultSymbol: 'Fixture.AdmissionResult' } : {}),
    owner: 'interaction',
    what: classification === 'DurableSink' ? 'ENF-019' : 'ENF-015',
    whatOwners: { [classification === 'DurableSink' ? 'ENF-019' : 'ENF-015']: 'capability-enforcement' },
  })),
  contracts: [
    ...symbols.map(([symbol, authorityClass]) => ({
      file: 'positive-six-classes.fs',
      symbol,
      anchor: `type ${symbol} =`,
      classification: 'Authority',
      class: authorityClass,
      ...semantics,
      issuers: [{ file: 'positive-six-classes.fs', symbol: `issue${symbol}`, anchor: `let issue${symbol}`, owner: 'interaction' }],
      ...(authorityClass === 'Witness' ? { admissions: [{ file: 'positive-six-classes.fs', symbol: 'CurrentAdmission.admit' }] } : {}),
    })),
    {
      file: 'positive-six-classes.fs',
      symbol: 'JsCapability',
      anchor: 'type JsCapability =',
      classification: 'Vocabulary',
      owner: 'repository-programming',
      what: 'ENF-008',
      issuers: [],
    },
  ],
}

const ids = (problems) => problems.map((hit) => hit.id)

test('WHAT[ENF-013] all six authority classes require exact positive contracts while JsCapability remains vocabulary', () => {
  assert.deepEqual(AUTHORITY_CLASSES, ['Evidence', 'Decision', 'Witness', 'Capability', 'Receipt', 'PhysicalHandle'])
  assert.deepEqual(scanEntries([entry('positive-six-classes.fs')], positiveManifest), [])
})

test('WHAT[ENF-014] stale anchors and unclassified sensitive declarations fail closed', () => {
  const stale = structuredClone(positiveManifest)
  stale.contracts[0].anchor = 'type RenamedEvidence ='
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], stale)).includes('stale-manifest-anchor'))

  assert.ok(ids(scanEntries([entry('unclassified-sensitive.fs')], { version: 1, methods: [], contracts: [] })).includes('unclassified-sensitive-declaration'))
})

test('WHAT[ENF-014] only a registered owner or issuer may mint authority', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('foreign-issuance.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('foreign-issuance'))
})

test('WHAT[ENF-014] production scan scope cannot hide an unrelated unregistered permit', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('unregistered-unrelated-permit.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('unclassified-sensitive-declaration'))
})

test('WHAT[ENF-019] persistence outside manifest-owned paths still fails closed', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('off-manifest-persistence.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('capability-persistence'))
})

test('WHAT[ENF-014] a foreign qualified issue helper is an authority mint', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('foreign-module-issue.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('foreign-issuance'))
})

test('WHAT[ENF-014] comments cannot forge an issuer declaration or anchor', () => {
  const forged = structuredClone(positiveManifest)
  forged.contracts[3].issuers = [{
    file: 'forged-issuer-anchor.fs',
    symbol: 'authorizedMint',
    anchor: 'let authorizedMint owner subject version',
  }]
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('forged-issuer-anchor.fs')],
    forged,
  )
  assert.ok(ids(problems).includes('stale-issuance-anchor'))
})

test('WHAT[ENF-017] every authority contract declares its multiplicity', () => {
  const missing = structuredClone(positiveManifest)
  missing.contracts[3].multiplicity = ''
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], missing)).includes('incomplete-contract'))
})

test('WHAT[ENF-018] one-shot consumption cannot collapse typed failure into bool', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('bool-consume.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('bool-one-shot-consume'))
})

test('WHAT[ENF-019] process capabilities cannot enter Fact/Event/codec/JSON persistence', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('capability-codec.fs')],
    positiveManifest,
  )
  assert.ok(ids(problems).includes('capability-persistence'))
})

test('WHAT[ENF-019] a capability nested in any durable payload type fails independently of path and serializer spelling', () => {
  const file = 'snapshot-quiescence-permit.fs'
  const manifest = {
    version: 1,
    methods: positiveManifest.methods,
    contracts: [{
      file,
      symbol: 'QuiescencePermit',
      anchor: 'type QuiescencePermit =',
      classification: 'Authority',
      class: 'Capability',
      ...semantics,
      issuers: [{ file, symbol: 'issue', anchor: 'let issue value =' }],
    }],
  }
  const problems = scanEntries([entry(file)], manifest)
  assert.deepEqual(
    problems.filter((hit) => hit.file === file).map((hit) => hit.id),
    ['capability-persistence'],
  )
})

test('WHAT[ENF-013] manifest owner and WHAT references must resolve through canonical authority registries', () => {
  const imaginary = JSON.parse(readFileSync(join(fixtureRoot, 'imaginary-authority-contracts.json'), 'utf8'))
  const problems = scanEntries([entry('positive-six-classes.fs')], imaginary)
  assert.deepEqual(ids(problems).filter((id) => id.startsWith('unregistered-authority-')), [
    'unregistered-authority-owner',
    'unregistered-authority-what',
  ])
})

test('WHAT[ENF-013] manifest ownership is exact for declarations issuers and unique WHAT definitions', () => {
  const mismatch = JSON.parse(readFileSync(join(fixtureRoot, 'owner-mismatch-authority-contracts.json'), 'utf8'))
  const registry = {
    subsystems: new Set(['interaction', 'persistence']),
    ownership: new Map([['positive-six-classes.fs', 'interaction']]),
    whats: new Map([['ENF-013', { package: 'capability-enforcement' }]]),
    packages: new Set(['capability-enforcement']),
  }
  const problems = scanEntries([entry('positive-six-classes.fs')], mismatch, registry)
  assert.ok(ids(problems).includes('authority-owner-mismatch'))

  const issuerMismatch = structuredClone(mismatch)
  issuerMismatch.contracts[0].owner = 'interaction'
  issuerMismatch.contracts[0].issuers[0].owner = 'persistence'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], issuerMismatch, registry)).includes('authority-issuer-owner-mismatch'))

  const whatMismatch = structuredClone(mismatch)
  whatMismatch.contracts[0].owner = 'interaction'
  whatMismatch.contracts[0].whatOwners['ENF-013'] = 'persistence'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], whatMismatch, registry)).includes('authority-what-owner-mismatch'))
})

test('WHAT[ENF-013] source identity is a single resolved subsystem without legacy aliases', () => {
  const registry = {
    subsystems: new Set(['interaction', 'persistence']),
    ownership: new Map([['positive-six-classes.fs', 'interaction']]),
    whats: new Map([['ENF-013', { package: 'capability-enforcement' }], ['ENF-008', { package: 'capability-enforcement' }]]),
    packages: new Set(['capability-enforcement']),
  }
  const legal = structuredClone(positiveManifest)
  legal.contracts = legal.contracts.filter((row) => row.classification === 'Authority')
  legal.methods = []
  assert.deepEqual(scanEntries([entry('positive-six-classes.fs')], legal, registry), [])

  const legacyAlias = structuredClone(legal)
  legacyAlias.contracts[0].owner = 'capability-enforcement'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], legacyAlias, registry)).includes('unregistered-authority-owner'))

  const wrongSubsystem = structuredClone(legal)
  wrongSubsystem.contracts[0].owner = 'persistence'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], wrongSubsystem, registry)).includes('authority-owner-mismatch'))

  const methodLegal = {
    version: 1,
    methods: [{
      file: 'positive-six-classes.fs',
      symbol: 'Fixture.CurrentAdmission.admit',
      classification: 'Admission',
      result: 'Result',
      resultSymbol: 'Fixture.AdmissionResult',
      owner: 'interaction',
      what: 'ENF-013',
      whatOwners: { 'ENF-013': 'capability-enforcement' },
    }],
    contracts: structuredClone(positiveManifest.contracts.filter((row) => row.classification === 'Authority')),
  }
  assert.deepEqual(scanEntries([entry('positive-six-classes.fs')], methodLegal, registry), [])

  const methodWrong = structuredClone(methodLegal)
  methodWrong.methods[0].owner = 'persistence'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], methodWrong, registry)).includes('authority-owner-mismatch'))
})

test('WHAT[ENF-013] WHAT package identity is separate from source subsystem', () => {
  const registry = {
    subsystems: new Set(['interaction', 'persistence']),
    ownership: new Map([['positive-six-classes.fs', 'interaction']]),
    whats: new Map([['ENF-013', { package: 'capability-enforcement' }]]),
    packages: new Set(['capability-enforcement']),
  }
  const subsystemAsPackage = structuredClone(positiveManifest)
  subsystemAsPackage.contracts = subsystemAsPackage.contracts.filter((row) => row.classification === 'Authority')
  subsystemAsPackage.methods = []
  for (const row of subsystemAsPackage.contracts) {
    row.owner = 'interaction'
    if (row.what === 'ENF-013') row.whatOwners = { 'ENF-013': 'interaction' }
  }
  subsystemAsPackage.contracts[0].what = 'ENF-013'
  subsystemAsPackage.contracts[0].whatOwners = { 'ENF-013': 'interaction' }
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], subsystemAsPackage, registry)).includes('authority-what-owner-mismatch'))

  const unknownWhat = structuredClone(subsystemAsPackage)
  unknownWhat.contracts = [structuredClone(subsystemAsPackage.contracts[0])]
  unknownWhat.contracts[0].what = 'UNKNOWN-999'
  unknownWhat.contracts[0].whatOwners = { 'UNKNOWN-999': 'capability-enforcement' }
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], unknownWhat, registry)).includes('unregistered-authority-what'))
})

test('WHAT[ENF-014] explicit and legacy-mapped subsystems resolve through the real repository path', () => {
  const root = mkdtempSync(join(tmpdir(), 'authority-subsystem-'))
  try {
    const write = (path, text) => {
      const target = join(root, path)
      mkdirSync(dirname(target), { recursive: true })
      writeFileSync(target, text)
    }
    write('scripts/checks/subsystems.json', JSON.stringify({
      schema_version: 1,
      subsystems: [
        { id: 'interaction', legacy_owners: ['capability-enforcement'] },
        { id: 'persistence', legacy_owners: ['durable-events'] },
      ],
    }))
    write('src/Wanxiangshu/Wanxiangshu.fsproj', [
      '<Project Sdk="Microsoft.NET.Sdk">',
      '  <ItemGroup>',
      '    <Compile Include="ExplicitCapability.fsi" />',
      '    <Compile Include="ExplicitCapability.fs" />',
      '    <Compile Include="LegacyReceipt.fsi" />',
      '    <Compile Include="LegacyReceipt.fs" />',
      '  </ItemGroup>',
      '</Project>',
      '',
    ].join('\n'))
    write('src/Wanxiangshu/Wanxiangshu.Shard.explicit-capability.fsproj', [
      '<Project Sdk="Microsoft.NET.Sdk">',
      '  <PropertyGroup>',
      '    <WanxiangshuSubsystem>interaction</WanxiangshuSubsystem>',
      '    <WanxiangshuCompileShard>explicit-capability</WanxiangshuCompileShard>',
      '  </PropertyGroup>',
      '  <ItemGroup>',
      '    <Compile Include="ExplicitCapability.fsi" />',
      '    <Compile Include="ExplicitCapability.fs" />',
      '  </ItemGroup>',
      '</Project>',
      '',
    ].join('\n'))
    write('src/Wanxiangshu/ExplicitCapability.fsi', 'namespace Fixture\n')
    write('src/Wanxiangshu/ExplicitCapability.fs', [
      'namespace Fixture',
      '',
      '// DSL-AUTHORITY: Capability',
      '// DSL-ISSUE: ExplicitCapability',
      'type ExplicitCapability = private ExplicitCapability of subject: string',
      '',
      'module Owner =',
      '    let issueExplicitCapability subject = ExplicitCapability subject',
      '',
    ].join('\n'))
    write('src/Wanxiangshu/Wanxiangshu.Owner.durable-events.legacy-receipt.fsproj', [
      '<Project Sdk="Microsoft.NET.Sdk">',
      '  <PropertyGroup>',
      '    <WanxiangshuSemanticOwner>durable-events</WanxiangshuSemanticOwner>',
      '    <WanxiangshuOwnerLocality>legacy-receipt</WanxiangshuOwnerLocality>',
      '    <WanxiangshuOwnerLocalityKind>runtime</WanxiangshuOwnerLocalityKind>',
      '  </PropertyGroup>',
      '  <ItemGroup>',
      '    <Compile Include="LegacyReceipt.fsi" />',
      '    <Compile Include="LegacyReceipt.fs" />',
      '  </ItemGroup>',
      '</Project>',
      '',
    ].join('\n'))
    write('src/Wanxiangshu/LegacyReceipt.fsi', 'namespace Fixture\n')
    write('src/Wanxiangshu/LegacyReceipt.fs', [
      'namespace Fixture',
      '',
      '// DSL-AUTHORITY: Receipt',
      '// DSL-ISSUE: LegacyReceipt',
      'type LegacyReceipt = { Subject: string }',
      '',
      'module Owner =',
      '    let issueLegacyReceipt subject = { Subject = subject }',
      '',
    ].join('\n'))
    write('requirements/capability-enforcement/WHAT.md', [
      '# capability-enforcement — WHAT',
      '',
      '## ENF-013: test authority scope',
      '',
    ].join('\n'))
    const manifest = {
      version: 1,
      methods: [{
        file: 'src/Wanxiangshu/ExplicitCapability.fs',
        symbol: 'Fixture.EffectPort.Commit',
        classification: 'Effect',
        owner: 'interaction',
        what: 'ENF-013',
        whatOwners: { 'ENF-013': 'capability-enforcement' },
      }],
      contracts: [
        {
          file: 'src/Wanxiangshu/ExplicitCapability.fs',
          symbol: 'ExplicitCapability',
          anchor: 'type ExplicitCapability =',
          classification: 'Authority',
          class: 'Capability',
          owner: 'interaction',
          what: 'ENF-013',
          scope: 'exact subject',
          freshness: 'current admission only',
          multiplicity: 'one-shot',
          consume: 'typed Result failure',
          durability: 'process-local',
          issuers: [{
            file: 'src/Wanxiangshu/ExplicitCapability.fs',
            symbol: 'issueExplicitCapability',
            anchor: 'let issueExplicitCapability',
            owner: 'interaction',
          }],
          whatOwners: { 'ENF-013': 'capability-enforcement' },
        },
        {
          file: 'src/Wanxiangshu/LegacyReceipt.fs',
          symbol: 'LegacyReceipt',
          anchor: 'type LegacyReceipt =',
          classification: 'Authority',
          class: 'Receipt',
          owner: 'persistence',
          what: 'ENF-013',
          scope: 'exact subject',
          freshness: 'current admission only',
          multiplicity: 'replayable evidence',
          consume: 'typed inspection only',
          durability: 'receipt records outcome',
          issuers: [{
            file: 'src/Wanxiangshu/LegacyReceipt.fs',
            symbol: 'issueLegacyReceipt',
            anchor: 'let issueLegacyReceipt',
            owner: 'persistence',
          }],
          whatOwners: { 'ENF-013': 'capability-enforcement' },
        },
      ],
    }
    assert.deepEqual(scanRepo(root, manifest), { ok: true, problems: [] })

    const legacyAlias = structuredClone(manifest)
    legacyAlias.contracts[0].owner = 'capability-enforcement'
    const legacyResult = scanRepo(root, legacyAlias)
    assert.equal(legacyResult.ok, false)
    assert.ok(legacyResult.problems.map((hit) => hit.id).includes('unregistered-authority-owner'))

    const wrongSubsystem = structuredClone(manifest)
    wrongSubsystem.contracts[1].owner = 'interaction'
    const wrongResult = scanRepo(root, wrongSubsystem)
    assert.equal(wrongResult.ok, false)
    assert.ok(wrongResult.problems.map((hit) => hit.id).includes('authority-owner-mismatch'))

    const wrongMethod = structuredClone(manifest)
    wrongMethod.methods[0].owner = 'persistence'
    const wrongMethodResult = scanRepo(root, wrongMethod)
    assert.equal(wrongMethodResult.ok, false)
    assert.ok(wrongMethodResult.problems.map((hit) => hit.id).includes('authority-owner-mismatch'))

    const wrongPackage = structuredClone(manifest)
    wrongPackage.contracts[0].whatOwners = { 'ENF-013': 'interaction' }
    const wrongPackageResult = scanRepo(root, wrongPackage)
    assert.equal(wrongPackageResult.ok, false)
    assert.ok(wrongPackageResult.problems.map((hit) => hit.id).includes('authority-what-owner-mismatch'))
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
