// tests/unit/domain.meta.test.mjs — the facade's own contract.
//
// VERIFY-008 states that mjs tests may only enter through the contract surface
// and that Fable's output shape is confined to domain.mjs. That makes the
// facade itself load-bearing: if it hands back a subtly wrong value, every
// test built on it passes while describing an implementation that is broken.
//
// Three hazards below are not hypothetical. Each was observed while probing
// the Fable output, and each fails SILENTLY — no exception, no type error,
// just a wrong answer. They are locked here so a facade regression is loud.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as domain from './support/domain.mjs'

const {
  utcOffset,
  clockAt,
  deadline,
  toList,
  listItems,
  fold,
  journal,
  envelope,
  stream,
  fact,
  agentFact,
  asFact,
  caseOf,
  payloadOf,
  cursor,
  sessionId,
  logicalRunId,
  authorityRoot,
  providerRun,
  idValue,
  agentFactCaseNames,
} = domain

// ── hazard 1: DateTimeOffset without an offset ──────────────────────────────
// Fable's compareDates branches on `"offset" in x`. A bare `new Date(iso)` has
// no offset, so it takes the DateTime branch and adds the LOCAL timezone
// offset. Under a non-UTC TZ every comparison shifts, and Deadline.isExpired
// reports true for a deadline that has not passed.

test('utcOffset produces a value carrying an explicit offset', () => {
  const value = utcOffset('2026-01-01T00:00:00Z')
  assert.ok('offset' in value, 'facade clock values must carry an offset property')
  assert.equal(value.offset, 0, 'facade clock values must be UTC')
  assert.equal(value.getTime(), Date.parse('2026-01-01T00:00:00Z'))
})

test('a bare Date lacks the offset that F# comparison depends on', () => {
  // Documents WHY utcOffset exists. If this ever starts passing, Fable changed
  // its date representation and the facade's rationale must be re-checked.
  assert.equal('offset' in new Date('2026-01-01T00:00:00Z'), false)
})

test('deadline comparisons are correct through the facade clock', () => {
  const dl = deadline.ofBudget('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:02Z'), dl), 3000)
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:02Z'), dl), false)

  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:05Z'), dl), 0)
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:05Z'), dl), true)

  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:06Z'), dl), true)
})

test('deadline verdict does not depend on the ambient timezone', () => {
  // The naive-Date bug is invisible under TZ=UTC and wrong everywhere else.
  // The facade must give the same answer regardless of process TZ.
  const original = process.env.TZ
  const dl = deadline.ofBudget('2026-01-01T00:00:00Z', 5000)
  const before = clockAt('2026-01-01T00:00:02Z')

  try {
    for (const zone of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
      process.env.TZ = zone
      assert.equal(deadline.isExpired(before, dl), false, `isExpired must stay false under TZ=${zone}`)
      assert.equal(deadline.remainingMs(before, dl), 3000, `remaining must stay 3000ms under TZ=${zone}`)
    }
  } finally {
    if (original === undefined) delete process.env.TZ
    else process.env.TZ = original
  }
})

// ── hazard 2: a JS array reports itself as an empty F# list ─────────────────
// FSharpList__get_IsEmpty tests `xs.tail == null`. A JS array has no `tail`,
// so List.fold immediately returns the initial state: the projection stays
// empty and every assertion about it describes nothing at all.

test('a raw array folds to nothing, which is why toList exists', async () => {
  const List = await import(`${domain.introspect.fableLibraryDir}/List.js`)
  assert.equal(
    List.fold((accumulator, item) => accumulator + item, 0, [1, 2, 3]),
    0,
    'a raw array must fold to the seed — documents the silent-empty hazard',
  )
  assert.equal(List.fold((accumulator, item) => accumulator + item, 0, toList([1, 2, 3])), 6)
})

test('toList round-trips through listItems', () => {
  assert.deepEqual(listItems(toList([1, 2, 3])), [1, 2, 3])
  assert.deepEqual(listItems(toList([])), [])
})

test('fold.apply converts arrays instead of silently folding nothing', () => {
  const session = sessionId('ses_meta')
  const accepted = envelope({
    seq: 1,
    stream: stream.session(session),
    fact: fact('AuthorityRootAccepted', {
      SessionId: session,
      LogicalRunId: logicalRunId('run-meta'),
      AuthorityRootUserMessageId: authorityRoot('msg_root'),
      AuthorityKind: 'HumanRoot',
      SelectedAgent: 'fast-coder',
      PeerAgent: 'deep-coder',
      CanonicalRole: 'coder',
      SelectedTier: 'fast',
    }),
  })

  const projection = fold.apply(fold.empty, [accepted])
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))
  assert.deepEqual(Object.keys(fold.sessions(projection.value)), ['ses_meta'])
})

