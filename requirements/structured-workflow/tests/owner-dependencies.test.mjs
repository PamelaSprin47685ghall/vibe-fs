import assert from 'node:assert/strict'
import { join, resolve } from 'node:path'
import test from 'node:test'

import { analyzeOwnerContracts } from '../../../scripts/checks/owner-contracts.mjs'
import { buildTraceGraph } from '../../../scripts/lib/requirement-trace.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const REQUIREMENT_TRACE = buildTraceGraph(join(ROOT, 'requirements'))

const file = (path) => ({ path })

const ownership = (...entries) => ({
  owners: [...new Set(entries.map((entry) => entry.owner))],
  ownership: entries,
})

const registry = (overrides = {}) => ({
  schema_version: 1,
  contracts: [],
  physical_adapters: [],
  composition_roots: [],
  requirement_dependencies: [],
  owner_cycle_justifications: [],
  ...overrides,
})

const analyze = ({ files, owners, contracts = registry(), migrationState, requirementTrace = REQUIREMENT_TRACE }) =>
  analyzeOwnerContracts({
    compilePaths: files.map((entry) => entry.path),
    semanticOwners: ownership(...owners),
    publishedContracts: contracts,
    migrationState,
    requirementTrace,
    repositoryRoot: ROOT,
  })

const codes = (result) => result.violations.map((violation) => violation.code)

