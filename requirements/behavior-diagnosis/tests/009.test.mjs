import assert from 'node:assert/strict'
import test, { afterEach } from 'node:test'
import { call, decode } from './support/cycle.mjs'

// Let Node deliver completed journal work between the synchronous steps.
afterEach(() => new Promise(resolve => setImmediate(resolve)))

test('WHAT[behavior-diagnosis-009] actual cycle decoder accepts one completed call and refuses zero or two', () => {
  const single = decode([call()])
  assert.equal(single.decodedCalls, 1)
  assert.deepEqual(single.decision, {
    ok: true,
    value: {
      text: 'resolve the current question work result settled continue on the settled path',
      evidence: '',
      ruleId: 'primitive-obsession',
      toolCallIds: ['call-1'],
    },
  })
  for (const parts of [[], [call(), call({ callID: 'call-2' })]]) {
    assert.equal(decode(parts).decision.ok, false)
  }
})

test('WHAT[behavior-diagnosis-009] raw cardinality precedes decode filtering on the decoder surface', () => {
  // One decodable call plus one completed-but-undecodable chronicle call:
  // the raw cardinality of the step is two, and the breach stands no matter
  // how many of those calls decode.
  const undecodable = call({
    callID: 'call-2',
    state: { status: 'completed', input: { charge: 'only a charge, nothing else' } },
  })
  const mixed = decode([call(), undecodable])
  assert.equal(mixed.chronicleCallCount, 2, 'raw cardinality counts undecodable calls too')
  assert.equal(mixed.decodedCalls, 1, 'decode filtering alone would hide the second call')
  assert.equal(mixed.decision.ok, false, 'a two-call step never forms a valid cycle')

  // 0 / 1 / 2 legal-call controls: only exactly one raw call yields a cycle.
  const zero = decode([])
  assert.equal(zero.chronicleCallCount, 0)
  assert.equal(zero.decodedCalls, 0)
  assert.equal(zero.decision.ok, false)

  const one = decode([call()])
  assert.equal(one.chronicleCallCount, 1)
  assert.equal(one.decodedCalls, 1)
  assert.equal(one.decision.ok, true)

  const two = decode([call(), call({ callID: 'call-2' })])
  assert.equal(two.chronicleCallCount, 2)
  assert.equal(two.decodedCalls, 2)
  assert.equal(two.decision.ok, false)
})

