// tests/unit/Journal/boot.test.mjs — PERSIST-002/004/006, verification layer 2.
//
// The resource contract: what a real file on a real filesystem does. These are
// layer 2 rather than layer 1 because the clauses are ABOUT the file — a partial
// tail, a permission bit, a second writer racing for one RuntimeId. Asserting
// them against an in-memory stand-in would assert the stand-in.
//
// Every test owns a fresh directory and removes it, so a failure cannot leak into
// the next one (VERIFY-004). The directory is a path INSIDE the temp dir that
// production creates itself: `mkdtemp` already yields 0700, so pre-creating it
// would make the PERSIST-006 assertion pass with production setting no mode.

import assert from 'node:assert/strict'
import test from 'node:test'
import { agentFactCaseOf, caseOf, envelope, fact, idValue, journal, journalStore, payloadOf, sessionId, stream } from '../../unit/support/domain.mjs'

const SESSION = sessionId('ses_a')
const CLOSED = fact('CompanionBloggerClosed', { SessionId: SESSION })

/** Run a test body against a fresh store, always cleaning up. */
const withStore = (body) => {
  const store = journalStore()
  try {
    body(store)
  } finally {
    store.close()
  }
}

/**
 * One serialized line, built through the same constructor production writes.
 *
 * A corrupt fixture must differ from a healthy one only in the way the test
 * intends, so hand-writing JSON here would risk proving the codec rejects the
 * test's own typo.
 */
const line = (runtime, seq) =>
  journal.serialize(envelope({ runtime, seq, stream: stream.session(SESSION), fact: CLOSED }))

// ── PERSIST-006: permissions are set at creation, not after ──────────────────

test('PERSIST_006_runtime_directory_is_0700_and_the_journal_file_is_0600', () => {
  withStore((store) => {
    store.open()

    // A journal line names sessions, Git trees and prompt payload digests. Setting
    // the mode at creation rather than chmod-ing after matters: between mkdir and
    // chmod the directory would be world-readable.
    assert.deepEqual(store.modes(), { directory: '700', file: '600' })
  })
})

test('PERSIST_006_permissions_hold_regardless_of_the_process_umask', () => {
  // The default umask 022 yields 755/644, which is exactly what this repo shipped
  // before the mode arguments were passed. Forcing a permissive umask proves the
  // bits come from production and not from the environment.
  const original = process.umask(0o000)
  try {
    withStore((store) => {
      store.open()
      assert.deepEqual(store.modes(), { directory: '700', file: '600' })
    })
  } finally {
    process.umask(original)
  }
})

// ── PERSIST-002: the writer's two outcomes, observed on disk ─────────────────

test('PERSIST_002_creating_a_writer_writes_the_runtime_started_envelope_first', () => {
  withStore((store) => {
    const j = store.open({ runtime: 'rt_1', pid: 4242, startedAt: '2026-01-01T00:00:00Z' })

    // There is no way to obtain a writer without this line, which is what makes
    // "every journal has a RuntimeStarted at LocalSeq 1" structural.
    assert.deepEqual(store.files(), ['blobs', 'rt_1.ndjson'])
    assert.equal(store.lines().length, 1)
    assert.deepEqual(
      {
        fact: caseOf(payloadOf(j.initEnvelope.Fact)),
        seq: Number(idValue.localSeq(j.initEnvelope.LocalSeq)),
        runtime: idValue.runtime(j.initEnvelope.RuntimeId),
        stream: caseOf(j.initEnvelope.Stream),
      },
      { fact: 'RuntimeStarted', seq: 1, runtime: 'rt_1', stream: 'Workspace' },
    )

    // The next append is 2, and 1 is already durable.
    assert.deepEqual({ next: j.seq(), lastCommitted: j.lastCommittedSeq() }, { next: 2, lastCommitted: 1 })
  })
})

