import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import * as warmStart from '../../../dist/Repository/Investigation/WarmStartSurface.js'

const here = dirname(fileURLToPath(import.meta.url))
const providerRoot = join(here, '../../../resources/provider')
const hint = (ordinal, rank, file, content, score = 0.9) => ({
  keywordOrdinal: ordinal,
  localRank: rank,
  filePath: file,
  startLine: rank,
  endLine: rank + 2,
  content,
  score,
  totalLines: 100,
})

const search = (ordinal, query, hints) => ({ ordinal, query, hints })

const readLines = (semanticPath, replacements = {}) => {
  let text = readFileSync(join(providerRoot, semanticPath, 'en.md'), 'utf8')
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .trimEnd()
  for (const key in replacements) {
    text = text.replaceAll(`{{${key}}}`, replacements[key])
  }
  return text.split('\n')
}
const renderCharge = (charge, searches) =>
  warmStart.render(readLines('lifecycle/warm-start/charge-envelope', { charge }), charge, searches)
const appendAppendix = (base, searches) =>
  warmStart.appendToProviderPrompt(readLines('lifecycle/warm-start/appendix'), base, searches)
const sid = 'ses_warm_start'

const waitFor = async (predicate, message, ms = 1500) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

test('WHAT[REPOSITORY-INVESTIGATION-009] AGENT_032_renderer_enforces_24_hint_and_64KiB_bounds_by_whole_entries', () => {
  assert.equal(warmStart.maxHintsTotal, 24)
  assert.equal(warmStart.maxWarmStartBytes, 64 * 1024)

  const huge = Array.from({ length: 32 }, (_, i) => hint(1, i + 1, `src/${i}.fs`, `${i}:` + '界'.repeat(5000)))
  const rendered = renderCharge('charge', [search(1, 'wide', huge)])
  const parsed = parseToml(rendered)
  const bytes = Buffer.byteLength(rendered, 'utf8')

  assert.ok(bytes <= warmStart.maxWarmStartBytes, `warm start was ${bytes} bytes`)
  assert.ok((parsed.repository_hint?.length ?? 0) <= warmStart.maxHintsTotal)
  assert.ok(parsed.repository_hint_omitted > 0)
})

test('WHAT[REPOSITORY-INVESTIGATION-009] AGENT_032_append_composes_authoritative_instruction_before_reference_hints', () => {
  const base = 'authoritative assignment'
  const rendered = appendAppendix(base, [search(1, 'q', [hint(1, 1, 'src/a.fs', 'orientation')])])

  assert.ok(rendered.startsWith('# authoritative assignment\n'))
  const parsed = parseToml(rendered)
  assert.equal(parsed.repository_hint[0].content, 'orientation')
})

test('WHAT[REPOSITORY-INVESTIGATION-009] AGENT_032_searches_all_independent_keywords_in_one_parallel_wave_and_restores_ordinal_order', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-warm-start-'))
  let release
  const gate = new Promise((resolve) => { release = resolve })
  const started = []

  const searchFn = async (query, repo, topK) => {
    assert.equal(repo, root)
    assert.equal(topK, warmStart.topKPerKeyword)
    started.push(query)
    await gate
    if (query === 'broken') throw new Error('one shard failed')
    return [{
      filePath: `src/${query}.fs`,
      startLine: 1,
      endLine: 3,
      content: `hit:${query}`,
      score: 0.9,
      totalLines: 20,
    }]
  }

  try {
    const pending = warmStart.prepareWithSearch(
      searchFn,
      sid,
      'Inspector',
      root,
      'slow\nbroken\nfast',
      'inspect charge',
    )

    await waitFor(() => started.length === 3, 'queries did not start as one wave')
    assert.deepEqual(new Set(started), new Set(['slow', 'broken', 'fast']))
    release()

    const result = await pending
    assert.equal(result.ok, true, result.error)
    const parsed = parseToml(result.value)
    assert.deepEqual(parsed.repository_search.map((x) => x.ordinal), [1, 2, 3])
    assert.deepEqual(parsed.repository_search.map((x) => x.candidate_count), [1, 0, 1])
    assert.deepEqual(parsed.repository_hint.map((x) => x.keyword_ordinal), [1, 3])
  } finally {
    release?.()
    rmSync(root, { recursive: true, force: true })
  }
})