{
const { default: assert } = await import('node:assert/strict')
const { mkdtempSync, readFileSync, rmSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join } = await import('node:path')
const blog = await import('../../../dist/Enforcer/BlogSurface.js')
const journal = await import('../../../dist/Persistence/Journal/Surface.js')
const runtime = await import('../../../dist/Context/Companion/RuntimeSurface.js')
const resources = await import('../../../dist/Resources/PromptSurface.js')
const ownership = await import('../../verification-system/tests/support/blogger-ownership.mjs')

resources.runtimeInstallFromPackage()

let ownerCounter = 0
const setupOwner = async (t) => {
  ownerCounter += 1
  const n = ownerCounter
  const ids = {
    main: `ses-main-bd009-${n}`,
    blogger: `ses-blogger-bd009-${n}`,
    request: `req-blog-bd009-${n}`,
    root: `msg-root-blog-bd009-${n}`,
    physical: `msg-phys-blog-bd009-${n}`,
  }
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bd009-'))
  const opened = await journal.JournalSurface_bootWithWriterId(
    dir,
    `writer-bd009-${n}`,
    `rt-bd009-${n}`,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const durable = opened.journal.journal
  await ownership.linkBlogger(opened.journal, ids.main, ids.blogger)
  const profile = await ownership.rootBlogger(opened.journal, ids.blogger, ids.root)
  // Real process-local owner scope (isolates the shared flight registry).
  const scope = runtime.createScope()
  const request = runtime.main({
    requestId: ids.request,
    mainSession: ids.main,
    bloggerSession: ids.blogger,
    toml: 'bd-009-toml',
  })
  assert.equal(runtime.claimCurrentRequest(scope, ids.blogger, request), 'Claimed')
  await ownership.ownRequest({
    handle: opened.journal,
    durable,
    scope,
    bloggerSession: ids.blogger,
    profile,
    request,
    physical: ids.physical,
  })
  t.after(() => {
    try {
      runtime.dispose(scope)
    } catch {}
    try {
      journal.JournalSurface_dispose(opened.journal)
    } catch {}
    rmSync(dir, { recursive: true, force: true })
  })
  return {
    ids,
    durable,
    scope,
    request,
    writerFile: join(dir, 'wanxiang', 'events', `writer-bd009-${n}.ndjson`),
  }
}

const ownedTerminal = (ids, run, parts = []) => [
  ownership.userMessage(ids.physical),
  ownership.assistantMessage(run, ids.physical, parts),
]

// BlogObservationCommitted is the single durable fact that atomically carries
// frame append, RecordCoverage advance, tip and identities (WHAT-012), so the
// count of committed facts in the real writer file is the coverage verdict.
// A writer line is canonical JSON (durable-events-003): the journal envelope
// rides in payload, and its fact is the union path
// Agent → Context → BlogObservationCommitted.
const isObservationLine = line => {
  let parsed
  try {
    parsed = JSON.parse(line)
  } catch {
    return false
  }
  const fact = parsed?.payload?.Fact
  return Array.isArray(fact)
    && fact[0] === 'Agent'
    && Array.isArray(fact[1])
    && fact[1][0] === 'Context'
    && Array.isArray(fact[1][1])
    && fact[1][1][0] === 'BlogObservationCommitted'
}

const committedCount = (writerFile) => {
  let text = ''
  try {
    text = readFileSync(writerFile, 'utf8')
  } catch {
    return 0
  }
  return text
    .split('\n')
    .filter(line => line.trim() !== '')
    .filter(isObservationLine)
    .length
}

const undecodableChronicle = callID => ({
  type: 'tool',
  tool: 'chronicle',
  callID,
  state: { status: 'completed', input: { charge: 'only a charge, nothing else' } },
})

test('WHAT[behavior-diagnosis-009] GAP-112 real raw two-call terminal with only one decodable call commits nothing and advances no coverage', async (t) => {
  const { ids, durable, scope, writerFile } = await setupOwner(t)
  const parts = [
    ownership.chroniclePart('call-1', 'primitive-obsession', 'one decodable observation'),
    undecodableChronicle('call-2'),
  ]

  const outcome = await blog.continueTransform(
    scope,
    durable,
    ids.blogger,
    ownedTerminal(ids, 'run-mixed', parts),
  )
  assert.ok(['ProjectMessages', 'StopPhysicalRun'].includes(outcome.kind))

  // The raw cardinality breach commits no BlogObservationCommitted, and
  // coverage only ever advances through that single fact — zero facts means
  // zero coverage advance.
  assert.equal(committedCount(writerFile), 0)
  // No commit, no release: the exact flight still holds the open request.
  assert.equal(runtime.tryGetFlight(scope, ids.blogger).requestId, ids.request)
})

test('WHAT[behavior-diagnosis-009] a single fully decodable call still commits exactly one observation through the same real chain', async (t) => {
  const { ids, durable, scope, writerFile } = await setupOwner(t)
  const parts = [ownership.chroniclePart('call-1', 'primitive-obsession', 'one decodable observation')]

  // A successful commit parks the continuation waiting for new material, so
  // the durable effect is the assertion target — not the parked return.
  const pending = blog.continueTransform(
    scope,
    durable,
    ids.blogger,
    ownedTerminal(ids, 'run-single', parts),
  )
  const deadline = Date.now() + 5000
  while (committedCount(writerFile) === 0 && Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 20))
  }
  assert.equal(committedCount(writerFile), 1)
  assert.equal(runtime.tryGetFlight(scope, ids.blogger), null)

  runtime.dispose(scope)
  await Promise.race([pending, new Promise(resolve => setTimeout(resolve, 1000))])
})
}