test('fold.apply rejects a single envelope passed where a sequence was meant', () => {
  const session = sessionId('ses_meta')
  const single = envelope({
    seq: 1,
    stream: stream.session(session),
    fact: fact('CompanionBloggerClosed', { SessionId: session }),
  })

  assert.throws(() => fold.apply(fold.empty, single), /envelope sequence/)
})

// ── hazard 3: union tag ordinals are positional ─────────────────────────────
// Constructing a fact by ordinal silently builds a DIFFERENT fact when a case
// is inserted earlier in the union. Resolving name → ordinal from cases()
// converts that into a named failure.

test('facts are built by case name, and an unknown name fails loudly', () => {
  const session = sessionId('ses_meta')
  const built = agentFact('FallbackCursorAdvanced', {
    SessionId: session,
    LogicalRunId: 'run-meta',
    AuthorityRootUserMessageId: 'msg_root',
    Reason: 'provider_error',
    AssistantMessageId: 'msg_a1',
    ProviderAttempt: '1',
  })

  assert.equal(caseOf(built), 'FallbackCursorAdvanced')
  assert.throws(() => agentFact('FallbackCursorAdvancedTypo', {}), /has no case/)
})

test('asFact wraps an AgentFact as the top-level Agent case', () => {
  const session = sessionId('ses_meta')
  const wrapped = asFact(agentFact('CompanionBloggerClosed', { SessionId: session }))

  assert.equal(caseOf(wrapped), 'Agent')
  assert.equal(caseOf(payloadOf(wrapped)), 'CompanionBloggerClosed')
})

test('caseOf refuses a non-union value instead of returning undefined', () => {
  assert.throws(() => caseOf({ tag: 0 }), /expects an F# union/)
  assert.equal(caseOf(undefined), undefined)
})

test('stream cases resolve by name', () => {
  assert.equal(caseOf(stream.workspace()), 'Workspace')
  assert.equal(caseOf(stream.session(sessionId('ses_meta'))), 'Session')
})

// ── the persisted shape is the contract surface ─────────────────────────────

test('an envelope survives NDJSON round trip and still folds', () => {
  const session = sessionId('ses_rt')

  // FALLBACK-001: the cursor is created by the Authority Root, so an advance with
  // no root is rejected. The round trip therefore needs both envelopes.
  const root = envelope({
    seq: 6,
    observedAt: '2026-03-04T05:06:06Z',
    stream: stream.session(session),
    fact: fact('AuthorityRootAccepted', {
      SessionId: session,
      LogicalRunId: logicalRunId('run-rt'),
      AuthorityRootUserMessageId: authorityRoot('msg_root'),
      AuthorityKind: 'HumanRoot',
      SelectedAgent: 'fast-coder',
      PeerAgent: 'deep-coder',
      CanonicalRole: 'coder',
      SelectedTier: 'fast',
    }),
  })

  const advanced = envelope({
    seq: 7,
    observedAt: '2026-03-04T05:06:07Z',
    stream: stream.session(session),
    run: 'msg_a1',
    fact: fact('FallbackCursorAdvanced', {
      SessionId: session,
      LogicalRunId: logicalRunId('run-rt'),
      AuthorityRootUserMessageId: authorityRoot('msg_root'),
      ProviderRun: providerRun('msg_a1'),
      PreviousOffset: 0,
      NextOffset: 1,
      ConsecutiveFailureCount: 1,
      Reason: 'provider_error',
    }),
  })

  const line = journal.serialize(advanced)
  assert.equal(line.includes('\n'), false, 'one envelope must serialise to one NDJSON line')

  const decoded = journal.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : String(decoded.error))
  assert.equal(idValue.session(payloadOf(payloadOf(decoded.value.Fact)).SessionId), 'ses_rt')
  assert.equal(idValue.localSeq(decoded.value.LocalSeq), 7n)

  const projection = fold.replay([root, advanced])
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))

  const sessions = fold.sessions(projection.value)
  assert.deepEqual(Object.keys(sessions), ['ses_rt'])

  // FALLBACK-007: the advance is validated, not absorbed — offset and count both
  // move, and asserting the whole cursor means a dropped field cannot pass.
  assert.deepEqual(
    { Offset: sessions.ses_rt.Fallback.Cursor.Offset, ConsecutiveFailureCount: sessions.ses_rt.Fallback.Cursor.ConsecutiveFailureCount },
    { Offset: 1, ConsecutiveFailureCount: 1 },
  )
})

test('journal.deserialize reports a decode failure as data, not an exception', () => {
  const result = journal.deserialize('{"not":"an envelope"}')
  assert.equal(result.ok, false)
  assert.equal(typeof result.error, 'string')
})

