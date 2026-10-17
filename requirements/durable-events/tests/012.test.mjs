import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const CLOSED_AGENT = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: 'ses_es_writer' },
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const withRepo = (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  return fn(commonDir)
    .finally(() => rmSync(repo, { recursive: true, force: true }))
}

test('WHAT[durable-events-012] BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB', async () => {
  await withRepo('journal-blob-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es_blob', 4242, '2026-04-01T00:00:00Z'), 'boot')

    const receipt = mustOk(await journal.JournalSurface_writePayload(booted.journal, 'large-body\n'), 'write')
    assert.match(receipt.blobRef, /^blobs\/[0-9a-f]{64}$/)

    const handle = receipt.blobRef.slice('blobs/'.length)
    // durable-events-012: payloads are inline in the ndjson event line; no
    // payloads directory exists.
    assert.equal(existsSync(join(commonDir, 'wanxiangshu', 'payloads')), false)

    const read = mustOk(await journal.JournalSurface_readPayload(booted.journal, receipt.blobRef), 'read')
    assert.equal(read.content, 'large-body\n')
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[durable-events-012] appended_fact_lifts_real_blob_digest_into_persisted_payload_refs', async () => {
  await withRepo('journal-closure-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_bootWithWriterId(commonDir, 'journal-closure-proof', 'rt_es_closure', 4242, '2026-04-01T00:00:00Z'), 'boot')

    const receipt = mustOk(await journal.JournalSurface_writePayload(booted.journal, 'part-body\n'), 'write')
    const handle = receipt.blobRef.slice('blobs/'.length)

    const fact = {
      family: 'Companion',
      case: 'TerminalOutputCaptured',
      payload: {
        SessionId: 'ses_closure',
        TextRef: receipt.blobRef,
        TextDigest: receipt.blobDigest,
        ProviderRun: 'real-closure-run',
      },
    }

    const appended = mustOk(
      await journal.JournalSurface_appendAgent(booted.journal, { kind: 'Session', session: 'ses_closure' }, 'real-closure-run', fact),
      'append',
    )
    assert.ok(appended.projection)

    const ndjson = readFileSync(join(commonDir, 'wanxiangshu', 'events', 'journal-closure-proof-0000.ndjson'), 'utf8')
    assert.match(ndjson, new RegExp(`"payload_refs":\\["${handle}"\\]`))
    // The same event line embeds the payload inline (durable-events-012).
    assert.match(ndjson, new RegExp(`"payloads":\\{"${handle}":"${Buffer.from('part-body\n', 'utf8').toString('base64')}"\\}`))
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[durable-events-012] closure_fails_closed_when_a_real_content_address_is_missing', async () => {
  await withRepo('journal-closure-missing', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es_missing', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const missingDigest = 'f'.repeat(64)

    const fact = {
      family: 'Companion',
      case: 'TerminalOutputCaptured',
      payload: {
        SessionId: 'ses_missing',
        TextRef: `blobs/${missingDigest}`,
        TextDigest: missingDigest,
        ProviderRun: 'real-missing-run',
      },
    }

    const result = await journal.JournalSurface_appendAgent(
      booted.journal,
      { kind: 'Session', session: 'ses_missing' },
      'real-missing-run',
      fact,
    )
    assert.equal(result.ok, false)
    assert.match(JSON.stringify(result.error), /MissingPayload/i)
    journal.JournalSurface_dispose(booted.journal)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const withJournal = async (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), `wxs-journal-closure-${writerId}-`))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  const booted = await journal.JournalSurface_bootWithWriterId(commonDir, writerId, `rt-${writerId}`, 4242, '2026-04-01T00:00:00Z')
  assert.equal(booted.ok, true, JSON.stringify(booted.error))
  try {
    await fn(commonDir, booted.journal)
  } finally {
    journal.JournalSurface_dispose(booted.journal)
    rmSync(repo, { recursive: true, force: true })
  }
}
const terminalCaptured = (session, ref, digest) => ({
  family: 'Companion',
  case: 'TerminalOutputCaptured',
  payload: {
    SessionId: session,
    TextRef: ref,
    TextDigest: digest,
    ProviderRun: `run-${session}`,
  },
})
const payloadRefsFromFile = (commonDir, writerId) => {
  const file = join(commonDir, 'wanxiangshu', 'events', `${writerId}-0000.ndjson`)
  return readFileSync(file, 'utf8')
    .trim()
    .split('\n')
    .map(JSON.parse)
    .filter((event) => event.event_type === 'JournalEnvelope')
    .map((event) => event.payload_refs)
}
const inlinePayloadsFromFile = (commonDir, writerId) => {
  const file = join(commonDir, 'wanxiangshu', 'events', `${writerId}-0000.ndjson`)
  return readFileSync(file, 'utf8')
    .trim()
    .split('\n')
    .map(JSON.parse)
    .filter((event) => event.event_type === 'JournalEnvelope')
    .map((event) => event.payloads || {})
}

