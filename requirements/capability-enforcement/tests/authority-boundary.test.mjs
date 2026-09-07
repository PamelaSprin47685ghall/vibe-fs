import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { AUTHORITY_CLASSES, scanEntries } from '../../../scripts/checks/authority-boundary.mjs'

const fixtureRoot = join(dirname(fileURLToPath(import.meta.url)), 'fixtures/authority-boundary')
const entry = (name) => ({ file: name, text: readFileSync(join(fixtureRoot, name), 'utf8') })
const semantics = {
  owner: 'capability-enforcement',
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
    owner: 'capability-enforcement',
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
      issuers: [{ file: 'positive-six-classes.fs', symbol: `issue${symbol}`, anchor: `let issue${symbol}` }],
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
    owners: new Set(['capability-enforcement', 'finality']),
    ownership: new Map([['positive-six-classes.fs', 'capability-enforcement']]),
    whats: new Map([['ENF-013', { package: 'capability-enforcement' }]]),
  }
  const problems = scanEntries([entry('positive-six-classes.fs')], mismatch, registry)
  assert.ok(ids(problems).includes('authority-owner-mismatch'))

  const issuerMismatch = structuredClone(mismatch)
  issuerMismatch.contracts[0].owner = 'capability-enforcement'
  issuerMismatch.contracts[0].issuers[0].owner = 'finality'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], issuerMismatch, registry)).includes('authority-issuer-owner-mismatch'))

  const whatMismatch = structuredClone(mismatch)
  whatMismatch.contracts[0].owner = 'capability-enforcement'
  whatMismatch.contracts[0].whatOwners['ENF-013'] = 'finality'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], whatMismatch, registry)).includes('authority-what-owner-mismatch'))
})
