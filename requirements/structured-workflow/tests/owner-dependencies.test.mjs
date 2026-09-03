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

const symbolUse = (consumer, provider, symbol, overrides = {}) => ({
  consumerPath: consumer.path ?? consumer,
  providerPaths: provider ? [provider.path ?? provider] : [],
  symbol,
  symbolKind: 'FSharpMemberOrFunctionOrValue',
  line: 1,
  column: 0,
  isNamespace: false,
  isModule: false,
  isFromOpenStatement: false,
  isFromPattern: false,
  isFromType: false,
  isFromUse: true,
  missingDeclaration: false,
  ...overrides,
})

const analyze = ({ files, owners, contracts = registry(), uses = [], migrationState, requirementTrace = REQUIREMENT_TRACE }) =>
  analyzeOwnerContracts({
    compilePaths: files.map((entry) => entry.path),
    semanticOwners: ownership(...owners),
    publishedContracts: contracts,
    symbolUses: uses,
    migrationState,
    requirementTrace,
    repositoryRoot: ROOT,
  })

const codes = (result) => result.violations.map((violation) => violation.code)

test('WHAT[STRUCTURED-WORKFLOW-011] private cross-owner symbols are rejected', () => {
  const provider = file('src/Wanxiangshu/Provider/Internal.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [
      symbolUse(consumer, provider, 'Wanxiangshu.Provider.Internal.secret', {
        line: 17,
        column: 9,
      }),
    ],
  })

  const violation = result.violations.find((entry) => entry.code === 'cross-owner-private-import')
  assert.deepEqual(
    { code: violation?.code, message: violation?.message },
    {
      code: 'cross-owner-private-import',
      message:
        'src/Wanxiangshu/Consumer/Use.fs:17:9 → src/Wanxiangshu/Provider/Internal.fs: consumer may not consume provider symbol \'Wanxiangshu.Provider.Internal.secret\'',
    },
  )
})

test('WHAT[STRUCTURED-WORKFLOW-011] Surface naming never substitutes for a declared contract', () => {
  const provider = file('src/Wanxiangshu/Provider/Surface.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [symbolUse(consumer, provider, 'Wanxiangshu.Provider.Surface.value')],
  })

  assert.ok(codes(result).includes('undeclared-published-contract'))
})

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

test('WHAT[STRUCTURED-WORKFLOW-011] analysis fails closed without compiler evidence', () => {
  const owned = file('src/Wanxiangshu/Alpha/Model.fs')
  const result = analyzeOwnerContracts({
    compilePaths: [owned.path],
    semanticOwners: ownership({ path: owned.path, owner: 'alpha' }),
    publishedContracts: registry(),
  })

  assert.ok(codes(result).includes('missing-compiler-symbol-uses'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] composition roots cannot match uncontracted foreign policy cases', () => {
  const policy = file('src/Wanxiangshu/Provider/Policy.fs')
  const root = file('src/Wanxiangshu/Host/Root.fs')
  const policyRoot = 'Wanxiangshu.Provider.PolicyDecision'
  const result = analyze({
    files: [policy, root],
    owners: [
      { path: policy.path, owner: 'provider' },
      { path: root.path, owner: 'host' },
    ],
    contracts: registry({
      composition_roots: [
        {
          path: root.path,
          owner: 'host',
          wires: [{ path: policy.path, symbol_roots: [policyRoot] }],
          justification: 'The host root may construct the provider input but may not own provider policy.',
        },
      ],
    }),
    uses: [
      symbolUse(root, policy, `${policyRoot}.Allow`, {
        symbolKind: 'FSharpUnionCase',
        isFromPattern: true,
      }),
    ],
  })

  assert.ok(codes(result).includes('composition-root-foreign-policy'))
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
    uses: [
      symbolUse(alpha, beta, 'Wanxiangshu.Beta.Contract.beta'),
      symbolUse(beta, alpha, 'Wanxiangshu.Alpha.Contract.alpha'),
    ],
  })
}

