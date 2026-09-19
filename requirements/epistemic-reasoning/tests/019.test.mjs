import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { randomUUID } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const withRepo = async (t, fn) => {
  const root = mkdtempSync(join(tmpdir(), 'sphinx-spine-'))
  execFileSync('git', ['init', '-q', root])
  t.after(() => rmSync(root, { recursive: true, force: true }))
  await fn(join(root, '.git'))
}
const jsEvent = (inquiryId, revision, kind, payload, parents = []) => ({
  inquiryId,
  revision,
  kind,
  parents,
  payload,
})
const encode = (event) => {
  const result = gecSurface.encodeSphinxEnvelope(event)
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
  const { envelope } = result
  assert.match(envelope.id, /^[0-9a-f]{40}$/)
  assert.equal(envelope.stream, `sphinx/${event.inquiryId}`)
  assert.equal(envelope.type, `sphinx/${event.kind}`)
  assert.deepEqual(envelope.payloadRefs, [])
  return envelope
}
const appendDurable = async (handle, envelope) => {
  const result = await eventStore.append(handle, [envelope])
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
}
const readSpine = (handle, stream) => {
  const head = eventStore.head(handle, stream)
  if (head == null) return []
  const ordered = []
  let cursor = head
  while (cursor != null) {
    const envelope = eventStore.read(handle, cursor)
    assert.ok(envelope != null, `durable spine is missing ${cursor}`)
    ordered.unshift(envelope)
    cursor = envelope.parents[0] ?? null
  }
  return ordered
}
const fold = (envelopes) => {
  const state = gecSurface.sphinxCurrent({ envelopes })
  assert.equal(state.ok, true, JSON.stringify(state.error ?? null))
  return state
}

