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

test('WHAT[REPOSITORY-INVESTIGATION-008] AGENT_032_nonconsumer_nonempty_keywords_fail_and_missing_workspace_skips', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-warm-start-role-'))
  let calls = 0
  const searchFn = async () => {
    calls += 1
    return []
  }

  try {
    const denied = await warmStart.prepareWithSearch(searchFn, sid, 'Browser', root, 'repo', 'raw charge')
    assert.equal(denied.ok, false)
    assert.match(denied.error, /only available to Engineer or DevOps/)
    assert.equal(calls, 0)

    const noWorkspace = await warmStart.appendToBaseWithSearch(searchFn, sid, 'Engineer', undefined, 'repo', 'base')
    assert.deepEqual(noWorkspace, { ok: true, value: '# base\n' })
    assert.equal(calls, 0)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
