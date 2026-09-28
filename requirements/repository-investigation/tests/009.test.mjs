import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as semble from '../../../dist/Repository/Investigation/SembleSurface.js'
import { hint, searchResult, warmStart, withWorkspace } from './support/warm-start.mjs'

const deferred = () => {
  let resolve
  const promise = new Promise((done) => { resolve = done })
  return { promise, resolve }
}

test('WHAT[repository-investigation-009] concurrent queries may finish in reverse order or fail without changing the merged keyword order', async () => {
  await withWorkspace(async (directory) => {
    const gates = new Map(['slow', 'broken', 'fast'].map((query) => [query, deferred()]))
    const finished = new Map(['slow', 'broken', 'fast'].map((query) => [query, deferred()]))
    const allStarted = deferred()
    const starts = []
    const completions = []
    const search = async (query, workspace, topK) => {
      assert.equal(workspace, directory)
      assert.equal(topK, warmStart.topKPerKeyword)
      starts.push(query)
      if (starts.length === 3) allStarted.resolve()
      await gates.get(query).promise
      completions.push(query)
      finished.get(query).resolve()
      if (query === 'broken') throw new Error('query failed')
      return [hint(`src/${query}.fs`, `hit:${query}`)]
    }
    const pending = warmStart.prepareWithSearch(search, 'investigation-order', 'Engineer', directory, 'slow\nbroken\nfast', 'Inspect.')
    try {
      await allStarted.promise
      assert.deepEqual(new Set(starts), new Set(['slow', 'broken', 'fast']))
      for (const query of ['fast', 'broken', 'slow']) {
        gates.get(query).resolve()
        await finished.get(query).promise
      }
      const result = await pending
      assert.equal(result.ok, true)
      const data = parseToml(result.value)
      assert.deepEqual(completions, ['fast', 'broken', 'slow'])
      assert.deepEqual(data.repository_search.map((item) => [item.ordinal, item.query, item.candidate_count]), [[1, 'slow', 1], [2, 'broken', 0], [3, 'fast', 1]])
      assert.deepEqual(data.repository_hint.map((item) => [item.keyword_ordinal, item.local_rank, item.content]), [[1, 1, 'hit:slow'], [3, 1, 'hit:fast']])
    } finally {
      for (const gate of gates.values()) gate.resolve()
      await pending
    }
  })
})

test('WHAT[repository-investigation-009] stable dedupe compares every path, range and content component', () => {
  const same = hint('src/a.fs', 'original')
  const searches = [
    searchResult(1, 'first', [same]),
    searchResult(2, 'second', [
      { ...same, score: 0.1 },
      { ...same, filePath: 'src/b.fs' },
      { ...same, startLine: 2 },
      { ...same, endLine: 4 },
      { ...same, content: 'changed' },
    ]),
  ]
  const data = parseToml(warmStart.render(['Inspect actual files.'], 'Inspect actual files.', searches))
  assert.equal(data.repository_hint.length, 5)
  assert.deepEqual(data.repository_hint.map((item) => [item.keyword_ordinal, item.local_rank]), [[1, 1], [2, 2], [2, 3], [2, 4], [2, 5]])
  assert.equal(data.repository_hint[0].score, 0.9)
  assert.equal(data.repository_hint.at(-1).content, 'changed')
})

test('WHAT[repository-investigation-009] current finite hint budgets omit whole entries with intact retained content', () => {
  assert.equal(warmStart.maxHintsTotal, 24)
  assert.equal(warmStart.maxWarmStartBytes, 64 * 1024)
  for (const width of [10, 5000]) {
    const hints = Array.from({ length: 32 }, (_, index) => hint(`src/${index}.fs`, `${index}:` + '界'.repeat(width)))
    const result = warmStart.render(['Inspect.'], 'Inspect.', [searchResult(1, 'query', hints)])
    const data = parseToml(result)
    assert.ok(Buffer.byteLength(result, 'utf8') <= warmStart.maxWarmStartBytes)
    assert.ok(data.repository_hint.length > 0)
    assert.ok(data.repository_hint.length <= warmStart.maxHintsTotal)
    assert.equal(data.repository_hint_omitted, hints.length - data.repository_hint.length)
    assert.deepEqual(data.repository_hint.map((item) => item.content), hints.slice(0, data.repository_hint.length).map((item) => item.content))
  }
})

test('WHAT[repository-investigation-009] oversized base task exposes the unresolved scope of the byte ceiling', { todo: 'GAP-084: base-only fallback and appendix-only budgets differ from a total-prompt ceiling' }, () => {
  const charge = '界'.repeat(warmStart.maxWarmStartBytes)
  const result = warmStart.render([charge], charge, [searchResult(1, 'query', [hint('a.fs', 'hint')])])
  assert.ok(Buffer.byteLength(result, 'utf8') <= warmStart.maxWarmStartBytes)
})

test('WHAT[repository-investigation-009] disabled search returns no candidates and launch selection respects disabled and test modes', async () => {
  const disabled = semble.launchFromVars({ SEMBLE_MCP_DISABLED: 'true', SEMBLE_MCP_FIXTURE: '/tmp/unused.js' })
  assert.equal(disabled.kind, 'Disabled')
  assert.deepEqual(await semble.search(disabled, 'auth', '/repo', 5), [])
  assert.equal(semble.launchFromVars({ WANXIANGSHU_TEST: 'true' }).kind, 'Disabled')
  assert.deepEqual(semble.launchFromVars({ WANXIANGSHU_TEST: 'true', SEMBLE_MCP_FIXTURE: '/tmp/fixture.js' }), { kind: 'Fixture', value: '/tmp/fixture.js' })
  const pinned = semble.launchFromVars({ SEMBLE_MCP_REF: 'release-1' })
  assert.equal(pinned.kind, 'Uvx')
  assert.equal(pinned.value, 'release-1')
  assert.deepEqual(semble.uvxCommand(' v1.2.3 '), ['uvx', '--from', 'semble[mcp] @ git+https://github.com/MinishLab/semble.git@v1.2.3', 'semble'])
  assert.deepEqual(semble.fixtureCommand('/tmp/fixture.js'), ['node', '/tmp/fixture.js'])
})

test.todo('WHAT[repository-investigation-009] actual transport launch failure and timeout fail open and release all process resources; an injected rejected query covers neither (GAP-083)')