test('WHAT[STRUCTURED-WORKFLOW-011] unjustified owner cycles are rejected', () => {
  assert.ok(codes(ownerCycleFixture()).includes('unjustified-owner-cycle'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] an exact live owner-cycle justification is accepted', () => {
  const result = ownerCycleFixture([
    {
      owners: ['alpha', 'beta'],
      justification: 'The two contracts form one explicitly reviewed sovereignty bridge until their joint cutover.',
    },
  ])

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] strict contract cycles remain live while their owners have migration backlog', () => {
  const alphaContract = file('src/Wanxiangshu/Alpha/Contract.fs')
  const alphaBacklog = file('src/Wanxiangshu/Alpha/Backlog.fs')
  const betaContract = file('src/Wanxiangshu/Beta/Contract.fs')
  const betaBacklog = file('src/Wanxiangshu/Beta/Backlog.fs')
  const proof = 'requirements/structured-workflow/tests/owner-dependencies.test.mjs'
  const cycleJustification = {
    owners: ['alpha', 'beta'],
    justification: 'The exact live contract SCC remains reviewed while unrelated owner files finish migrating.',
  }
  const fixture = (ownerCycleJustifications) =>
    analyze({
      files: [alphaContract, alphaBacklog, betaContract, betaBacklog],
      owners: [
        { path: alphaContract.path, owner: 'alpha' },
        { path: alphaBacklog.path, owner: 'alpha' },
        { path: betaContract.path, owner: 'beta' },
        { path: betaBacklog.path, owner: 'beta' },
      ],
      contracts: registry({
        contracts: [
          {
            path: alphaContract.path,
            owner: 'alpha',
            node: 'alpha-contract-cutover',
            contract: 'Alpha.Contract',
            kind: 'published-contract',
            consumers: ['beta'],
            symbols: ['Wanxiangshu.Alpha.Contract.alpha'],
            justification: 'Beta consumes the migrated alpha contract while alpha retains unrelated backlog.',
          },
          {
            path: betaContract.path,
            owner: 'beta',
            node: 'beta-contract-cutover',
            contract: 'Beta.Contract',
            kind: 'published-contract',
            consumers: ['alpha'],
            symbols: ['Wanxiangshu.Beta.Contract.beta'],
            justification: 'Alpha consumes the migrated beta contract while beta retains unrelated backlog.',
          },
        ],
        owner_cycle_justifications: ownerCycleJustifications,
      }),
      uses: [
        symbolUse(alphaContract, betaContract, 'Wanxiangshu.Beta.Contract.beta'),
        symbolUse(betaContract, alphaContract, 'Wanxiangshu.Alpha.Contract.alpha'),
      ],
      migrationState: {
        closedPaths: [alphaContract.path, betaContract.path],
        nodeByPath: [
          [alphaContract.path, 'alpha-contract-cutover'],
          [betaContract.path, 'beta-contract-cutover'],
        ],
        nodes: [
          {
            id: 'alpha-contract-cutover',
            state: 'DONE',
            proofs: [proof],
            publishes: ['Alpha.Contract'],
          },
          {
            id: 'beta-contract-cutover',
            state: 'DONE',
            proofs: [proof],
            publishes: ['Beta.Contract'],
          },
        ],
        closedOwners: [],
      },
    })

  assert.ok(codes(fixture([])).includes('unjustified-owner-cycle'))
  const justified = fixture([cycleJustification])
  assert.equal(justified.ok, true, JSON.stringify(justified.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] stale owner-cycle justifications are rejected', () => {
  const owned = file('src/Wanxiangshu/Alpha/Model.fs')
  const result = analyze({
    files: [owned],
    owners: [{ path: owned.path, owner: 'alpha' }],
    contracts: registry({
      owner_cycle_justifications: [
        {
          owners: ['alpha', 'beta'],
          justification: 'This declaration has no corresponding live strongly connected component.',
        },
      ],
    }),
  })

  assert.ok(codes(result).includes('stale-cycle-justification'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] foreign execution-position vocabulary is rejected even when published', () => {
  const cursor = file('src/Wanxiangshu/Provider/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Provider.Cursor.current'
  const result = analyze({
    files: [cursor, consumer],
    owners: [
      { path: cursor.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    contracts: registry({
      contracts: [
        {
          path: cursor.path,
          owner: 'provider',
          kind: 'published-contract',
          consumers: ['consumer'],
          symbols: [symbol],
          justification: 'The fixture proves vocabulary restrictions outrank contract visibility.',
        },
      ],
    }),
    uses: [symbolUse(consumer, cursor, symbol)],
  })

  assert.ok(codes(result).includes('foreign-execution-position'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] durable semantic cursor evidence crosses the execution-position guard only by exact proof', () => {
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
          },
          justification: 'The durable semantic cursor records replay evidence and never selects executable workflow control.',
        },
      ],
    }),
    uses: [symbolUse(consumer, cursor, symbol)],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
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
          },
          justification: 'A comment that names another proof must never grant an execution-position exception.',
        },
      ],
    }),
    uses: [symbolUse(consumer, cursor, symbol)],
  })

  assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
  assert.ok(codes(result).includes('foreign-execution-position'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] semantic evidence rejects bare paths wrong identities traversal and inactive tests', () => {
  const cursor = file('src/Wanxiangshu/Context/Trace/Cursor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Context.Trace.XTraceCursor.sequence'
  const exact = {
    path: 'requirements/semantic-trace/tests/x-trace.test.mjs',
    title: 'WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque',
    what_id: 'SEMANTIC-TRACE-003',
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
        justification: 'Malformed semantic evidence must never grant an execution-position exception.',
      }],
    }),
    uses: [symbolUse(consumer, cursor, symbol)],
    requirementTrace,
  })

  for (const proof of malformed) {
    const result = run(proof)
    assert.ok(codes(result).includes('invalid-semantic-evidence-metadata'))
    assert.ok(codes(result).includes('foreign-execution-position'))
  }

  const skippedProof = {
    path: 'requirements/semantic-trace/tests/skipped.test.mjs',
    title: 'WHAT[SEMANTIC-TRACE-003] skipped cursor claim',
    what_id: 'SEMANTIC-TRACE-003',
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
    assert.ok(codes(inactive).includes('foreign-execution-position'))
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
      },
      justification: 'A foreign owner law must never grant this provider an execution-position exception.',
    }] }),
    uses: [symbolUse(consumer, cursor, symbol)],
  })
  assert.ok(codes(foreignLaw).includes('invalid-semantic-evidence-metadata'))
  assert.ok(codes(foreignLaw).includes('foreign-execution-position'))
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

