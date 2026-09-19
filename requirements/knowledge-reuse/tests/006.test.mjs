import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const bookkeeperRefresh = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js");
const { CANONICAL_A, CANONICAL_Q, installBookkeeperRuntime, scriptedBookkeeperPort } = await import("./support/bookkeeper-session-support.mjs");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-'))
  const handle = eventStore.create(dir, 'bookkeeper-mechanical')
  return {
    dir,
    handle,
    cleanup: () => {
      eventStore.dispose(handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

test('WHAT[knowledge-reuse-006] CASE006_synthesis_refresh_publishes_refreshed_with_revised_a', async () => {
  const { dir, handle, cleanup } = sandbox()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-mech-1', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, false)
    assert.equal((await bookkeeperRefresh.refreshStale(handle, dir, 's-mech-1')).value, false)
    assert.equal(createCalls.length, 0)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, true)
    await installBookkeeperRuntime(port, ['s-mech-1'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-mech-1')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const fetched = await casebook.fetchCase(handle, 10, 's-mech-1')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.a, CANONICAL_A)
    assert.equal(fetched.value.observations[0].contentHash, '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')
    assert.equal((await casebook.needsRefresh(handle, 10, 's-mech-1', dir)).value, true)
    assert.notEqual(fetched.value, null)
  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})
test('WHAT[knowledge-reuse-006] CASE006_mechanical_refresh_no_case_is_noop', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    const result = await bookkeeperRefresh.refreshStale(handle, dir, 'missing')
    assert.equal(result.ok, true)
    assert.equal(result.value, false)
  } finally {
    cleanup()
  }
})
test('WHAT[knowledge-reuse-006] CASE006_mechanical_refresh_missing_file_still_publishes', async () => {
  const { dir, handle, cleanup } = sandbox()
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'gone.txt'), 'x', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-gone', 'Q', 'A', [fileRead('gone.txt', casebook.contentHash('x'))]))).ok, true)
    rmSync(join(dir, 'gone.txt'), { force: true })
    await installBookkeeperRuntime(port, ['s-gone'])
    const result = await bookkeeperRefresh.refreshStale(handle, dir, 's-gone')
    assert.equal(result.ok, true)
    assert.equal(result.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    const fetched = await casebook.fetchCase(handle, 10, 's-gone')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.observations.length, 1)
  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, mkdirSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const bookkeeperRefresh = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js");
const lifecycle = await import("../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const CANONICAL_Q = 'Canonical maintained question'
const CANONICAL_A = 'Summary of Inspector answers across turns.'
const scriptedBookkeeperPort = () => {
  const createCalls = []
  const prompts = []
  const programCalls = []
  const terminals = new Set()
  let seq = 0
  const port = {
    CreateChildSession: async () => {
      throw new Error('Bookkeeper must not attach to a deleted physical parent')
    },
    CreateSiblingSession: async (_ownerSessionId, physicalParentId) => {
      seq += 1
      const child = `bk-child-${seq}`
      createCalls.push({ child, physicalParentId })
      return bookkeeper.acceptedSession(child)
    },
    AbortSession: async () => bookkeeper.aborted(),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return { Dispose: () => terminals.delete(callback) }
    },
    SendPrompt: async (childSession, text) => {
      prompts.push(text)
      const sid = bookkeeper.sessionValue(childSession)
      const tx = bookkeeper.txIdFor(sid)
      assert.notEqual(tx, '', 'SendPrompt must run against a bound Bookkeeper tx')
      const out = await bookkeeper.runProgram(
        sid,
        `class Js extends JsProgram { async run() { this.setQuestion(${JSON.stringify(CANONICAL_Q)}); this.setAnswer(${JSON.stringify(CANONICAL_A)}); return { changed: true }; } }`,
      )
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(bookkeeper.sessionId(sid), bookkeeper.completed(sid))
      return bookkeeper.acceptedPrompt()
    },
  }
  return { port, createCalls, prompts, programCalls }
}
const installBookkeeperRuntime = (port, ownerSessionIds) => {
  const installed = bookkeeper.setRuntime(
    port,
    ownerSessionIds.map((sessionId) => ({
      sessionId,
      logicalRunId: `bookkeeper-run-${sessionId}`,
      authorityRootUserMessageId: `bookkeeper-root-${sessionId}`,
      agent: 'engineer',
    })),
  )
  assert.equal(installed.ok, true, installed.error)
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const openStore = (dir, writerId) => eventStore.create(join(dir, '.git'), writerId)

test('WHAT[knowledge-reuse-006] CASE006_create_child_once_per_refresh_via_js_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-refresh-'))
  const handle = eventStore.create(dir, 'bookkeeper-session-refresh')
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-session-refresh', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    await installBookkeeperRuntime(port, ['s-session-refresh'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-session-refresh')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(createCalls[0].physicalParentId, undefined)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseRefresh')), true)
    const fetched = await casebook.fetchCase(handle, 10, 's-session-refresh')
    assert.equal(fetched.value.q, CANONICAL_Q)
    assert.equal(fetched.value.a, CANONICAL_A)
  } finally {
    bookkeeper.resetRuntime()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, mkdirSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const bookkeeperRefresh = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js");
const lifecycle = await import("../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js");
const { CANONICAL_A, CANONICAL_Q, installBookkeeperRuntime, scriptedBookkeeperPort } = await import("./support/bookkeeper-session-support.mjs");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const FAIL_A = 'A-must-fail-synthesis'
const failingPort = () => {
  let seq = 0
  return {
    port: {
      CreateChildSession: async () => {
        throw new Error('Bookkeeper must not attach to a deleted physical parent')
      },
      CreateSiblingSession: async () => bookkeeper.acceptedSession(`bk-fail-${++seq}`),
      AbortSession: async () => bookkeeper.aborted(),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => bookkeeper.failedPrompt('injected synth failure'),
    },
  }
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

test('WHAT[knowledge-reuse-006] CASE006_injected_synthesizer_error_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-err-'))
  const handle = eventStore.create(dir, 'bookkeeper-synthesis-error')
  const { port } = failingPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-err-1', 'Q keep', FAIL_A, [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    await installBookkeeperRuntime(port, ['s-err-1'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-err-1')
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /injected synth failure/)
    const fetched = await casebook.fetchCase(handle, 10, 's-err-1')
    assert.equal(fetched.value.a, FAIL_A)
    assert.equal(fetched.value.q, 'Q keep')
    assert.equal(fetched.value.observations[0].contentHash, casebook.contentHash('hello'))
  } finally {
    bookkeeper.resetRuntime()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[knowledge-reuse-006] CASE006_synthesizer_runs_once_per_stale_refresh', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-once-'))
  const handle = eventStore.create(dir, 'bookkeeper-synthesis-once')
  const { port, createCalls, programCalls } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-once', 'Q-count-synth-once', 'A once', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    await installBookkeeperRuntime(port, ['s-once'])
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-once')
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
  } finally {
    bookkeeper.resetRuntime()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => schemaNode('string'),
  enum: (values) => schemaNode('enum', { values }),
}
const factory = { tool: { schema: fakeSchema } }

