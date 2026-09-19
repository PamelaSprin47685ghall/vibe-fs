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

test('WHAT[repository-investigation-007] AGENT_032_keywords_normalize_stable_exact_dedupe_and_cap_at_eight', () => {
  const raw = ' alpha\r\n\r\nbeta\nalpha\nAlpha\n gamma \n d\n e\n f\n g\n h\n i\n'
  assert.equal(warmStart.maxKeywords, 8)
  assert.deepEqual(warmStart.normalizeKeywords(raw), ['alpha', 'beta', 'Alpha', 'gamma', 'd', 'e', 'f', 'g'])
})

test('WHAT[repository-investigation-007] AGENT_032_zero_keywords_is_byte_exact_zero_work', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-warm-start-role-'))
  let calls = 0
  const searchFn = async () => {
    calls += 1
    return []
  }

  try {
    const zero = await warmStart.prepareWithSearch(searchFn, sid, 'Blogger', root, ' \r\n ', 'raw charge')
    assert.deepEqual(zero, { ok: true, value: '# raw charge\n' })
    assert.equal(calls, 0)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