test('WHAT[STRUCTURED-WORKFLOW-011] immutable data fields named Cursor are not execution positions', () => {
  const provider = file('src/Wanxiangshu/Provider/Anchor.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const symbol = 'Wanxiangshu.Provider.TraceAnchor.Cursor'
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
          justification: 'The cursor is immutable trace-anchor data, not an executable workflow position.',
        },
      ],
    }),
    uses: [symbolUse(consumer, provider, symbol, { symbolKind: 'FSharpField' })],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
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
    uses: [symbolUse(consumer, provider, symbol)],
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
    uses: [symbolUse(consumer, provider, symbol)],
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
  assert.equal(result.strictEdges.length, 1)
  assert.equal(result.pendingEdges.length, 0)
})

test('WHAT[STRUCTURED-WORKFLOW-011] pending providers stay visible and cannot publish contracts before cutover', () => {
  const provider = file('src/Wanxiangshu/Provider/Internal.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const pending = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [symbolUse(consumer, provider, 'Wanxiangshu.Provider.Internal.value')],
    migrationState: { closedPaths: [], nodeByPath: [], nodes: [], closedOwners: [] },
  })

  assert.equal(pending.ok, true, JSON.stringify(pending.violations, null, 2))
  assert.equal(pending.pendingEdges.length, 1)
  assert.equal(pending.strictEdges.length, 0)
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

test('WHAT[STRUCTURED-WORKFLOW-011] wildcard authorizations and stale consumer grants fail closed', () => {
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
          justification: 'The fixture deliberately contains a wildcard and a stale consumer grant.',
        },
      ],
    }),
    uses: [symbolUse(consumer, provider, symbol)],
  })

  assert.ok(codes(result).includes('invalid-symbol-authorization'))

  const staleConsumer = analyze({
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
          symbols: [symbol],
          justification: 'The unused owner deliberately has no compiler-resolved edge to this contract.',
        },
      ],
    }),
    uses: [symbolUse(consumer, provider, symbol)],
  })

  assert.ok(codes(staleConsumer).includes('stale-contract-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] a symbol root authorizes only that aggregate', () => {
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
    uses: [symbolUse(consumer, provider, `${aggregate}.Completed`, { symbolKind: 'FSharpUnionCase' })],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] a published file does not authorize a sibling symbol', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const published = 'Wanxiangshu.Provider.Contract.published'
  const secret = 'Wanxiangshu.Provider.Contract.secret'
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
          symbols: [published],
          justification: 'Consumer receives only the provider-owned published value.',
        },
      ],
    }),
    uses: [symbolUse(consumer, provider, published), symbolUse(consumer, provider, secret, { line: 2 })],
  })

  assert.ok(codes(result).includes('unauthorized-contract-symbol'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] a published symbol does not authorize a sibling consumer', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const allowed = file('src/Wanxiangshu/Allowed/Use.fs')
  const denied = file('src/Wanxiangshu/Denied/Use.fs')
  const symbol = 'Wanxiangshu.Provider.Contract.published'
  const result = analyze({
    files: [provider, allowed, denied],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: allowed.path, owner: 'allowed' },
      { path: denied.path, owner: 'denied' },
    ],
    contracts: registry({
      contracts: [
        {
          path: provider.path,
          owner: 'provider',
          kind: 'published-contract',
          consumers: ['allowed'],
          symbols: [symbol],
          justification: 'Only the allowed owner receives this provider-owned symbol.',
        },
      ],
    }),
    uses: [symbolUse(allowed, provider, symbol), symbolUse(denied, provider, symbol, { line: 2 })],
  })

  assert.ok(codes(result).includes('unauthorized-contract-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] stale exact symbol grants are rejected', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
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
          symbols: ['Wanxiangshu.Provider.Contract.removed'],
          justification: 'The declaration deliberately names a symbol with no live compiler edge.',
        },
      ],
    }),
    uses: [symbolUse(consumer, provider, 'Wanxiangshu.Provider.Contract.live')],
  })

  assert.ok(codes(result).includes('stale-symbol-authorization'))
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
    uses: [symbolUse(adapter, provider, `${portRoot}.Read`)],
  })

  assert.ok(codes(result).includes('cross-owner-private-import'))
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
    uses: [symbolUse(adapter, port, `${portRoot}.Read`, { symbolKind: 'FSharpMemberOrFunctionOrValue' })],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.allowedEdges[0]?.authorizationKind, 'physical-adapter')
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
    uses: [symbolUse(root, contract, symbol)],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[STRUCTURED-WORKFLOW-011] composition-root targets do not authorize sibling symbols', () => {
  const contract = file('src/Wanxiangshu/Provider/Contract.fs')
  const root = file('src/Wanxiangshu/Host/Root.fs')
  const published = 'Wanxiangshu.Provider.Contract.create'
  const secret = 'Wanxiangshu.Provider.Contract.secret'
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
          wires: [{ path: contract.path, symbols: [published] }],
          justification: 'The root is limited to one exact provider-owned factory.',
        },
      ],
    }),
    uses: [symbolUse(root, contract, published), symbolUse(root, contract, secret, { line: 2 })],
  })

  assert.ok(codes(result).includes('undeclared-published-contract'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] source and requirement graphs stay distinct and requirement edges need prose', () => {
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
  assert.notDeepEqual(result.sourceOwnerEdges, result.requirementOwnerEdges)
})