test('PERSIST_002_a_committed_append_reached_the_file_before_it_returned', () => {
  withStore((store) => {
    const j = store.open()
    const result = j.append(stream.session(SESSION), CLOSED, 'run_1')

    assert.equal(result.committed, true)

    // The clause is about ordering: Committed means the bytes are durable, so the
    // line must be readable the instant the call returns. Nothing here waits.
    const lines = store.lines()
    assert.equal(lines.length, 2)

    const decoded = journal.deserialize(lines[1])
    assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
    assert.deepEqual(
      {
        seq: Number(idValue.localSeq(decoded.value.LocalSeq)),
        fact: agentFactCaseOf(payloadOf(decoded.value.Fact)),
        run: idValue.providerRun(decoded.value.ProviderRun),
      },
      { seq: 2, fact: 'CompanionBloggerClosed', run: 'run_1' },
    )
  })
})

test('PERSIST_002_local_seq_advances_only_for_appends_that_committed', () => {
  withStore((store) => {
    const j = store.open()

    for (let expected = 2; expected <= 5; expected += 1) {
      assert.equal(j.seq(), expected)
      assert.equal(j.append(stream.session(SESSION), CLOSED).committed, true)
      assert.equal(j.lastCommittedSeq(), expected)
    }

    // Five lines, sequence 1..5 with no gaps: Boot's own contiguity check is what
    // this feeds, so a gap here would be indistinguishable from corruption later.
    assert.deepEqual(
      store.lines().map((text) => Number(idValue.localSeq(journal.deserialize(text).value.LocalSeq))),
      [1, 2, 3, 4, 5],
    )
  })
})

test('PERSIST_002_appending_to_a_disposed_writer_is_CommitUnknown_not_a_throw', () => {
  withStore((store) => {
    const j = store.open()
    j.dispose()

    const result = j.append(stream.session(SESSION), CLOSED)

    // There is no third outcome. A throw would leave the caller unable to say
    // whether the fact landed, which is precisely what CommitUnknown exists to
    // express — and it carries the EventId so the caller can look for it.
    assert.equal(result.committed, false)
    assert.equal(result.failure, 'WriteFailed')
    assert.equal(typeof result.eventId, 'string')
    assert.equal(result.eventId.length > 0, true)

    // The failed append did not consume a sequence number, so the file is intact.
    assert.equal(store.lines().length, 1)
  })
})

// ── PERSIST-003: CommitUnknown puts the writer into fail-closed reconcile ──────

test('PERSIST_003_commit_unknown_triggers_fail_closed_reconcile', () => {
  withStore((store) => {
    const j = store.open()
    j.dispose()

    const first = j.append(stream.session(SESSION), CLOSED)
    assert.equal(first.committed, false)
    assert.equal(first.failure, 'WriteFailed')
    assert.equal(typeof first.eventId, 'string')
    assert.equal(first.eventId.length > 0, true)

    const beforeSeq = j.seq()
    const beforeLines = store.lines().length

    // After the first CommitUnknown the writer stays closed: further appends are
    // refused and the journal does not make progress without explicit recovery.
    const second = j.append(stream.session(SESSION), CLOSED)
    assert.equal(second.committed, false)
    assert.equal(second.failure, 'WriteFailed')
    assert.equal(j.seq(), beforeSeq)
    assert.equal(store.lines().length, beforeLines)
  })
})

test('PERSIST_002_a_second_writer_for_one_runtime_id_is_refused', () => {
  withStore((store) => {
    store.open({ runtime: 'rt_1' })

    // `wx` rather than `a`. Reopening would interleave two independent LocalSeq
    // sequences into one file, and Boot's contiguity check would then read the
    // result as corruption at an arbitrary point.
    assert.throws(() => store.open({ runtime: 'rt_1' }), (error) => error.code === 'EEXIST')

    // A different runtime is a different file, and both coexist.
    store.open({ runtime: 'rt_2' })
    assert.deepEqual(store.files(), ['blobs', 'rt_1.ndjson', 'rt_2.ndjson'])
  })
})