test('WHAT[durable-events-012] closure_lifts_a_content_addressed_digest_into_payload_refs', async () => {
  await withJournal('closure-real', async (commonDir, handle) => {
    const receipt = await journal.JournalSurface_writePayload(handle, 'durable body\n')
    assert.equal(receipt.ok, true, JSON.stringify(receipt.error))

    const appended = await journal.JournalSurface_appendAgent(
      handle,
      { kind: 'Session', session: 'ses_real' },
      'run-ses_real',
      terminalCaptured('ses_real', receipt.blobRef, receipt.blobDigest),
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))

    const refs = payloadRefsFromFile(commonDir, 'closure-real')
    assert.deepEqual(refs.at(-1), [receipt.blobRef.slice('blobs/'.length)])
    // durable-events-012: the same event line embeds the payload inline.
    const inline = inlinePayloadsFromFile(commonDir, 'closure-real')
    assert.deepEqual(inline.at(-1), {
      [receipt.blobRef.slice('blobs/'.length)]: Buffer.from('durable body\n', 'utf8').toString('base64'),
    })
  })
})
test('WHAT[durable-events-012] closure_dedupes_a_matching_blob_ref_and_digest_pair', async () => {
  await withJournal('closure-dedupe', async (commonDir, handle) => {
    const receipt = await journal.JournalSurface_writePayload(handle, 'dedupe body\n')
    assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
    // The writer derives the digest from bytes; use a real content address and
    // assert the closure contains one ref even though ref + digest are both present.
    const appended = await journal.JournalSurface_appendAgent(
      handle,
      { kind: 'Session', session: 'ses_dedupe' },
      'run-ses_dedupe',
      terminalCaptured('ses_dedupe', receipt.blobRef, receipt.blobDigest),
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))

    const refs = payloadRefsFromFile(commonDir, 'closure-dedupe')
    assert.equal(refs.at(-1).length, 1)
    assert.equal(refs.at(-1)[0], receipt.blobRef.slice('blobs/'.length))
  })
})
test('WHAT[durable-events-012] closure ignores placeholder handles while Trace refuses to treat them as original material', async () => {
  await withJournal('closure-placeholder', async (commonDir, handle) => {
    const { spawnSync } = await import('node:child_process')
    const { fileURLToPath } = await import('node:url')
    const { readdirSync } = await import('node:fs')
    journal.JournalSurface_dispose(handle)
    const { NODE_TEST_CONTEXT, WANXIANGSHU_NO_FATAL_EXIT, ...env } = process.env
    const probe = spawnSync(process.execPath, [fileURLToPath(new URL('../../semantic-trace/tests/support/invalid-material-child.mjs', import.meta.url)), commonDir, 'placeholder'], { env, encoding: 'utf8', timeout: 10000 })
    assert.ifError(probe.error)
    assert.equal(probe.signal, 'SIGKILL', probe.stderr)
    assert.match(probe.stderr, /XTrace material is outside the declared payload closure/)
    const directory = join(commonDir, 'wanxiangshu', 'events')
    const facts = readdirSync(directory).filter(file => file.endsWith('.ndjson'))
      .flatMap(file => readFileSync(join(directory, file), 'utf8').trim().split('\n').map(JSON.parse))
    const placeholder = facts.filter(event => JSON.stringify(event.payload).includes('blobs/placeholder'))
    assert.equal(placeholder.length, 1)
    assert.deepEqual(placeholder[0].payload_refs, [])
  })
})
test('WHAT[durable-events-012] closure_is_empty_for_a_fact_without_blob_fields', async () => {
  await withJournal('closure-empty', async (commonDir, handle) => {
    const appended = await journal.JournalSurface_appendAgent(
      handle,
      { kind: 'Session', session: 'ses_empty' },
      null,
      { family: 'Companion', case: 'CompanionBloggerClosed', payload: { SessionId: 'ses_empty' } },
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))
    assert.deepEqual(payloadRefsFromFile(commonDir, 'closure-empty').at(-1), [])
  })
})
}