test('WHAT[STRUCTURED-WORKFLOW-011] compiler symbol uses are authoritative without lexical evidence', () => {
  const provider = file('src/Wanxiangshu/Provider/Internal.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [symbolUse(consumer, provider, 'Wanxiangshu.Provider.Internal.inferred')],
  })

  assert.ok(codes(result).includes('cross-owner-private-import'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] type-only symbol uses remain dependency evidence', () => {
  const provider = file('src/Wanxiangshu/Provider/Model.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [
      symbolUse(consumer, provider, 'Wanxiangshu.Provider.HiddenOutcome', {
        symbolKind: 'FSharpEntity',
        isFromType: true,
        isFromUse: false,
      }),
    ],
  })

  assert.ok(codes(result).includes('cross-owner-private-import'))
  assert.equal(result.sourceEdges[0].useKind, 'type')
})

test('WHAT[STRUCTURED-WORKFLOW-011] same-owner cross-file uses are not border crossings', () => {
  const provider = file('src/Wanxiangshu/Alpha/Provider.fs')
  const consumer = file('src/Wanxiangshu/Alpha/Consumer.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'alpha' },
      { path: consumer.path, owner: 'alpha' },
    ],
    uses: [symbolUse(consumer, provider, 'Wanxiangshu.Alpha.Provider.value')],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.sourceEdges.length, 0)
})

test('WHAT[STRUCTURED-WORKFLOW-011] external symbols and raw open tokens are ignored', () => {
  const provider = file('src/Wanxiangshu/Provider/Contract.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [provider, consumer],
    owners: [
      { path: provider.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [
      symbolUse(consumer, null, 'System.String', { symbolKind: 'FSharpEntity', isFromType: true }),
      symbolUse(consumer, provider, 'Wanxiangshu.Provider.Contract', {
        symbolKind: 'FSharpEntity',
        isModule: true,
        isFromOpenStatement: true,
      }),
    ],
  })

  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.sourceEdges.length, 0)
})

test('WHAT[STRUCTURED-WORKFLOW-011] project symbols without declaration locations fail closed', () => {
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [consumer],
    owners: [{ path: consumer.path, owner: 'consumer' }],
    uses: [symbolUse(consumer, null, 'Wanxiangshu.Missing.value', { missingDeclaration: true })],
  })

  assert.ok(codes(result).includes('missing-symbol-declaration'))
})

test('WHAT[STRUCTURED-WORKFLOW-011] multiple production declaration locations fail closed', () => {
  const left = file('src/Wanxiangshu/Provider/Left.fs')
  const right = file('src/Wanxiangshu/Provider/Right.fs')
  const consumer = file('src/Wanxiangshu/Consumer/Use.fs')
  const result = analyze({
    files: [left, right, consumer],
    owners: [
      { path: left.path, owner: 'provider' },
      { path: right.path, owner: 'provider' },
      { path: consumer.path, owner: 'consumer' },
    ],
    uses: [
      symbolUse(consumer, left, 'Wanxiangshu.Provider.ambiguous', {
        providerPaths: [left.path, right.path],
      }),
    ],
  })

  assert.ok(codes(result).includes('ambiguous-symbol-declaration'))
})