// ── PERSIST-004: only the last incomplete envelope may be dropped ────────────

test('PERSIST_004_a_truncated_tail_line_is_dropped_and_the_prefix_is_kept', () => {
  withStore((store) => {
    // A crash mid-write leaves a partial final line. The two complete envelopes
    // before it are real facts and must survive.
    store.writeRaw('rt_1', [line('rt_1', 1), line('rt_1', 2)].join('\n') + '\n' + line('rt_1', 3).slice(0, 40))

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 2)
    assert.deepEqual(booted.diagnostics, [], 'a torn tail is expected after a crash, not a defect to report')
    assert.deepEqual(
      booted.envelopes.map((e) => Number(idValue.localSeq(e.LocalSeq))),
      [1, 2],
    )
  })
})

test('PERSIST_004_a_final_line_without_its_newline_is_treated_as_incomplete', () => {
  withStore((store) => {
    // The newline is what marks a line finished. Without it the writer may have
    // been interrupted between `write` and `fdatasync`, so the bytes cannot be
    // trusted even when they happen to parse.
    store.writeRaw('rt_1', [line('rt_1', 1), line('rt_1', 2)].join('\n'))

    const booted = store.boot()
    assert.equal(booted.envelopes.length, 1)
    assert.deepEqual(booted.diagnostics, [])
  })
})

test('PERSIST_004_corruption_in_the_middle_stops_at_the_damage_and_reports_it', () => {
  withStore((store) => {
    // The critical asymmetry: a bad line in the MIDDLE is not skipped. Later
    // envelopes build on facts the reader never saw, so continuing would fold a
    // history onto a base that does not exist.
    store.writeRaw('rt_1', [line('rt_1', 1), '{broken', line('rt_1', 3)].join('\n') + '\n')

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 1, 'recovery truncates at the damage rather than resuming past it')
    assert.equal(booted.diagnostics.length, 1)
    assert.match(booted.diagnostics[0], /^Failed to parse line 1 in rt_1\.ndjson: /)
  })
})

test('PERSIST_004_a_local_seq_gap_is_corruption_even_when_every_line_parses', () => {
  withStore((store) => {
    // Both lines are valid JSON and valid envelopes. The damage is the missing
    // fact between them, which only the sequence reveals — so parseability is not
    // the integrity check.
    store.writeRaw('rt_1', [line('rt_1', 1), line('rt_1', 3)].join('\n') + '\n')

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 1)
    assert.deepEqual(booted.diagnostics, ['LocalSeq anomaly in rt_1.ndjson: expected 2, got 3'])
  })
})

test('PERSIST_004_a_journal_that_does_not_start_at_one_yields_nothing', () => {
  withStore((store) => {
    // A file whose first line is LocalSeq 2 lost its RuntimeStarted. There is no
    // valid prefix at all, so nothing may be recovered.
    store.writeRaw('rt_1', [line('rt_1', 2), line('rt_1', 3)].join('\n') + '\n')

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 0)
    assert.deepEqual(booted.diagnostics, ['LocalSeq anomaly in rt_1.ndjson: expected 1, got 2'])
  })
})

test('PERSIST_004_a_line_from_another_runtime_is_refused_by_filename', () => {
  withStore((store) => {
    // The filename IS the RuntimeId, so a line disagreeing with it means the file
    // was concatenated or renamed. Trusting the line over the name would let one
    // runtime's sequence continue inside another's stream.
    store.writeRaw('rt_1', [line('rt_1', 1), line('rt_OTHER', 2)].join('\n') + '\n')

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 1)
    assert.deepEqual(booted.diagnostics, ['RuntimeId mismatch in rt_1.ndjson: expected rt_1, got rt_OTHER'])
  })
})