test('WHAT[knowledge-reuse-006] CASE006_bookkeeper_provider_contract_is_one_program', () => {
  const tool = bookkeeper.contract(factory)
  assert.equal(tool.name, 'js-bookkeeper')
  assert.deepEqual(tool.argumentNames, ['program'])
  assert.match(tool.description, /one atomic JavaScript transformation|用一次原子的 JavaScript transformation/i)
  assert.match(tool.description, /setQuestion\(newText\)/)
  assert.match(tool.description, /setAnswer\(newText\)/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => schemaNode('string'),
  enum: (values) => schemaNode('enum', { values }),
}
const factory = { tool: { schema: fakeSchema } }
const run = (sessionId, program) => bookkeeper.runProgram(sessionId, program)
const current = (txId) => {
  const staged = bookkeeper.snapshot(txId)
  assert.equal(staged.ok, true)
  return staged.value
}
const currentCase = (question, answer) => [question, answer]

test('WHAT[knowledge-reuse-006] js_bookkeeper_surface_is_program_only_and_has_case_sdk', () => {
  const tool = bookkeeper.contract(factory)
  assert.equal(tool.name, 'js-bookkeeper')
  assert.deepEqual(tool.argumentNames, ['program'])
  assert.match(tool.description, /question\(matches = \[\]\)/)
  assert.match(tool.description, /answer\(matches = \[\]\)/)
  assert.match(tool.description, /setQuestion\(newText\)/)
  assert.match(tool.description, /setAnswer\(newText\)/)
  assert.match(tool.description, /not a line number|不是行号/)
  assert.doesNotMatch(tool.description, /Q\.md|A\.md|old_text|new_text|filesystem/i)
})
test('WHAT[knowledge-reuse-006] js_bookkeeper_program_reshapes_question_and_answer_atomically', async () => {
  const tx = 'tx-js-bookkeeper-both'
  const session = 'bk-js-bookkeeper-both'
  bookkeeper.beginTransaction(tx, '## Goal\nkeep old goal\n## Constraints\nold constraint', '## Answer\nold answer\n## Evidence\nweak')
  bookkeeper.bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          const question = this.question([
            ["goal", "afterGoal", "## Goal"],
            ["constraints", "afterConstraints", "## Constraints"],
          ]);
          const answer = this.answer([
            ["claim", "afterClaim", "## Answer"],
            ["evidence", "afterEvidence", "## Evidence"],
          ]);

          this.setQuestion(
            question.text("^", "constraints")
              + "## Constraints\\nnew constraint"
          );
          this.setAnswer(
            "## Answer\\nnew answer\\n"
              + answer.text("evidence", "$")
          );
          return { changed: true, source: "coherent case" };
        }
      }`,
    )

    assert.match(String(result), /changed = true/)
    assert.deepEqual(current(tx), ['## Goal\nkeep old goal\n## Constraints\nnew constraint', '## Answer\nnew answer\n## Evidence\nweak'])

    const taken = bookkeeper.take(tx)
    assert.equal(taken.ok, true)
    assert.deepEqual(taken.value, currentCase(taken.value[0], taken.value[1]))
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})
test('WHAT[knowledge-reuse-006] js_bookkeeper_zero_mutation_is_legal', async () => {
  const tx = 'tx-js-bookkeeper-idle'
  const session = 'bk-js-bookkeeper-idle'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          return { changed: false, question: this.question().text(), answer: this.answer().text() };
        }
      }`,
    )

    assert.match(String(result), /changed = false/)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})