{
  const { default: assert } = await import('node:assert/strict')
  const { randomUUID, createHash } = await import('node:crypto')
  const { mkdtempSync, readFileSync, realpathSync, rmSync, existsSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const { fileURLToPath } = await import('node:url')
  const { runVerificationToolProbe } = await import('../../../scripts/lib/verification-tool-probe.mjs')
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const child = fileURLToPath(new URL('./support/payload-lock-acquire-child.mjs', import.meta.url))

  for (const scenario of ['mkdir-eacces', 'mkdir-eio', 'busy-success', 'busy-eacces']) {
    test(`WHAT[durable-events-012] actual Journal payload ${scenario} respects the original gate and preserves canonical history`, async t => {
      assert.equal(typeof journal.JournalSurface_writePayload, 'function')
      const root = realpathSync(mkdtempSync(join(tmpdir(), 'payload-lock-acquire-')))
      const commonDir = join(root, '.git')
      const sourceWriterId = randomUUID()
      const measureWriter = randomUUID()
      const sourceFile = join(commonDir, 'wanxiangshu', 'events', `${sourceWriterId}-0000.ndjson`)
      const request = { sourceWriterId, sessionId: 'payload-g1-existing-session',
        oldBody: '原有非空payload\r\nNUL:\u0000；雪 😀 保留尾空格  ',
        incomingBody: '新的非空payload\r\nNUL:\u0000；é 😀 保留尾空格  ' }
      const oldDigest = createHash('sha256').update(request.oldBody, 'utf8').digest('hex')
      const incomingDigest = createHash('sha256').update(request.incomingBody, 'utf8').digest('hex')
      const env = { ...process.env }
      delete env.NODE_TEST_CONTEXT
      delete env.NODE_OPTIONS
      const probe = async (mode, writer) => {
        try {
          return JSON.parse(await runVerificationToolProbe(process.execPath,
            [child, mode, commonDir, writer, scenario, JSON.stringify(request)], { cwd: root, env, signal: t.signal }))
        } catch (error) {
          if (typeof error?.stderr === 'string' && typeof error.message === 'string') error.message += '\n' + error.stderr
          throw error
        }
      }
      let completed = false
      try {
        const measured = await probe('measure', measureWriter)
        const cold = await probe('cold', randomUUID())
        assert.notEqual(measured.pid, process.pid)
        assert.notEqual(cold.pid, process.pid)
        assert.notEqual(cold.pid, measured.pid)
        assert.equal(measured.oldDigest, oldDigest)
        assert.equal(measured.incomingDigest, incomingDigest)
        assert.deepEqual(measured.result, { ok: true, blobRef: 'blobs/' + incomingDigest,
          blobDigest: incomingDigest })
        // durable-events-012: staged inline payloads never create a payloads
        // directory and never touch disk before an append.
        assert.equal(measured.observed.payloadWrite, 0)
        assert.equal(measured.observed.payloadFsync, 0)
        assert.equal(measured.observed.payloadClose, 0)
        assert.equal(measured.payloadBeforeRelease, false)
        assert.equal(measured.observed.injected, 0)
        assert.equal(measured.observed.append, 0)
        assert.deepEqual(measured.before.eventFiles, [`${sourceWriterId}-0000.ndjson`])
        assert.equal(measured.before.writerCreated, false)
        assert.ok(measured.beforeCurrent.sessions.includes(request.sessionId))
        assert.deepEqual(measured.beforeCurrent.sessionProjections[request.sessionId].xTrace,
          { openingPresent: false, partCount: 0, latestTerminalPresent: true })
        assert.equal(measured.factRefs.length, 2)
        assert.deepEqual(measured.factRefs.at(-1), [oldDigest])
        assert.deepEqual(measured.afterCurrent, measured.beforeCurrent)
        assert.deepEqual(measured.oldRead, { ok: true, content: request.oldBody })
        assert.deepEqual(measured.incomingRead, { ok: true, content: request.incomingBody })
        assert.deepEqual(cold.current, measured.beforeCurrent)
        assert.deepEqual(cold.factRefs, measured.factRefs)
        assert.deepEqual(cold.oldRead, measured.oldRead)
        // The staged incoming payload is process-local until an append embeds
        // it; a cold process only sees committed inline payloads.
        assert.deepEqual(cold.incomingRead, { ok: false, error: 'event-store payload missing: ' + incomingDigest })
        assert.deepEqual(cold.physical, measured.physical)
        // The seed event line embeds the old payload inline.
        const seedEvent = JSON.parse(readFileSync(sourceFile, 'utf8').trimEnd().split('\n').at(-1))
        assert.equal(seedEvent.payload_refs[0], oldDigest)
        assert.equal(seedEvent.payloads[oldDigest], Buffer.from(request.oldBody, 'utf8').toString('base64'))
        assert.equal(existsSync(join(commonDir, 'wanxiangshu', 'payloads')), false,
          'ndjson is the only carrier; a payloads directory must never appear')
        t.diagnostic(JSON.stringify({ scenario, measurePid: measured.pid, coldPid: cold.pid,
          actualInlinePayloadAndCold: true, ...measured.observed }))
        completed = true
      } finally {
        if (completed) rmSync(root, { recursive: true, force: true })
        else t.diagnostic('PAYLOAD_LOCK_ACQUIRE_FAILURE_EVIDENCE: retained ' + root)
      }
    })
  }
}
