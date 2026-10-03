import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, casebook, index, parse } from './support/casebook.mjs'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { CANONICAL_Q, CANONICAL_A, installBookkeeperRuntime, scriptedBookkeeperPort } from './support/bookkeeper-session-support.mjs'

test('WHAT[knowledge-reuse-005] actual fetch refreshes changed files once and atomically retains the completion baseline', async () => {
  const local = sandbox()
  try {
    const { identity, baseline, shelfmark } = await createCase(local)
    const { port, createCalls } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    assert.equal(parse(await local.fetch(shelfmark)).answer, 'Answer B')
    assert.equal(createCalls.length, 0)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    assert.equal(parse(await local.fetch(shelfmark)).answer, CANONICAL_A)
    const maintained = await casebook.fetchCaseByIdentity(local.store, identity)
    assert.equal(maintained.q, CANONICAL_Q)
    assert.equal(maintained.completionFileState, baseline)
    assert.notEqual(maintained.maintenanceFileState, baseline)
    const again = await local.fetch(index.shelfmarkFor(identity, CANONICAL_Q))
    assert.equal(parse(again).answer, CANONICAL_A)
    assert.match(again, /No change was found|没有变化/)
    assert.equal(createCalls.length, 1)
  } finally { bookkeeper.resetRuntime(); local.close() }
})

test('WHAT[knowledge-reuse-005] failed actual maintenance returns the old answer and leaves both baselines unchanged', async () => {
  const local = sandbox()
  try {
    bookkeeper.resetRuntime()
    const { identity, baseline, shelfmark } = await createCase(local)
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    const result = await local.fetch(shelfmark)
    assert.equal(parse(result).answer, 'Answer B')
    assert.match(result, /could not|unable|未|无法/i)
    const current = await casebook.fetchCaseByIdentity(local.store, identity)
    assert.equal(current.completionFileState, baseline)
    assert.equal(current.maintenanceFileState, baseline)
  } finally { local.close() }
})

test('WHAT[knowledge-reuse-005] actual maintenance receives the durable old bytes rather than their digest', async () => {
  const local = sandbox()
  try {
    const { identity, shelfmark } = await createCase(local)
    const { port, prompts } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    assert.equal(parse(await local.fetch(shelfmark)).answer, CANONICAL_A)
    assert.equal(prompts.length, 1)
    const diff = parse(prompts[0]).diff.content
    assert.match(diff, /-version-B/)
    assert.match(diff, /\+version-C/)
    assert.doesNotMatch(diff, new RegExp(casebook.contentHash('version-B')))
  } finally { bookkeeper.resetRuntime(); local.close() }
})

test('WHAT[knowledge-reuse-005] actual maintenance receives changed lines without unchanged file bodies', async () => {
  const local = sandbox()
  try {
    const { identity, baseline, shelfmark } = await createCase(local, 'multiline', 'unchanged header\nold line\nunchanged footer\n')
    const { port, prompts } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    writeFileSync(join(local.dir, 'subject.txt'), 'unchanged header\nnew line\nunchanged footer\n')
    const expected = [
      'diff --git a/subject.txt b/subject.txt',
      '--- a/subject.txt',
      '+++ b/subject.txt',
      '@@ -2,1 +2,1 @@',
      '-old line',
      '+new line',
      '',
    ].join('\n')
    const capture = await casebook.computeMaintenanceDiff(local.store, local.dir, ['subject.txt'], baseline)
    assert.equal(capture.diffSummary, expected)
    await local.fetch(shelfmark)
    assert.equal(prompts.length, 1)
    assert.equal(parse(prompts[0]).diff.content, expected)
  } finally { bookkeeper.resetRuntime(); local.close() }
})

test('WHAT[knowledge-reuse-005] separated edits produce two hunks without sending the unchanged middle to Bookkeeper', async () => {
  const local = sandbox()
  try {
    const middle = 'UNCHANGED PRIVATE BODY\nUNCHANGED SECOND LINE\nUNCHANGED THIRD LINE\n'
    const { identity, baseline, shelfmark } = await createCase(local, 'separated-edits', 'old head\n' + middle + 'old tail\n')
    const { port, prompts } = scriptedBookkeeperPort()
    installBookkeeperRuntime(port, [identity])
    writeFileSync(join(local.dir, 'subject.txt'), 'new head\n' + middle + 'new tail\n')
    const expected = [
      'diff --git a/subject.txt b/subject.txt',
      '--- a/subject.txt',
      '+++ b/subject.txt',
      '@@ -1,1 +1,1 @@',
      '-old head',
      '+new head',
      '@@ -5,1 +5,1 @@',
      '-old tail',
      '+new tail',
      '',
    ].join('\n')
    const captured = await casebook.computeMaintenanceDiff(local.store, local.dir, ['subject.txt'], baseline)
    assert.equal(captured.diffSummary, expected)
    assert.equal(parse(await local.fetch(shelfmark)).answer, CANONICAL_A)
    assert.equal(prompts.length, 1)
    const delivered = parse(prompts[0]).diff.content
    assert.doesNotMatch(delivered, /UNCHANGED/)
    assert.deepEqual(delivered.split('\n').filter(line => line.startsWith('@@')), [
      '@@ -1,1 +1,1 @@',
      '@@ -5,1 +5,1 @@',
    ])
  } finally { bookkeeper.resetRuntime(); local.close() }
})