test('WHAT[epistemic-reasoning-019] append_before_current_only_advances_after_durable_append_and_rejects_stale_expected_revision', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const handle = eventStore.create(commonDir, 'writer-spine-append')
    try {
      const genesis = fold([])
      assert.equal(genesis.current.revision, 0)
      assert.deepEqual(genesis.current.seen, {})
      assert.ok(genesis.eventHead == null)
      let current = genesis.current

      const unknown = gecSurface.encodeSphinxEnvelope(
        jsEvent(inquiryId, 0, 'BogusKind', { plugins: ['sphinx-legacy'] }),
      )
      assert.equal(unknown.ok, false)
      assert.match(unknown.error.code, /unknown-kind/)

      const envelope = encode(jsEvent(inquiryId, 0, 'plugin-set-bound', { plugins: ['sphinx-legacy'] }))
      const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(gate.ok, true)
      assert.equal(gate.duplicate, false)
      assert.equal(gate.revision, 1)
      // The gate alone advances nothing: Current moves only after a durable append.
      assert.equal(current.revision, 0)

      await appendDurable(handle, envelope)
      const advanced = fold(readSpine(handle, stream))
      assert.equal(advanced.current.revision, 1)
      assert.equal(advanced.eventHead, envelope.id)
      assert.deepEqual(eventStore.heads(handle, stream), [envelope.id])
      current = advanced.current

      // Stale expectedRevision must conflict before any write: no silent advance.
      const stale = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(stale.ok, false)
      assert.match(stale.error.code, /REVISION_CONFLICT/)

      const afterConflict = fold(readSpine(handle, stream))
      assert.equal(afterConflict.current.revision, 1)
      assert.equal(afterConflict.eventHead, envelope.id)
      assert.equal(afterConflict.semanticHash, advanced.semanticHash)
    } finally {
      eventStore.dispose(handle)
    }
  })
})
test('WHAT[epistemic-reasoning-019] restart_recovery_and_cache_loss_replay_the_same_canonical_hash', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const first = encode(jsEvent(inquiryId, 0, 'plugin-set-bound', { plugins: ['sphinx-legacy'] }))
    const second = encode(
      jsEvent(
        inquiryId,
        1,
        'observation-accepted',
        { workId: 'work_alpha', attempt: 1, observation: 'first' },
        [first.id],
      ),
    )
    const handle = eventStore.create(commonDir, 'writer-spine-restart')
    let before
    let ids
    try {
      let current = fold([]).current
      for (const [index, envelope] of [first, second].entries()) {
        const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: index })
        assert.equal(gate.ok, true, JSON.stringify(gate.error ?? null))
        await appendDurable(handle, envelope)
        current = fold(readSpine(handle, stream)).current
      }
      const log = readSpine(handle, stream)
      assert.equal(log.length, 2)
      assert.ok(log.every((entry) => /^[0-9a-f]{40}$/.test(entry.id)))
      ids = log.map((entry) => entry.id)
      before = fold(log)
      assert.equal(before.current.revision, 2)
    } finally {
      eventStore.dispose(handle)
    }

    // Process restart: a NEW handle from the same commonDir without passing
    // any events. The durable log and folded Current must come back identical.
    const reopened = eventStore.create(commonDir, 'writer-spine-restarted')
    try {
      const durableLog = readSpine(reopened, stream)
      assert.deepEqual(
        durableLog.map((entry) => entry.id),
        ids,
      )
      const recovered = fold(durableLog)
      assert.equal(recovered.current.revision, 2)
      assert.equal(recovered.eventHead, before.eventHead)
      assert.equal(recovered.semanticHash, before.semanticHash)

      // Dropping the hot Current only clears memory: the next fold reloads
      // from a fresh canonical store read with the same result.
      const dropped = fold(readSpine(reopened, stream))
      assert.deepEqual(dropped.current, recovered.current)
      assert.equal(dropped.eventHead, recovered.eventHead)
      assert.equal(dropped.semanticHash, recovered.semanticHash)
    } finally {
      eventStore.dispose(reopened)
    }
  })
})
test('WHAT[epistemic-reasoning-019] same_work_attempt_replay_is_idempotent_but_conflicting_payload_is_rejected', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const observation = { workId: 'work_alpha', attempt: 1, observation: 'first' }
    const handle = eventStore.create(commonDir, 'writer-spine-idempotent')
    try {
      let current = fold([]).current
      const envelope = encode(jsEvent(inquiryId, 0, 'observation-accepted', observation))
      const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(gate.ok, true)
      await appendDurable(handle, envelope)
      current = fold(readSpine(handle, stream)).current
      const head = eventStore.head(handle, stream)

      // Exact redelivery under the same work attempt is the same canonical
      // fact: the gate reports a duplicate and nothing is appended twice.
      const redelivered = encode(jsEvent(inquiryId, 0, 'observation-accepted', { ...observation }))
      assert.equal(redelivered.id, envelope.id)
      const replay = gecSurface.checkAppend({ current, envelope: redelivered, expectedRevision: 1 })
      assert.equal(replay.ok, true)
      assert.equal(replay.duplicate, true)
      assert.equal(replay.revision, 1)
      assert.equal(readSpine(handle, stream).length, 1)
      assert.equal(eventStore.head(handle, stream), head)

      // Same workId+attempt with a conflicting payload must be rejected, not
      // merged and not appended.
      const conflicting = encode(
        jsEvent(inquiryId, 0, 'observation-accepted', {
          workId: 'work_alpha',
          attempt: 1,
          observation: 'contradictory rewrite',
        }),
      )
      const conflict = gecSurface.checkAppend({ current, envelope: conflicting, expectedRevision: 1 })
      assert.equal(conflict.ok, false)
      assert.match(conflict.error.code, /DUPLICATE_CONFLICT/)
      assert.equal(readSpine(handle, stream).length, 1)
      assert.equal(eventStore.head(handle, stream), head)
    } finally {
      eventStore.dispose(handle)
    }
  })
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { spawn } = await import("node:child_process");
const { readFileSync } = await import("node:fs");
const { mkdtemp, rm } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { randomUUID } = await import("node:crypto");
const { fileURLToPath } = await import("node:url");
const { dirname, join } = await import("node:path");
const { relativeServerEntry } = await import("./support.mjs");

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const serverEntry = join(repoRoot, relativeServerEntry)
const pkgVersion = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')).version
const QUESTION = '花青素合成是否解释红色？'
const CANDIDATE = {
  method: 'CausalMechanism',
  question: '花青素合成及其光谱吸收是否解释红色？',
  semanticKey: 'question:anthocyanin',
  dependencyKey: 'source:pigment-study',
  expectedRootGain: 0.95,
  cost: 0.2,
}
const FINDINGS = [
  {
    semanticKey: 'finding:anthocyanin',
    text: '花青素的吸收谱与组织酸碱环境共同决定可见红色。',
    evidenceKeys: ['evidence:pigment-study'],
    provenance: ['investigation:pigment'],
  },
]
const EVIDENCE = [
  {
    semanticKey: 'evidence:pigment-study',
    proposition: '独立色素研究支持花青素机制。',
    source: { id: 'pigment-study', kind: 'document' },
    dependencyKey: 'pigment-study',
    provenance: ['document:pigment-study'],
  },
]
const SYNTHESIS = {
  text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
  findingKeys: ['finding:anthocyanin'],
  uncertainties: [],
}
function assessArgs(handle) {
  return { handle, forms: { Why: 0.8, How: 0.2 }, facets: { causal: 0.9, explanatory: 1 } }
}
function investigateArgs(handle, actionKey) {
  return { handle, actionKey, findings: FINDINGS, evidence: EVIDENCE }
}
function spawnSphinx(env = {}) {
  const proc = spawn('node', [serverEntry], {
    cwd: repoRoot,
    stdio: ['pipe', 'pipe', 'pipe'],
    env: { ...process.env, ...env },
  })

  let nextId = 1
  /** @type {Map<number, {resolve:(v:any)=>void, reject:(e:Error)=>void, timer:NodeJS.Timeout}>} */
  const pending = new Map()
  let stdoutBuf = ''
  const lines = []
  let stderrBuf = ''

  proc.stdout.setEncoding('utf8')
  proc.stdout.on('data', (chunk) => {
    stdoutBuf += chunk
    let idx
    while ((idx = stdoutBuf.indexOf('\n')) !== -1) {
      const line = stdoutBuf.slice(0, idx).replace(/\r$/, '')
      stdoutBuf = stdoutBuf.slice(idx + 1)
      if (line.length === 0) continue
      lines.push(line)
      let msg
      try {
        msg = JSON.parse(line)
      } catch {
        continue
      }
      if (msg && msg.id != null && pending.has(msg.id)) {
        const p = pending.get(msg.id)
        pending.delete(msg.id)
        clearTimeout(p.timer)
        p.resolve(msg)
      }
    }
  })

  proc.stderr.setEncoding('utf8')
  proc.stderr.on('data', (chunk) => {
    stderrBuf += chunk
  })

  proc.on('error', () => {})
  proc.on('exit', (code) => {
    for (const [, p] of pending) {
      clearTimeout(p.timer)
      p.reject(new Error(`sphinx server exited with code ${code}\nstderr:\n${stderrBuf}`))
    }
    pending.clear()
  })

  function write(msg) {
    try {
      proc.stdin.write(JSON.stringify(msg) + '\n')
    } catch {
      /* process may already be dead */
    }
  }

  function call(method, params, timeoutMs = 15000) {
    const id = nextId++
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id)
        reject(new Error(`timeout waiting for ${method} id=${id}\nstderr:\n${stderrBuf}`))
      }, timeoutMs)
      pending.set(id, { resolve, reject, timer })
      write({ jsonrpc: '2.0', id, method, params: params ?? {} })
    })
  }

  function notify(method, params) {
    write({ jsonrpc: '2.0', method, params: params ?? {} })
  }

  async function initialize() {
    const res = await call('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'blackbox-test', version: '0.0.0' },
    })
    notify('notifications/initialized', {})
    return res
  }

  function tool(name, args, timeoutMs) {
    return call('tools/call', { name, arguments: args }, timeoutMs)
  }

  function close() {
    pending.clear()
    try {
      proc.kill('SIGKILL')
    } catch {
      /* already dead */
    }
  }

  return {
    proc,
    call,
    notify,
    initialize,
    tool,
    close,
    get lines() {
      return lines
    },
    get stderr() {
      return stderrBuf
    },
  }
}
async function driveToAnswered(s) {
  const startRes = await s.tool('start', { question: QUESTION })
  const handle = startRes.result.structuredContent.handle
  let nextTool = startRes.result.structuredContent.nextTool
  let actionKey = null
  let investigated = false

  while (nextTool != null) {
    let res
    if (nextTool === 'assess') {
      res = await s.tool('assess', assessArgs(handle))
    } else if (nextTool === 'propose') {
      const items = investigated ? [] : [CANDIDATE]
      res = await s.tool('propose', { handle, items })
    } else if (nextTool === 'investigate') {
      res = await s.tool('investigate', investigateArgs(handle, actionKey))
      investigated = true
    } else if (nextTool === 'synthesize') {
      res = await s.tool('synthesize', { handle, ...SYNTHESIS })
    } else {
      throw new Error(`unexpected nextTool: ${nextTool}`)
    }
    const sc = res.result.structuredContent
    nextTool = sc.nextTool
    if (sc.request?.action?.id) actionKey = sc.request.action.id
    if (sc.status === 'answered') break
  }
  return handle
}

