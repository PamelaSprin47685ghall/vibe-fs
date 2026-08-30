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
    ['Admission', 'witness-stale-result-map.fs', 'Foreign.verify', 'Result'],
    ['Admission', 'witness-effect-in-error-arm.fs', 'Foreign.verify', 'Result'],
    ['Admission', 'witness-effect-in-ok-arm.fs', 'Foreign.verify', 'Result'],
    ['Admission', 'witness-task-result-bind.fs', 'Foreign.verifyAsync', 'Result'],
    ['Effect', 'witness-effect-in-error-arm.fs', 'Foreign.RegisteredEffect.send'],
    ['Effect', 'witness-effect-in-ok-arm.fs', 'Foreign.RegisteredEffect.send'],
    ['Effect', 'witness-task-result-bind.fs', 'Foreign.RegisteredEffect.send'],
    ['DurableSink', 'journal.fs', 'Fixture.GenericJournal.Append'],
    ['DurableSink', 'inferred-capability-durable-sink.fs', 'Fixture.DurableSink.Commit'],
  ].map(([classification, file, symbol, result]) => ({
    classification,
    file,
    symbol,
    ...(result ? { result, resultSymbol: symbol === 'Foreign.verify' ? 'Fixture.CurrentWitness' : 'Fixture.AdmissionResult' } : {}),
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
const application = (file, line, resolvedTarget, argumentIdentifiers, argumentTypes = {}, declarationPaths = []) => ({
  consumerPath: file,
  sourceAnchor: resolvedTarget.split('.').slice(-2).join('.'),
  resolvedTarget,
  declarationPaths,
  providerPaths: declarationPaths,
  startLine: line,
  startColumn: 0,
  endLine: line,
  endColumn: 1,
  isApplication: true,
  argumentIdentifiers,
  argumentTypes,
})
const rangedApplication = (file, line, startColumn, endColumn, resolvedTarget, argumentIdentifiers, argumentTypes, declarationPaths) => ({
  ...application(file, line, resolvedTarget, argumentIdentifiers, argumentTypes, declarationPaths),
  startColumn,
  endColumn,
})
const matchExpression = (file, successLine, failureLine) => ({
  consumerPath: file,
  startLine: 4,
  startColumn: 4,
  endLine: 6,
  endColumn: 52,
  scrutinee: { startLine: 4, startColumn: 10, endLine: 4, endColumn: 30 },
  clauses: [
    { patternKind: 'Ok', startLine: successLine, startColumn: 19, endLine: successLine, endColumn: 52 },
    { patternKind: 'Error', startLine: failureLine, startColumn: 17, endLine: failureLine, endColumn: 52 },
  ],
})
const typedAdmissionUses = (file, line) => [{
  consumerPath: file,
  providerPaths: ['positive-six-classes.fs'],
  symbol: 'Fixture.CurrentWitness',
  symbolKind: 'FSharpEntity',
  line: 3,
  isFromPattern: false,
  isFromType: true,
}, {
  consumerPath: file,
  providerPaths: ['positive-six-classes.fs'],
  symbol: 'Fixture.CurrentAdmission.admit',
  symbolKind: 'FSharpMemberOrFunctionOrValue',
  line,
  isFromPattern: false,
  isFromType: false,
}]
const taskSend = (file, line, argument) => application(
  file,
  line,
  'Fixture.Task.send',
  [argument],
  { [argument]: argument === 'admitted' ? 'Fixture.AdmissionResult' : 'Fixture.CurrentWitness' },
  ['positive-six-classes.fs'],
)
const typedAdmissionApplications = (file, admissionLine, effectLine, witness = 'witness', effectArgument = 'admitted') => [
  application(file, admissionLine, 'Fixture.CurrentAdmission.admit', [witness], { [witness]: 'Fixture.CurrentWitness' }, ['positive-six-classes.fs']),
  taskSend(file, effectLine, effectArgument),
  {
    ...application(file, admissionLine, 'Microsoft.FSharp.Core.ResultModule.Map', [], {}, []),
    startLine: admissionLine,
    endLine: effectLine,
    endColumn: 100,
  },
]
const typedAdmissionControlFlow = (file, effectLine) => ({
  lambdaExpressions: [{
    consumerPath: file,
    body: { startLine: effectLine, startColumn: 0, endLine: effectLine, endColumn: 100 },
  }],
})

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

test('WHAT[ENF-016] stale witness needs a fresh current admission before an effect', () => {
  const stale = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-direct-effect.fs')],
    positiveManifest,
    { applicationUses: [taskSend('witness-direct-effect.fs', 5, 'current'), taskSend('witness-direct-effect.fs', 6, 'witness')] },
  )
  const freshFile = 'witness-typed-admitted-effect.fs'
  const fresh = scanEntries(
    [entry('positive-six-classes.fs'), entry(freshFile)],
    positiveManifest,
    {
      symbolUses: typedAdmissionUses(freshFile, 4),
      applicationUses: typedAdmissionApplications(freshFile, 4, 5),
      ...typedAdmissionControlFlow(freshFile, 5),
    },
  )
  assert.ok(ids(stale).includes('witness-direct-effect-without-admission'))
  assert.ok(!ids(fresh).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-015] a Result match failure arm cannot inherit admission from the preceding Ok arm', () => {
  const file = 'witness-effect-in-error-arm.fs'
  const manifest = structuredClone(positiveManifest)
  manifest.contracts.find((contract) => contract.symbol === 'CurrentWitness').admissions.push({ file, symbol: 'Foreign.verify' })
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], manifest, {
    applicationUses: [
      rangedApplication(file, 4, 10, 30, 'Foreign.verify', ['current', 'stale'], { current: 'Fixture.CurrentWitness', stale: 'Fixture.CurrentWitness' }, [file]),
      rangedApplication(file, 6, 17, 52, 'Foreign.RegisteredEffect.send', ['stale'], { stale: 'Fixture.CurrentWitness' }, [file]),
    ],
    matchExpressions: [matchExpression(file, 5, 6)],
  })
  assert.deepEqual(problems.filter((hit) => hit.file === file).map((hit) => hit.id), ['witness-direct-effect-without-admission'])
})

