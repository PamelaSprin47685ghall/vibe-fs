#!/usr/bin/env node
// spec/ pure-text contract checks.
//
// Checks:
//   1. Clause IDs defined once
//   2. References must resolve
//   3. Prefix ownership (PREFIX_OWNER)
//   4. spec/00.md navigates all active files and prefixes
//
// Usage: node scripts/checks/spec.mjs

import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const SSOT_DIR = 'spec'

/** Active prefix → owning file. Hard-coded by design: part of the contract. */
const PREFIX_OWNER = {
  ARCH: '01.md',
  AGENT: '02.md',
  PROMPT: '03.md',
  FALLBACK: '04.md',
  REVIEW: '05.md',
  ORCH: '06.md',
  HOST: '07.md',
  COMPANION: '08.md',
  EXEC: '09.md',
  VERIFY: '10.md',
  PERSIST: '11.md',
  CTX: '12.md',
  ENFORCER: '15.md',
  LOOP: '17.md',
}

const PREFIX_ALTERNATION = Object.keys(PREFIX_OWNER).join('|')
const CLAUSE_RE = new RegExp(`\\b(${PREFIX_ALTERNATION})-(\\d{3})\\b`, 'g')
const DEFINITION_RE = new RegExp(`^##\\s+((?:${PREFIX_ALTERNATION})-\\d{3})\\b`, 'gm')

const failures = []
const fail = (file, line, msg) => failures.push({ file, line, msg })

const files = readdirSync(SSOT_DIR)
  .filter((f) => /^\d{2}\.md$/.test(f))
  .sort()

/** @type {Map<string, {file: string, line: number}>} */
const definitions = new Map()
/** @type {{id: string, file: string, line: number}[]} */
const references = []
const sources = new Map()

for (const file of files) {
  const text = readFileSync(join(SSOT_DIR, file), 'utf8')
  sources.set(file, text)
  const lines = text.split('\n')

  for (const match of text.matchAll(DEFINITION_RE)) {
    const id = match[1]
    const line = text.slice(0, match.index).split('\n').length
    const previous = definitions.get(id)
    if (previous) {
      fail(file, line, `条款 ID 重复定义：${id}（已在 ${previous.file}:${previous.line} 定义）`)
      continue
    }
    definitions.set(id, { file, line })

    const prefix = id.split('-')[0]
    const owner = PREFIX_OWNER[prefix]
    if (owner && file !== owner) {
      fail(file, line, `条款 ${id} 定义在 ${file}，但 PREFIX_OWNER 规定 ${prefix}- 属于 spec/${owner}`)
    }
  }

  lines.forEach((content, index) => {
    for (const match of content.matchAll(CLAUSE_RE)) {
      references.push({ id: match[0], file, line: index + 1 })
    }
  })
}

for (const { id, file, line } of references) {
  if (!definitions.has(id)) {
    fail(file, line, `悬空条款引用：${id} 无定义`)
  }
}

// spec/00 must list every active numbered file
const navigation = sources.get('00.md') ?? ''
for (const file of files) {
  if (file === '00.md') continue
  if (!navigation.includes(`spec/${file}`)) {
    fail('00.md', 0, `导航索引缺少 spec/${file}`)
  }
}

// every active prefix must appear in 00.md as `PREFIX-`
for (const prefix of Object.keys(PREFIX_OWNER)) {
  if (!navigation.includes(`\`${prefix}-\``)) {
    fail('00.md', 0, `导航索引缺少条款前缀 ${prefix}-`)
  }
}

const definedCount = definitions.size
if (failures.length === 0) {
  console.log(`spec-check: OK — ${definedCount} 条款，${references.length} 处引用，${files.length} 个文件`)
  process.exit(0)
}

console.error(`spec-check: ${failures.length} 处问题`)
for (const { file, line, msg } of failures) {
  console.error(`  spec/${file}:${line}  ${msg}`)
}
process.exit(1)