test('WHAT[epistemic-reasoning-019] generic_restart_recovers_revision_results_and_conflict', { timeout: 30000 }, async () => {
  // Generic sphinx_inquiry_* transitions are durable facts: killing the
  // first process must not lose the revision, the accepted results, or the
  // expectedRevision gate. The second server resumes at the same revision.
  const commonDir = await mkdtemp(join(tmpdir(), 'sphinx-generic-'))
  try {
    const env = { SPHINX_COMMON_DIR: commonDir }
    const s1 = spawnSphinx(env)
    let inquiryId
    try {
      await s1.initialize()
      const startRes = await s1.tool('sphinx_inquiry_start', { question: 'durable generic probe' })
      inquiryId = startRes.result.structuredContent.inquiryId
      assert.match(inquiryId, /^iq_/)
      const submitRes = await s1.tool('sphinx_work_submit', {
        inquiryId,
        expectedRevision: 0,
        results: [{ workId: 'work_restart_1', output: 'first' }],
      })
      assert.equal(submitRes.result.structuredContent.revision, 1)
    } finally {
      s1.close()
    }

    const s2 = spawnSphinx(env)
    try {
      await s2.initialize()
      const status = await s2.tool('sphinx_inquiry_status', { inquiryId })
      assert.equal(status.result.isError, undefined)
      assert.equal(status.result.structuredContent.inquiryId, inquiryId)
      assert.equal(status.result.structuredContent.revision, 1)
      assert.equal(status.result.structuredContent.status, 'active')
      const exported = await s2.tool('sphinx_inquiry_export', { inquiryId })
      assert.equal(exported.result.structuredContent.revision, 1)
      assert.deepEqual(exported.result.structuredContent.results, [{ workId: 'work_restart_1', output: 'first' }])
      const stale = await s2.tool('sphinx_work_submit', { inquiryId, expectedRevision: 0, results: [] })
      assert.equal(stale.result.isError, true)
      assert.match(JSON.stringify(stale.result), /REVISION_CONFLICT/)
      const resumed = await s2.tool('sphinx_work_submit', {
        inquiryId,
        expectedRevision: 1,
        results: [{ workId: 'work_restart_2' }],
      })
      assert.equal(resumed.result.structuredContent.revision, 2)
      assert.equal(resumed.result.structuredContent.accepted, 1)
    } finally {
      s2.close()
    }
  } finally {
    await rm(commonDir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const SEED = 0x51eca129
const WAVES = 12
const SUBJECTS = Array.from({ length: 16 }, (_, index) => `witness-${String(index).padStart(2, '0')}`)
const TREATMENTS = ['wording-a', 'wording-b']
const CANDIDATES = ['c1', 'c2', 'c3']
const PLANTED_EFFECT = 0.15
const xorshift = (initial) => {
  let state = initial >>> 0 || 1
  return () => {
    state ^= state << 13
    state >>>= 0
    state ^= state >>> 17
    state ^= state << 5
    state >>>= 0
    return state / 4294967296
  }
}
const waveInput = (wave) => ({
  rootSnapshot: `snap-soak-${String(wave).padStart(2, '0')}`,
  seed: (SEED + wave * 7919) >>> 0,
  subjects: [...SUBJECTS],
  treatments: [...TREATMENTS],
  candidates: [...CANDIDATES],
})
const waveEvents = (wave, assignment) => {
  const inquiry = `iq_soak${String(wave).padStart(4, '0')}`
  const branch = `branch_soak${String(wave).padStart(4, '0')}`
  const work = `work_soak${String(wave).padStart(4, '0')}`
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  return [
    {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'soak probe',
      pluginLock: lock,
      budget: { compute: 100, budget: 100 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    },
    {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: work, branch, attempt: 1 },
    },
    {
      type: 'ObservationAccepted',
      inquiry,
      revision: 2,
      parent: 'ev1',
      observation: {
        rootSnapshotHash: `snap-soak-${String(wave).padStart(2, '0')}`,
        branch,
        work,
        attempt: 1,
        pluginLock: lock,
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: `prompt-soak-${wave}`,
        questionId: `q-soak-${wave}`,
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: [...CANDIDATES], labels: ['A', 'B', 'C'], order: [0, 1, 2] },
        treatment: assignment,
        blindToken: `blind01soakwave${String(wave).padStart(4, '0')}`,
        seed: `seed-soak-${wave}`,
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 16 },
        usage: { promptTokens: 5, completionTokens: 3 },
        payload: { wave },
      },
    },
    { type: 'BudgetDebited', inquiry, revision: 3, parent: 'ev2', debit: { compute: 7, budget: 7 } },
  ]
}

test('WHAT[epistemic-reasoning-019] soak_pure_spine_gates_conflicts_and_refold_recovers_without_io', async () => {
  for (let wave = 0; wave < WAVES; wave += 1) {
    const tag = String(wave).padStart(4, '0')
    const inquiryId = `iq_soakpure${tag}`
    const genesis = gecSurface.sphinxCurrent({ envelopes: [] })
    assert.equal(genesis.ok, true)
    assert.equal(genesis.current.revision, 0)
    const bound = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 0,
      kind: 'plugin-set-bound',
      parents: [],
      payload: { plugins: ['sphinx-legacy'] },
    })
    assert.equal(bound.ok, true)
    const gate0 = gecSurface.checkAppend({ current: genesis.current, envelope: bound.envelope, expectedRevision: 0 })
    assert.equal(gate0.ok, true)
    assert.equal(gate0.duplicate, false)
    assert.equal(gate0.revision, 1)
    const afterBound = gecSurface.sphinxCurrent({ envelopes: [bound.envelope] })
    assert.equal(afterBound.current.revision, 1)
    const observed = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 1,
      kind: 'observation-accepted',
      parents: [bound.envelope.id],
      payload: { workId: `work_soakpure${tag}`, attempt: 1, observation: `first-${tag}` },
    })
    assert.equal(observed.ok, true)
    const gate1 = gecSurface.checkAppend({ current: afterBound.current, envelope: observed.envelope, expectedRevision: 1 })
    assert.equal(gate1.ok, true)
    assert.equal(gate1.revision, 2)
    const spine = [bound.envelope, observed.envelope]
    const folded = gecSurface.sphinxCurrent({ envelopes: spine })
    assert.equal(folded.current.revision, 2)
    assert.equal(folded.eventHead, observed.envelope.id)
    const redelivered = gecSurface.checkAppend({ current: folded.current, envelope: observed.envelope, expectedRevision: 2 })
    assert.equal(redelivered.ok, true)
    assert.equal(redelivered.duplicate, true, `wave ${wave} identical redelivery must report a duplicate`)
    assert.equal(redelivered.revision, 2)
    const conflicting = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 1,
      kind: 'observation-accepted',
      parents: [bound.envelope.id],
      payload: { workId: `work_soakpure${tag}`, attempt: 1, observation: `contradictory-rewrite-${tag}` },
    })
    assert.equal(conflicting.ok, true)
    const conflict = gecSurface.checkAppend({ current: folded.current, envelope: conflicting.envelope, expectedRevision: 2 })
    assert.equal(conflict.ok, false)
    assert.match(conflict.error.code, /DUPLICATE_CONFLICT/)
    const stale = gecSurface.checkAppend({ current: folded.current, envelope: conflicting.envelope, expectedRevision: 0 })
    assert.equal(stale.ok, false)
    assert.match(stale.error.code, /REVISION_CONFLICT/)
    const refolded = gecSurface.sphinxCurrent({ envelopes: spine })
    assert.deepEqual(refolded.current, folded.current, `wave ${wave} dropping Current must refold identically`)
    assert.equal(refolded.eventHead, folded.eventHead)
    assert.equal(refolded.semanticHash, folded.semanticHash)

    const events = waveEvents(wave, 'wording-a')
    const hashed = gecSurface.semanticHash({ events })
    const replayed = gecSurface.replay({ events })
    assert.equal(replayed.ok, true)
    const bundle = gecSurface.exportFromEvents({ events })
    assert.equal(bundle.error, undefined)
    assert.equal(replayed.stateHash, hashed.hash, `wave ${wave} replay must equal the canonical hash`)
    assert.equal(bundle.semanticHash, hashed.hash, `wave ${wave} export must agree with replay on one hash`)
  }
})
}