test('WHAT[STRUCTURED-WORKFLOW-011] duplicate primary owners are rejected', () => {
  const contract = file('src/Wanxiangshu/Shared/Contract.fs')
  const result = analyze({
    files: [contract],
    owners: [
      { path: contract.path, owner: 'alpha' },
      { path: contract.path, owner: 'beta' },
    ],
  })

  assert.ok(codes(result).includes('duplicate-primary-owner'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] unowned production modules are rejected', () => {
  const orphan = file('src/Wanxiangshu/Orphan/Module.fs')
  const result = analyze({ files: [orphan], owners: [] })

  assert.ok(codes(result).includes('unowned-production-module'))
})

const ownerCycleFixture = (cycleJustifications = []) => {
  const alpha = file('src/Wanxiangshu/Alpha/Contract.fs')
  const beta = file('src/Wanxiangshu/Beta/Contract.fs')
  return analyze({
    files: [alpha, beta],
    owners: [
      { path: alpha.path, owner: 'alpha' },
      { path: beta.path, owner: 'beta' },
    ],
    contracts: registry({
      contracts: [
        {
          path: alpha.path,
          owner: 'alpha',
          kind: 'published-contract',
          consumers: ['beta'],
          symbols: ['Wanxiangshu.Alpha.Contract.alpha'],
          justification: 'Beta consumes the stable alpha outcome contract.',
        },
        {
          path: beta.path,
          owner: 'beta',
          kind: 'published-contract',
          consumers: ['alpha'],
          symbols: ['Wanxiangshu.Beta.Contract.beta'],
          justification: 'Alpha consumes the stable beta outcome contract.',
        },
      ],
      owner_cycle_justifications: cycleJustifications,
    }),
  })
}

test('WHAT[STRUCTURED-WORKFLOW-011] an exact owner-cycle justification shape is accepted', () => {
  const result = ownerCycleFixture([
    {
      owners: ['alpha', 'beta'],
      justification: 'The two contracts form one explicitly reviewed sovereignty bridge until their joint cutover.',
    },
  ])

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] durable semantic cursor evidence with exact proof is accepted', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Context.Trace.XTraceCursor.sequence'
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: cursor.path,
          owner: 'semantic-trace',
          kind: 'semantic-evidence',
          consumers: ['consumer'],
          symbols: [symbol],
          law: 'WHAT[SEMANTIC-TRACE-003]',
          proof: {
            path: 'requirements/semantic-trace/tests/x-trace.test.mjs',
            title: 'WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque',
            what_id: 'SEMANTIC-TRACE-003',
            surface_module: 'Context/Trace/SemanticTraceSurface.js',
          },
          justification: 'The durable semantic cursor records replay evidence and never selects executable workflow control.',
        },
      ],
    }),
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] semantic evidence without an exact production surface is rejected', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Context.Trace.XTraceCursor.sequence'
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({ contracts: [{
      path: cursor.path,
      owner: 'semantic-trace',
      kind: 'semantic-evidence',
      consumers: ['consumer'],
      symbols: [symbol],
      law: 'WHAT[SEMANTIC-TRACE-003]',
      proof: {
        path: 'requirements/semantic-trace/tests/x-trace.test.mjs',
        title: 'WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque',
        what_id: 'SEMANTIC-TRACE-003',
      },
      justification: 'A trace edge without its production surface does not prove production behavior.',
    }] }),
  })

  assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] an active same-owner proof without callback surface use is rejected', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Context.Trace.XTraceCursor.sequence'
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({ contracts: [{
      path: cursor.path,
      owner: 'semantic-trace',
      kind: 'semantic-evidence',
      consumers: ['consumer'],
      symbols: [symbol],
      law: 'WHAT[SEMANTIC-TRACE-005]',
      proof: {
        path: 'requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs',
        title: 'WHAT[SEMANTIC-TRACE-005] raw projection storage is rejected while copied semantic query is admitted',
        what_id: 'SEMANTIC-TRACE-005',
        surface_module: 'Context/Trace/SemanticTraceSurface.js',
      },
      justification: 'An active owner-law proof that does not call this surface cannot authorize the edge.',
    }] }),
  })

  assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] a comment-only WHAT mention cannot authorize semantic evidence', () => {
  const cursor = file('src/Wanxiangshu/ExternalInvestigation/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.ExternalInvestigation.Cursor.current'
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'external-investigation' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: cursor.path,
          owner: 'external-investigation',
          kind: 'semantic-evidence',
          consumers: ['consumer'],
          symbols: [symbol],
          law: 'WHAT[EXTERNAL-INVESTIGATION-010]',
          proof: {
            path: 'requirements/external-investigation/tests/browser-provenance-canary.test.mjs',
            title: 'WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office',
            what_id: 'EXTERNAL-INVESTIGATION-010',
            surface_module: 'Context/Trace/SemanticTraceSurface.js',
          },
          justification: 'A comment that names another proof must never authorize semantic evidence.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] semantic evidence rejects bare paths wrong identities traversal and inactive tests', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Context.Trace.XTraceCursor.sequence'
  const exact = {
    path: 'requirements/semantic-trace/tests/x-trace.test.mjs',
    title: 'WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque',
    what_id: 'SEMANTIC-TRACE-003',
    surface_module: 'Context/Trace/SemanticTraceSurface.js',
  }
  const malformed = [
    exact.path,
    { ...exact, title: `${exact.title} renamed` },
    { ...exact, what_id: 'SEMANTIC-TRACE-006' },
    { ...exact, path: 'requirements/semantic-trace/tests/../tests/x-trace.test.mjs' },
  ]
  const run = (proof, requirementTrace = REQUIREMENT_TRACE) => analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [{
        path: cursor.path,
        owner: 'semantic-trace',
        kind: 'semantic-evidence',
        consumers: ['consumer'],
        symbols: [symbol],
        law: 'WHAT[SEMANTIC-TRACE-003]',
        proof,
        justification: 'Malformed semantic evidence must be rejected on metadata alone.',
      }],
    }),
    requirementTrace,
  })

  for (const proof of malformed) {
    const result = run(proof)
    assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
  }

  const skippedProof = {
    path: 'requirements/semantic-trace/tests/skipped.test.mjs',
    title: 'WHAT[SEMANTIC-TRACE-003] skipped cursor claim',
    what_id: 'SEMANTIC-TRACE-003',
    surface_module: 'Context/Trace/SemanticTraceSurface.js',
  }
  for (const state of ['skip', 'todo']) {
    const inactiveTrace = {
      whats: new Map([['SEMANTIC-TRACE-003', { package: 'semantic-trace' }]]),
      proofEdges: [{
        file: join(ROOT, skippedProof.path),
        proofFile: join(ROOT, 'requirements/semantic-trace/HOW.md'),
        state,
        title: skippedProof.title,
        whatId: skippedProof.what_id,
      }],
    }
    const inactive = run(skippedProof, inactiveTrace)
    assert.ok(codes(inactive).includes('invalid-semantic-evidence-metadata'))
  }

  const foreignLaw = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({ contracts: [{
      path: cursor.path,
      owner: 'semantic-trace',
      kind: 'semantic-evidence',
      consumers: ['consumer'],
      symbols: [symbol],
      law: 'WHAT[EXTERNAL-INVESTIGATION-010]',
      proof: {
        path: 'requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs',
        title: 'WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office',
        what_id: 'EXTERNAL-INVESTIGATION-010',
        surface_module: 'Context/Trace/SemanticTraceSurface.js',
      },
      justification: 'A foreign owner law must never authorize this provider semantic evidence.',
    }] }),
  })
  assert.ok(codes(foreignLaw).includes('invalid-semantic-evidence-metadata'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] semantic evidence fails closed without normative metadata or with symbol roots', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'semantic-trace' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: cursor.path,
          owner: 'semantic-trace',
          kind: 'semantic-evidence',
          consumers: ['consumer'],
          symbol_roots: ['Wanxiangshu.Context.Trace.XTraceCursor'],
          justification: 'This malformed fixture must not weaken exact durable-evidence authorization.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
  assert.ok(codes(result).includes('invalid-semantic-evidence-authorization'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] an exact published symbol is accepted', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Provider.Contract.value'
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: provider.path,
          owner: 'provider',
          kind: 'published-contract',
          consumers: ['consumer'],
          symbols: [symbol],
          justification: 'Consumer uses the provider-owned stable outcome symbol.',
        },
      ],
    }),
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