test('WHAT[ENF-015] an effect in the exact Result match success arm is admitted', () => {
  const file = 'witness-effect-in-ok-arm.fs'
  const manifest = structuredClone(positiveManifest)
  manifest.contracts.find((contract) => contract.symbol === 'CurrentWitness').admissions.push({ file, symbol: 'Foreign.verify' })
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], manifest, {
    applicationUses: [
      rangedApplication(file, 4, 10, 30, 'Foreign.verify', ['current', 'stale'], { current: 'Fixture.CurrentWitness', stale: 'Fixture.CurrentWitness' }, [file]),
      rangedApplication(file, 5, 19, 52, 'Foreign.RegisteredEffect.send', ['stale'], { stale: 'Fixture.CurrentWitness' }, [file]),
    ],
    matchExpressions: [matchExpression(file, 5, 6)],
  })
  assert.ok(!ids(problems).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-015] a successful taskResult bind admits only its downstream body', () => {
  const file = 'witness-task-result-bind.fs'
  const manifest = structuredClone(positiveManifest)
  manifest.contracts.find((contract) => contract.symbol === 'CurrentWitness').admissions.push({ file, symbol: 'Foreign.verifyAsync' })
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], manifest, {
    applicationUses: [
      rangedApplication(file, 5, 22, 47, 'Foreign.verifyAsync', ['current', 'stale'], { current: 'Fixture.CurrentWitness', stale: 'Fixture.CurrentWitness' }, [file]),
      rangedApplication(file, 6, 8, 43, 'Foreign.RegisteredEffect.send', ['stale'], { stale: 'Fixture.CurrentWitness' }, [file]),
    ],
    bindExpressions: [{
      consumerPath: file,
      builderKind: 'TaskResult',
      binding: { startLine: 5, startColumn: 22, endLine: 5, endColumn: 47 },
      body: { startLine: 6, startColumn: 8, endLine: 6, endColumn: 43 },
    }],
  })
  assert.ok(!ids(problems).includes('witness-direct-effect-without-admission'))
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