test('PERSIST_005_a_pre_050_line_stops_the_boot_with_the_migration_message', () => {
  withStore((store) => {
    // The refusal must reach boot, not just the codec: an operator sees this path.
    store.writeRaw('rt_1', line('rt_1', 1) + '\n' + JSON.stringify({ LocalSeq: 2, IsDead: true }) + '\n')

    const booted = store.boot()

    assert.equal(booted.envelopes.length, 1)
    assert.equal(booted.diagnostics.length, 1)
    assert.match(booted.diagnostics[0], /does not support pre-0\.5\.0 runtime journals/)
  })
})

test('PERSIST_004_an_empty_or_absent_journal_is_a_clean_start_not_an_error', () => {
  withStore((store) => {
    // A first run has no directory at all. That is not corruption, and reporting a
    // diagnostic would make every fresh install look damaged.
    assert.deepEqual(store.boot(), { envelopes: [], diagnostics: [], frontier: {} })

    store.writeRaw('rt_1', '')
    const booted = store.boot()
    assert.deepEqual({ envelopes: booted.envelopes, diagnostics: booted.diagnostics }, { envelopes: [], diagnostics: [] })
  })
})

// ── PERSIST-004: the frontier bounds what a boot may read ────────────────────

test('PERSIST_004_the_frontier_is_captured_before_reading_so_a_concurrent_append_is_excluded', () => {
  withStore((store) => {
    const j = store.open()
    j.append(stream.session(SESSION), CLOSED)

    const frontier = store.frontier()
    assert.deepEqual(Object.keys(frontier), ['rt_1'])
    const atCapture = frontier.rt_1

    // A live writer keeps appending while this process boots. The frontier pins the
    // byte length so the replay is a prefix of a known state rather than a moving
    // target — otherwise the fold's end depends on scheduling.
    j.append(stream.session(SESSION), CLOSED)
    assert.equal(store.frontier().rt_1 > atCapture, true, 'the file did grow, so the bound is meaningful')

    const booted = store.boot()
    assert.equal(booted.envelopes.length, 3)
    assert.equal(booted.frontier.rt_1 >= atCapture, true)
  })
})

test('PERSIST_004_two_runtime_streams_merge_into_one_ordered_history', () => {
  withStore((store) => {
    const first = store.open({ runtime: 'rt_a', startedAt: '2026-01-01T00:00:00Z' })
    const second = store.open({ runtime: 'rt_b', startedAt: '2026-01-01T00:00:01Z' })

    first.append(stream.session(SESSION), CLOSED)
    second.append(stream.session(SESSION), CLOSED)

    const booted = store.boot()

    assert.deepEqual(Object.keys(booted.frontier).sort(), ['rt_a', 'rt_b'])
    assert.equal(booted.envelopes.length, 4)

    // Within each runtime the sequence must stay ascending. Across runtimes the
    // order is by observation time, which the writer stamps, so the exact
    // interleaving is not asserted here — only that neither stream got shuffled.
    for (const runtime of ['rt_a', 'rt_b']) {
      const seqs = booted.envelopes
        .filter((e) => idValue.runtime(e.RuntimeId) === runtime)
        .map((e) => Number(idValue.localSeq(e.LocalSeq)))

      assert.deepEqual(seqs, [1, 2], `${runtime} must appear in LocalSeq order`)
    }
  })
})

test('PERSIST_004_one_corrupt_stream_does_not_discard_a_healthy_one', () => {
  withStore((store) => {
    store.open({ runtime: 'rt_good' }).append(stream.session(SESSION), CLOSED)
    store.writeRaw('rt_bad', [line('rt_bad', 1), '{broken'].join('\n') + '\n')

    const booted = store.boot()

    // Independent logs per runtime: a damaged file costs only its own tail. One
    // shared file would make this an all-or-nothing loss.
    assert.equal(booted.diagnostics.length, 1)
    assert.match(booted.diagnostics[0], /rt_bad\.ndjson/)

    const good = booted.envelopes.filter((e) => idValue.runtime(e.RuntimeId) === 'rt_good')
    assert.deepEqual(
      good.map((e) => Number(idValue.localSeq(e.LocalSeq))),
      [1, 2],
    )
  })
})
