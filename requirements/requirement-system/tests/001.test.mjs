import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { duplicateClauseDefinitions, isProposalPath } from '../../../scripts/lib/spec-rules.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')
const INDEX_FILE = join(ROOT, 'requirements/INDEX.md')

const read = (path) => readFileSync(path, 'utf8')

const packageNamesFromIndexTables = () => {
  const text = read(INDEX_FILE)
  const names = []
  for (const line of text.split('\n')) {
    if (!line.startsWith('| ')) continue
    const name = /`([a-z][a-z0-9-]*)`/.exec(line)?.[1]
    if (name) names.push(name)
  }
  return [...new Set(names)]
}

test('WHAT[requirement-system-001] every product truth has exactly one owner package', () => {
  const fromIndex = packageNamesFromIndexTables()
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => !isProposalPath(entry) && statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()

  const unknown = dirs.filter((dir) => !fromIndex.includes(dir))
  assert.deepEqual(unknown, [], `requirements/ must not contain INDEX-external package dirs: ${unknown.join(', ')}`)

  // 全树 WHAT.md 规范命题唯一所有权：任何命题在任意时刻恰归一个包所有，严禁重复定义
  const entries = []
  for (const pkg of fromIndex) {
    const whatFile = join(REQUIREMENTS, pkg, 'WHAT.md')
    try {
      entries.push({ file: whatFile, pkg, text: read(whatFile) })
    } catch {
      // Missing file is governed by [006]/[017]
    }
  }

  const duplicates = duplicateClauseDefinitions(entries)
  assert.deepEqual(duplicates, [], `clause definitions must be globally unique:\n${duplicates.map((d) => d.msg).join('\n')}`)
})