test('WHAT[ENF-015] comment and diagnostic-string mentions are not witness effects', () => {
  const commentsOnly = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-comment-string.fs')],
    positiveManifest,
  )
  assert.ok(!ids(commentsOnly).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-015] pure witness match and fold consumers do not imply an effect', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-pure-consumer.fs')],
    positiveManifest,
  )
  assert.ok(!ids(problems).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-015] an effect in another declaration does not correlate with a witness consumer', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-unrelated-effect.fs')],
    positiveManifest,
  )
  assert.ok(!ids(problems).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-015] identity-shaped arguments do not substitute for a registered typed admission seam', () => {
  const problems = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-ignored-identity-args.fs')],
    positiveManifest,
    { symbolUses: [], applicationUses: [taskSend('witness-ignored-identity-args.fs', 4, 'witness')] },
  )
  assert.deepEqual(
    problems.filter((hit) => hit.file === 'witness-ignored-identity-args.fs').map((hit) => hit.id),
    ['witness-direct-effect-without-admission'],
  )
})

test('WHAT[ENF-015] witness cannot drive an effect without current subject/version/digest admission', () => {
  const rejected = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-direct-effect.fs')],
    positiveManifest,
    { applicationUses: [taskSend('witness-direct-effect.fs', 5, 'current'), taskSend('witness-direct-effect.fs', 6, 'witness')] },
  )
  assert.ok(ids(rejected).includes('witness-direct-effect-without-admission'))

  const admitted = scanEntries(
    [entry('positive-six-classes.fs'), entry('witness-typed-admitted-effect.fs')],
    positiveManifest,
    {
      symbolUses: typedAdmissionUses('witness-typed-admitted-effect.fs', 4),
      applicationUses: typedAdmissionApplications('witness-typed-admitted-effect.fs', 4, 5),
      ...typedAdmissionControlFlow('witness-typed-admitted-effect.fs', 5),
    },
  )
  assert.ok(!ids(admitted).includes('witness-direct-effect-without-admission'))
})