const minimumLineEdits = (before, after) => {
  let previous = Array.from({ length: before.length + 1 }, (_, index) => index)
  for (let target = 1; target <= after.length; target += 1) {
    const current = [target]
    for (let source = 1; source <= before.length; source += 1) {
      current[source] = before[source - 1] === after[target - 1]
        ? previous[source - 1]
        : Math.min(previous[source] + 1, current[source - 1] + 1)
    }
    previous = current
  }
  return previous[before.length]
}

const applyLinePatch = (before, diff) => {
  const rendered = diff.split('\n')
  const result = []
  let source = 0
  let edits = 0
  for (let index = 0; index < rendered.length; index += 1) {
    const header = /^@@ -(\d+),(\d+) \+(\d+),(\d+) @@$/.exec(rendered[index])
    if (!header) continue
    const [, oldStart, oldCount, newStart, newCount] = header.map(Number)
    const oldIndex = oldCount === 0 ? oldStart : oldStart - 1
    const newIndex = newCount === 0 ? newStart : newStart - 1
    assert.ok(oldIndex >= source, 'hunks must be ordered and disjoint')
    result.push(...before.slice(source, oldIndex))
    assert.equal(result.length, newIndex, 'new hunk coordinates must match the applied prefix')
    const removed = []
    const added = []
    while (index + 1 < rendered.length && /^[+-]/.test(rendered[index + 1])) {
      index += 1
      const line = rendered[index]
      if (line[0] === '-') removed.push(line.slice(1) + '\n')
      else added.push(line.slice(1) + '\n')
    }
    assert.equal(removed.length, oldCount)
    assert.equal(added.length, newCount)
    assert.deepEqual(removed, before.slice(oldIndex, oldIndex + oldCount))
    result.push(...added)
    source = oldIndex + oldCount
    edits += oldCount + newCount
  }
  result.push(...before.slice(source))
  return { lines: result, edits }
}

test('WHAT[knowledge-reuse-005] bounded repeated-line corpus produces applicable minimal edits against an independent dynamic-programming oracle', async (t) => {
  const corpus = [[]]
  let previous = [[]]
  for (let length = 1; length <= 3; length += 1) {
    previous = previous.flatMap(lines => ['A\n', 'B\n'].map(line => [...lines, line]))
    corpus.push(...previous)
  }
  for (const before of corpus) {
    await t.test(`WHAT[knowledge-reuse-005] all target sequences from ${JSON.stringify(before)}`, async () => {
      const local = sandbox()
      try {
        const subject = join(local.dir, 'subject.txt')
        writeFileSync(subject, before.join(''))
        const baseline = await casebook.freezeCompletionState(local.store, local.dir, ['subject.txt'])
        assert.equal(typeof baseline, 'string')
        for (const after of corpus) {
          writeFileSync(subject, after.join(''))
          const captured = await casebook.computeMaintenanceDiff(local.store, local.dir, ['subject.txt'], baseline)
          assert.equal(typeof captured.diffSummary, 'string', JSON.stringify(captured))
          const patched = applyLinePatch(before, captured.diffSummary)
          assert.deepEqual(patched.lines, after)
          assert.equal(patched.edits, minimumLineEdits(before, after))
        }
      } finally { local.close() }
    })
  }
})

test('WHAT[knowledge-reuse-005] separated insertion and replacement preserve CRLF and the missing final newline marker', async () => {
  const local = sandbox()
  try {
    const subject = join(local.dir, 'subject.txt')
    writeFileSync(subject, '\uFEFFheader\r\nkept\r\nold tail')
    const baseline = await casebook.freezeCompletionState(local.store, local.dir, ['subject.txt'])
    assert.equal(typeof baseline, 'string')
    writeFileSync(subject, '\uFEFFheader\r\ninserted\r\nkept\r\nnew tail\r\n')
    const captured = await casebook.computeMaintenanceDiff(local.store, local.dir, ['subject.txt'], baseline)
    assert.equal(captured.diffSummary, [
      'diff --git a/subject.txt b/subject.txt',
      '--- a/subject.txt',
      '+++ b/subject.txt',
      '@@ -1,0 +2,1 @@',
      '+inserted\r',
      '@@ -3,1 +4,1 @@',
      '-old tail',
      '\\ No newline at end of file',
      '+new tail\r',
      '',
    ].join('\n'))
  } finally { local.close() }
})