test('WHAT[knowledge-reuse-006] js_bookkeeper_duplicate_set_rolls_back_the_whole_program', async () => {
  const tx = 'tx-js-bookkeeper-duplicate'
  const session = 'bk-js-bookkeeper-duplicate'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          this.setQuestion("Q1");
          this.setQuestion("Q2");
          this.setAnswer("A1");
          return { changed: true };
        }
      }`,
    )

    assert.match(String(result), /setQuestion may be called at most once|setQuestion 在同一个 js-bookkeeper program 中最多只能调用一次/i)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})
test('WHAT[knowledge-reuse-006] js_bookkeeper_program_failure_rolls_back_staged_mutation', async () => {
  const tx = 'tx-js-bookkeeper-throw'
  const session = 'bk-js-bookkeeper-throw'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          this.setAnswer("changed");
          throw new Error("semantic stop");
        }
      }`,
    )

    assert.match(String(result), /semantic stop/)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})
test('WHAT[knowledge-reuse-006] js_bookkeeper_unbound_session_cannot_change_a_case', async () => {
  const result = await run(
    'no-such-session',
    `class Js extends JsProgram {
      async run() {
        this.setQuestion("changed");
        return null;
      }
    }`,
  )

  assert.match(String(result), /no Bookkeeper transaction|没有 Bookkeeper transaction/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");

test('WHAT[knowledge-reuse-006] T23_bookkeeper_refresh_receives_diff_only_and_has_no_repo_investigation_rights', async () => {
  // Bookkeeper CaseRefresh receives: old case + diff. No repo read/glob/grep.
  assert.equal(typeof bookkeeper.createRefreshPrompt, 'function', 'bookkeeper must format refresh prompt with diff only')
  const prompt = bookkeeper.createRefreshPrompt({
    q: 'Old Q',
    a: 'Old A',
    relatedPaths: ['a.txt'],
    diff: '-B\n+C',
  })
  assert.match(prompt, /CaseRefresh/)
  assert.match(prompt, /Old Q/)
  assert.match(prompt, /Old A/)
  assert.match(prompt, /diff/)
  assert.match(prompt, /Do not read the repository/)
  assert.doesNotMatch(prompt, /\[(?:transcript|file_contents|observations)\]/i,
    'maintenance must not receive a replay trace or full file payloads')
})
}