test('WHAT[ENF-014] compiler constructor evidence catches space-applied private DU construction outside the exact issuer span', () => {
  const manifest = {
    version: 1,
    methods: positiveManifest.methods,
    contracts: [{
      file: 'blessing-authority.fs',
      symbol: 'BlessingPermit',
      anchor: 'type BlessingPermit =',
      classification: 'Authority',
      class: 'Capability',
      ...semantics,
      issuers: [{ file: 'blessing-authority.fs', symbol: 'grant', anchor: 'let grant payload =' }],
    }],
  }
  const problems = scanEntries([entry('blessing-authority.fs'), entry('foreign-space-constructor.fs')], manifest, {
    symbolUses: [{
      consumerPath: 'foreign-space-constructor.fs',
      providerPaths: ['blessing-authority.fs'],
      symbol: 'Fixture.BlessingPermit',
      symbolKind: 'FSharpUnionCase',
      line: 5,
      isFromPattern: false,
      isFromType: false,
    }],
  })
  assert.deepEqual(problems.filter((hit) => hit.line === 5).map((hit) => hit.id), ['foreign-issuance'])
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

test('WHAT[ENF-014] private record construction outside the exact issuer span is foreign issuance', () => {
  const file = 'foreign-record-construction.fs'
  const manifest = {
    version: 1,
    methods: positiveManifest.methods,
    contracts: [{
      file,
      symbol: 'OneShotRecordCapability',
      anchor: 'type OneShotRecordCapability =',
      classification: 'Authority',
      class: 'Capability',
      ...semantics,
      issuers: [{ file, symbol: 'issueRecord', anchor: 'let issueRecord owner subject version =' }],
    }],
  }
  const recordFields = ['Owner', 'Subject', 'Version'].flatMap((field) => [11, 14].map((line) => ({
    consumerPath: file,
    providerPaths: [file],
    symbol: `Fixture.OneShotRecordCapability.${field}`,
    symbolKind: 'FSharpField',
    line,
    column: 6,
    isFromPattern: false,
    isFromType: false,
  })))
  const problems = scanEntries([entry(file)], manifest, { symbolUses: recordFields })
  assert.deepEqual([...new Set(problems.filter((hit) => hit.line === 14).map((hit) => hit.id))], ['foreign-issuance'])
})

test('WHAT[ENF-015] admission must consume the same witness and dominate the effect success path', () => {
  const file = 'witness-wrong-and-ignored-admission.fs'
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], positiveManifest, {
    symbolUses: [
      ...[4, 8].map((line) => ({
        consumerPath: file,
        providerPaths: ['positive-six-classes.fs'],
        symbol: 'Fixture.CurrentWitness',
        symbolKind: 'FSharpEntity',
        line,
        isFromPattern: false,
        isFromType: true,
      })),
      ...[5, 9].map((line) => ({
        consumerPath: file,
        providerPaths: ['positive-six-classes.fs'],
        symbol: 'Fixture.CurrentAdmission.admit',
        symbolKind: 'FSharpMemberOrFunctionOrValue',
        line,
        isFromPattern: false,
        isFromType: false,
      })),
    ],
    applicationUses: [
      application(file, 5, 'Fixture.CurrentAdmission.admit', ['other'], { other: 'Fixture.CurrentWitness' }, ['positive-six-classes.fs']),
      taskSend(file, 6, 'stale'),
      application(file, 9, 'Fixture.CurrentAdmission.admit', ['witness'], { witness: 'Fixture.CurrentWitness' }, ['positive-six-classes.fs']),
      taskSend(file, 10, 'witness'),
    ],
  })
  assert.deepEqual(
    problems.filter((hit) => hit.file === file).map((hit) => hit.id),
    ['witness-direct-effect-without-admission', 'witness-direct-effect-without-admission'],
  )
})

test('WHAT[ENF-019] arbitrary payload names cannot hide capabilities passed to a durable journal edge', () => {
  const file = 'arbitrary-stored-state.fs'
  const manifest = {
    version: 1,
    methods: positiveManifest.methods,
    contracts: [{
      file,
      symbol: 'StoredPermit',
      anchor: 'type StoredPermit =',
      classification: 'Authority',
      class: 'Capability',
      ...semantics,
      issuers: [{ file, symbol: 'issuePermit', anchor: 'let issuePermit value =' }],
    }],
  }
  const problems = scanEntries([entry(file)], manifest, { symbolUses: [{
    consumerPath: file,
    providerPaths: [file],
    symbol: 'Fixture.StoredPermit',
    symbolKind: 'FSharpEntity',
    line: 7,
    column: 12,
    isFromPattern: false,
    isFromType: true,
  }, {
    consumerPath: file,
    providerPaths: [file],
    symbol: 'Fixture.StoredState',
    symbolKind: 'FSharpEntity',
    line: 12,
    column: 45,
    isFromPattern: false,
    isFromType: true,
  }, {
    consumerPath: file,
    providerPaths: ['journal.fs'],
    symbol: 'Fixture.GenericJournal.Append',
    symbolKind: 'FSharpMemberOrFunctionOrValue',
    line: 13,
    column: 12,
    isFromPattern: false,
    isFromType: false,
  }], applicationUses: [application(
    file,
    13,
    'Fixture.GenericJournal.Append',
    ['state'],
    { state: 'Fixture.StoredState' },
    ['journal.fs'],
  )] })
  assert.ok(ids(problems).includes('capability-persistence'))
})

