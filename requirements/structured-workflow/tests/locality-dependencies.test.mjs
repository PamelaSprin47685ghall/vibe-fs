import assert from 'node:assert/strict'
import test from 'node:test'

import { analyzeLocalityDependencies } from '../../../scripts/lib/locality-dependencies.mjs'

const locality = (id, sources, references = [], owner = id) => ({ id, owner, sources, references })
const use = (consumerPath, providerPath, overrides = {}) => ({
  consumerPath,
  providerPaths: providerPath ? [providerPath] : [],
  symbol: 'Wanxiangshu.Provider.value',
  symbolKind: 'FSharpMemberOrFunctionOrValue',
  line: 3,
  column: 4,
  ...overrides,
})

test('WHAT[STRUCTURED-WORKFLOW-011] compiler-resolved locality edges must stay inside declared closure', () => {
  const provider = locality('provider', ['src/Wanxiangshu/Provider.fs'])
  const middle = locality('middle', ['src/Wanxiangshu/Middle.fs'], ['provider'])
  const direct = locality('direct', ['src/Wanxiangshu/Direct.fs'], ['provider'])
  const transitive = locality('transitive', ['src/Wanxiangshu/Transitive.fs'], ['middle'])
  const missing = locality('missing', ['src/Wanxiangshu/Missing.fs'], [], provider.owner)
  const external = locality('external', ['src/Wanxiangshu/External.fs'])

  const result = analyzeLocalityDependencies({
    localities: [provider, middle, direct, transitive, missing, external],
    declarationUses: [
      use(direct.sources[0], provider.sources[0]),
      use(transitive.sources[0], provider.sources[0], { isFromType: true, symbolKind: 'FSharpEntity' }),
      use(missing.sources[0], provider.sources[0], { isFromPattern: true, symbolKind: 'FSharpUnionCase' }),
      use(middle.sources[0], provider.sources[0], { isFromOpenStatement: true, symbolKind: 'FSharpEntity' }),
      use(external.sources[0], provider.sources[0], { isNamespace: true, symbolKind: 'FSharpEntity' }),
      use(external.sources[0], null, { symbol: 'System.String', assembly: 'System.Runtime' }),
    ],
  })

  assert.deepEqual(result.violations, [
    {
      code: 'missing-closure-edge',
      consumerLocality: 'missing',
      consumerSource: 'src/Wanxiangshu/Missing.fs',
      providerLocality: 'provider',
      providerSource: 'src/Wanxiangshu/Provider.fs',
      symbol: 'Wanxiangshu.Provider.value',
      line: 3,
      column: 4,
    },
  ])
  assert.deepEqual(
    result.edges.map(({ consumerLocality, providerLocality }) => [consumerLocality, providerLocality]),
    [
      ['direct', 'provider'],
      ['middle', 'provider'],
      ['missing', 'provider'],
      ['transitive', 'provider'],
    ],
  )
  assert.equal(result.census.localities, 6)
  assert.equal(result.census.actualSourceEdges, 4)
  assert.equal(result.census.missingClosureEdges, 1)

  const diagnosticOnlyChanges = analyzeLocalityDependencies({
    localities: [provider, direct],
    declarationUses: [
      use(direct.sources[0], provider.sources[0], { symbol: 'later', line: 12, column: 8 }),
      use(direct.sources[0], provider.sources[0], { symbol: 'earlier', line: 2, column: 1 }),
    ],
  })
  assert.equal(diagnosticOnlyChanges.edges.length, 1)
  assert.deepEqual(
    diagnosticOnlyChanges.edges.map(({ consumerSource, providerSource }) => ({ consumerSource, providerSource })),
    [{ consumerSource: direct.sources[0], providerSource: provider.sources[0] }],
    'symbol and source coordinates must not participate in edge identity',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-011] locality analysis is owner-independent and fail-closed', () => {
  const provider = locality('provider', ['src/Wanxiangshu/Provider.fs'], [], 'shared-owner')
  const consumer = locality('consumer', ['src/Wanxiangshu/Consumer.fs'], [], 'shared-owner')

  const sameOwnerLeak = analyzeLocalityDependencies({
    localities: [provider, consumer],
    declarationUses: [use(consumer.sources[0], provider.sources[0])],
  })
  assert.equal(sameOwnerLeak.violations[0]?.code, 'missing-closure-edge')

  assert.throws(
    () => analyzeLocalityDependencies({ localities: [provider, consumer] }),
    /declarationUses must be an array/,
  )
  assert.throws(
    () =>
      analyzeLocalityDependencies({
        localities: [provider, locality('duplicate', provider.sources)],
        declarationUses: [],
      }),
    /compiled by multiple localities/,
  )
})