const migrationContractFixture = ({
  closed = true,
  entryNode = 'provider-cutover',
  nodeState = 'DONE',
  proofs,
  publishes = ['Provider.Contract'],
} = {}) => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Provider.Contract.value'
  const node = {
    id: 'provider-cutover',
    state: nodeState,
    publishes,
    ...(proofs === undefined ? {} : { proofs }),
  }
  return analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: provider.path,
          owner: 'provider',
          node: entryNode,
          contract: 'Provider.Contract',
          kind: 'published-contract',
          consumers: ['consumer'],
          symbols: [symbol],
          justification: 'The completed provider cutover publishes this exact stable contract.',
        },
      ],
    }),
    migrationState: {
      closedPaths: closed ? [provider.path] : [],
      nodeByPath: [[provider.path, node.id]],
      nodes: [node],
      closedOwners: closed ? ['provider'] : [],
    },
  })
}

test('WHAT[STRUCTURED-WORKFLOW-011] a published contract binds its exact DONE node and vocabulary', () => {
  const result = migrationContractFixture()

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] contracts cannot publish before cutover', () => {
  assert.ok(codes(migrationContractFixture({ closed: false })).includes('contract-before-cutover'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] stale migration node and vocabulary bindings fail closed', () => {
  assert.ok(codes(migrationContractFixture({ entryNode: 'other-cutover' })).includes('contract-node-mismatch'))
  assert.ok(codes(migrationContractFixture({ nodeState: 'RUNNING' })).includes('contract-node-mismatch'))
  assert.ok(codes(migrationContractFixture({ publishes: [] })).includes('contract-vocabulary-mismatch'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] migration proof inventory cannot grant or deny contract authority', () => {
  const absent = migrationContractFixture()
  const unrelated = migrationContractFixture({ proofs: ['requirements/structured-workflow/tests/missing-contract.test.mjs'] })

  assert.equal(absent.ok, true, JSON.stringify(absent.violations, null, 2))
  assert.equal(unrelated.ok, true, JSON.stringify(unrelated.violations, null, 2))
  assert.equal(codes(unrelated).includes('contract-without-proof'), false)
})

test('WHAT[STRUCTURED-WORKFLOW-011] wildcard authorizations fail closed', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const unused = file('src/Wanxiangshu/Unused/Model.fs')
  const symbol = 'Wanxiangshu.Provider.Contract.value'
  const result = analyze({
    files: [provider, consumer, unused],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
      { path: unused.path, owner: 'unused' },
    ],
    contracts: registry({
      contracts: [
        {
          path: provider.path,
          owner: 'provider',
          kind: 'published-contract',
          consumers: ['consumer', 'unused'],
          symbols: [symbol, 'Wanxiangshu.Provider.*'],
          justification: 'The fixture deliberately contains a wildcard authorization.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('invalid-symbol-authorization'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] a symbol root declaration shape is accepted', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const aggregate = 'Wanxiangshu.Provider.ProviderOutcome'
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: provider.path,
          owner: 'provider',
          kind: 'published-contract',
          consumers: ['consumer'],
          symbol_roots: [aggregate],
          justification: 'Consumer may observe the complete provider-owned outcome aggregate.',
        },
      ],
    }),
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] an adapter target cannot authorize an undeclared provider port', () => {
  const provider = file('src/Wanxiangshu/Provider/InternalPhysical.fs')
  const adapter = file('src/Wanxiangshu/Host/Adapter.fs')
  const portRoot = 'Wanxiangshu.Provider.ProviderDevice'
  const result = analyze({
    files: [provider, adapter],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: adapter.path, owner: 'host' },
    ],
    contracts: registry({
      physical_adapters: [
        {
          path: adapter.path,
          owner: 'host',
          ports: [{ path: provider.path, symbol_roots: [portRoot] }],
          justification: 'The declaration cannot turn a private provider symbol into a physical port by itself.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('undeclared-physical-port'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] an exact physical port and adapter target is accepted', () => {
  const port = file('src/Wanxiangshu/Provider/Port.fs')
  const adapter = file('src/Wanxiangshu/Host/Adapter.fs')
  const portRoot = 'Wanxiangshu.Provider.ProviderPort'
  const result = analyze({
    files: [port, adapter],
    owners: [
      { path: port.path, owner: 'provider' },
      { path: adapter.path, owner: 'host' },
    ],
    contracts: registry({
      contracts: [
        {
          path: port.path,
          owner: 'provider',
          kind: 'physical-port',
          consumers: ['host'],
          symbol_roots: [portRoot],
          justification: 'The port is the provider-owned physical observation boundary.',
        },
      ],
      physical_adapters: [
        {
          path: adapter.path,
          owner: 'host',
          ports: [{ path: port.path, symbol_roots: [portRoot] }],
          justification: 'The adapter translates the exact provider port into host physical I/O.',
        },
      ],
    }),
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] physical adapters reject bare path targets', () => {
  const port = file('src/Wanxiangshu/Provider/Port.fs')
  const adapter = file('src/Wanxiangshu/Host/Adapter.fs')
  const result = analyze({
    files: [port, adapter],
    owners: [
      { path: port.path, owner: 'provider' },
      { path: adapter.path, owner: 'host' },
    ],
    contracts: registry({
      physical_adapters: [
        {
          path: adapter.path,
          owner: 'host',
          ports: [port.path],
          justification: 'The adapter declaration deliberately uses the forbidden legacy target shape.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('invalid-physical-adapter'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] exact composition-root wiring is accepted', () => {
  const contract = file('src/Wanxiangshu/Provider/Contract.fs')
  const root = file('src/Wanxiangshu/Host/Root.fs')
  const symbol = 'Wanxiangshu.Provider.Contract.create'
  const result = analyze({
    files: [contract, root],
    owners: [
      { path: contract.path, owner: 'provider' },
      { path: root.path, owner: 'host' },
    ],
    contracts: registry({
      composition_roots: [
        {
          path: root.path,
          owner: 'host',
          wires: [{ path: contract.path, symbols: [symbol] }],
          justification: 'The root constructs and orders only the exact provider-owned factory.',
        },
      ],
    }),
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] requirement edges need prose', () => {
  const owned = file('src/Wanxiangshu/Alpha/Model.fs')
  const result = analyze({
    files: [owned],
    owners: [{ path: owned.path, owner: 'alpha' }],
    contracts: registry({
      requirement_dependencies: [
        { consumer: 'alpha', provider: 'beta', justification: '' },
        {
          consumer: 'alpha',
          provider: 'gamma',
          justification: 'Alpha law consumes Gamma evidence without creating a production import.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('invalid-requirement-dependency'))
})