test('WHAT[ENF-015] only a registered resolved effect symbol triggers witness authority enforcement', () => {
  const file = 'witness-registered-effect.fs'
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], positiveManifest, {
    applicationUses: [application(
      file,
      4,
      'Fixture.EffectPort.Commit',
      ['witness'],
      { witness: 'Fixture.CurrentWitness' },
      ['effect-port.fs'],
    )],
  })
  assert.deepEqual(problems.filter((hit) => hit.file === file).map((hit) => hit.id), ['witness-direct-effect-without-admission'])
})

test('WHAT[ENF-015] admission success cannot authorize an effect over a different stale witness value', () => {
  const file = 'witness-stale-result-map.fs'
  const manifest = structuredClone(positiveManifest)
  manifest.contracts.find((contract) => contract.symbol === 'CurrentWitness').admissions.push({ file, symbol: 'Foreign.verify' })
  const problems = scanEntries([entry('positive-six-classes.fs'), entry(file)], manifest, {
    applicationUses: [
      application(file, 9, 'Foreign.verify', ['current'], { current: 'Fixture.CurrentWitness' }, [file]),
      application(file, 10, 'Fixture.EffectPort.SendMessage', ['stale'], { stale: 'Fixture.CurrentWitness' }, ['effect-port.fs']),
    ],
  })
  assert.deepEqual(problems.filter((hit) => hit.file === file).map((hit) => hit.id), ['witness-direct-effect-without-admission'])
})

test('WHAT[ENF-019] inferred capability-containing arguments cannot cross a registered durable sink', () => {
  const file = 'inferred-capability-durable-sink.fs'
  const manifest = {
    version: 1,
    methods: positiveManifest.methods,
    contracts: [{
      file,
      symbol: 'StoredPermit',
      anchor: 'type StoredPermit =',
      classification: 'Authority',
      class: 'Capability',
      ...semantics,
      issuers: [{ file, symbol: 'issuePermit', anchor: 'let issuePermit value =' }],
    }],
  }
  const problems = scanEntries([entry(file)], manifest, {
    symbolUses: [{
      consumerPath: file,
      providerPaths: [file],
      declarationPaths: [file],
      symbol: 'Fixture.StoredPermit',
      symbolKind: 'FSharpEntity',
      line: 6,
      column: 29,
      isFromPattern: false,
      isFromType: true,
    }],
    applicationUses: [application(file, 15, 'Fixture.DurableSink.Commit', ['state'], { state: 'Fixture.HiddenState' }, [file])],
  })
  assert.ok(ids(problems).includes('capability-persistence'))
})

test('WHAT[ENF-013] manifest ownership is exact for declarations issuers and unique WHAT definitions', () => {
  const mismatch = JSON.parse(readFileSync(join(fixtureRoot, 'owner-mismatch-authority-contracts.json'), 'utf8'))
  const registry = {
    owners: new Set(['capability-enforcement', 'finality']),
    ownership: new Map([['positive-six-classes.fs', 'capability-enforcement']]),
    whats: new Map([['ENF-013', { package: 'capability-enforcement' }]]),
  }
  const problems = scanEntries([entry('positive-six-classes.fs')], mismatch, { registry })
  assert.ok(ids(problems).includes('authority-owner-mismatch'))

  const issuerMismatch = structuredClone(mismatch)
  issuerMismatch.contracts[0].owner = 'capability-enforcement'
  issuerMismatch.contracts[0].issuers[0].owner = 'finality'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], issuerMismatch, { registry })).includes('authority-issuer-owner-mismatch'))

  const whatMismatch = structuredClone(mismatch)
  whatMismatch.contracts[0].owner = 'capability-enforcement'
  whatMismatch.contracts[0].whatOwners['ENF-013'] = 'finality'
  assert.ok(ids(scanEntries([entry('positive-six-classes.fs')], whatMismatch, { registry })).includes('authority-what-owner-mismatch'))
})