test('pre-0.5.0 journals are refused rather than guessed (PERSIST-005)', () => {
  // The markers are the pre-0.5.0 projection fields that a modulo-4 cursor
  // migration would have to invent values for. Asserting the whole set means
  // dropping one is a failure here rather than a silent acceptance of an old
  // journal at startup.
  const legacyMarkers = [
    'FailuresOnCurrentSide',
    'IsDead',
    'TotalFailures',
    'BaseModelID',
    'BaseProviderID',
    'EffectiveModelID',
    'EffectiveProviderID',
  ]

  for (const marker of legacyMarkers) {
    const line = `{"Fact":["Agent",["FallbackCursorAdvanced",{"${marker}":1}]]}`
    assert.equal(journal.containsLegacyFallbackFields(line), true, `'${marker}' must be refused`)

    const result = journal.deserialize(line)
    assert.equal(result.ok, false, `'${marker}' must not decode`)
    assert.equal(result.error, journal.pre050MigrationMessage)

    const factResult = journal.deserializeFact(line)
    assert.equal(factResult.ok, false)
    assert.equal(factResult.error, journal.pre050MigrationMessage)
  }
})

test('a current-schema journal line is not mistaken for a legacy one', () => {
  const session = sessionId('ses_current')
  const line = journal.serialize(
    envelope({
      seq: 1,
      stream: stream.session(session),
      run: 'msg_a1',
      fact: fact('FallbackCursorAdvanced', {
        SessionId: session,
        LogicalRunId: logicalRunId('run-current'),
        AuthorityRootUserMessageId: authorityRoot('msg_root'),
        ProviderRun: providerRun('msg_a1'),
        PreviousOffset: 0,
        NextOffset: 1,
        ConsecutiveFailureCount: 1,
        Reason: 'provider_error',
      }),
    }),
  )

  assert.equal(journal.containsLegacyFallbackFields(line), false)
  assert.equal(journal.deserialize(line).ok, true)
})

// ── pure domain values reachable without any Host mock ──────────────────────

test('fallback cursor exposes offsets as case names, not ordinals', () => {
  assert.deepEqual([0, 1, 2, 3].map(cursor.side), ['SideA', 'SideA', 'SideB', 'SideB'])
  assert.deepEqual(cursor.sideSequence(6), ['SideA', 'SideA', 'SideB', 'SideB', 'SideA', 'SideA'])
})

test('cursor.effectiveAgent accepts a plain object as the F# record', () => {
  const pair = { SelectedAgent: 'fast-coder', PeerAgent: 'deep-coder' }
  const agentAt = (offset) => cursor.effectiveAgent(pair, cursor.atOffset(offset))

  assert.deepEqual([0, 1, 2, 3].map(agentAt), ['fast-coder', 'fast-coder', 'deep-coder', 'deep-coder'])
})

// ── the facade must stay wired to a real build ──────────────────────────────

test('the facade resolves exactly one versioned fable-library directory', () => {
  assert.match(domain.introspect.fableLibraryDir, /fable-library-js\.\d+\.\d+\.\d+$/)
})

test('the AgentFact union is non-empty, so case resolution is meaningful', () => {
  const names = agentFactCaseNames()
  assert.ok(names.length > 0)
  assert.equal(new Set(names).size, names.length, 'case names must be unique')
})

// ── hazard 4: Fable escapes a reserved emitted name with `$` ─────────────────
//
// `ReviewChallenge.Text` emits as `Text$`, so reading `.Text` off the module gave
// `undefined`. Nothing threw: the facade exported `reviewChallenge.text ===
// undefined`, and a test asserting the challenge sentence would have compared
// undefined to undefined and passed.
//
// `member()` now tries the `$` spelling too. This sweep is what found it and is
// what keeps the next one from shipping — it needs no knowledge of which members
// exist, so a newly bound member is covered the moment it is added.

test('no facade member is undefined', () => {
  const undefinedMembers = []

  for (const [name, value] of Object.entries(domain)) {
    if (value === undefined) {
      undefinedMembers.push(name)
      continue
    }

    // Namespace objects only. A function's own properties are not contract
    // surface, and `introspect` is the facade describing itself.
    const isNamespace = value !== null && typeof value === 'object' && !Array.isArray(value)
    if (!isNamespace) continue

    for (const [key, member] of Object.entries(value)) {
      if (member === undefined) undefinedMembers.push(`${name}.${key}`)
    }
  }

  assert.deepEqual(
    undefinedMembers,
    [],
    'an undefined facade member reads as undefined in every test that uses it, and comparing undefined to undefined passes',
  )
})

test('member resolution prefers a real export over the escaped spelling', () => {
  // The `$` spellings are tried LAST. If they were tried first, a module
  // exporting both `foo` and `foo$` — Fable does this when a value and a type
  // share a name — would bind the wrong one.
  assert.equal(domain.reviewChallenge.textVersion, 1)
  assert.match(domain.reviewChallenge.text, /^Nope, let's re-evaluate: /)

  // And the digest is derived from `ReviewChallenge.Prompt` (the instruction
  // comment bytes), not from the bare `Text`.
  const digest = idValue.sealDigest(domain.reviewChallenge.contentDigest((input) => `H(${input})`))
  assert.equal(digest, `H(# ${domain.reviewChallenge.text}\n)`)
})
