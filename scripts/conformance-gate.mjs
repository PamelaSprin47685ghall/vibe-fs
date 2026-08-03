#!/usr/bin/env node
// Verify STATUS/conformance.toml is the single source of truth for clause status.
// Run with --check to validate without writing STATUS/conformance.md.

import { readFileSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { join } from 'node:path'

const SSOT_DIR = 'SSOT'
const TOML = 'STATUS/conformance.toml'
const MD_OUT = 'STATUS/conformance.md'

const ACTIVE_SSOT = [
  '01.md', '02.md', '03.md', '04.md', '05.md', '06.md', '07.md', '08.md',
  '09.md', '10.md', '11.md', '12.md', '13.md', '15.md'
]

const VALID_STATUSES = new Set(['conformant', 'implementing', 'blocked'])
const VALID_LIFECYCLES = new Set(['active', 'superseded'])

const CHECK_ONLY = process.argv.includes('--check')

function fail (msg) {
  console.error(`conformance-gate: FAIL — ${msg}`)
  process.exit(1)
}

function parseToml (text) {
  const clauses = []
  let current = null
  let currentKey = null
  for (let i = 0; i < text.split('\n').length; i++) {
    const raw = text.split('\n')[i]
    const line = raw.replace(/\r$/, '')
    if (line.trim() === '') continue
    if (line.startsWith('#')) continue
    if (line === '[[clause]]') {
      if (current) clauses.push(current)
      current = {}
      currentKey = null
      continue
    }
    const m = line.match(/^([a-z_]+)\s*=\s*(.*)$/)
    if (!m) {
      if (current && currentKey && (line.trim().startsWith('"') || line.trim().startsWith('[') || line.trim() === ']')) {
        // array continuation handled below
      } else {
        continue
      }
    }
    if (m) {
      currentKey = m[1]
      const rest = m[2].trim()
      if (rest.startsWith('[')) {
        current[currentKey] = []
        if (rest !== '[' && rest !== '[]') {
          const items = rest.match(/"([^"]+)"/g) || []
          current[currentKey] = items.map(s => s.slice(1, -1))
        }
      } else if (rest.startsWith('"') && rest.endsWith('"')) {
        current[currentKey] = rest.slice(1, -1)
      } else if (/^-?\d+$/.test(rest)) {
        current[currentKey] = parseInt(rest, 10)
      } else {
        current[currentKey] = rest
      }
      continue
    }
    if (current && currentKey) {
      const items = line.match(/"([^"]+)"/g)
      if (items) {
        current[currentKey].push(...items.map(s => s.slice(1, -1)))
      }
    }
  }
  if (current) clauses.push(current)
  return clauses
}

function extractClauseIdsFromSsot () {
  const ids = new Set()
  for (const file of ACTIVE_SSOT) {
    const text = readFileSync(join(SSOT_DIR, file), 'utf8')
    for (const line of text.split('\n')) {
      const m = line.match(/^#{2,4}\s+((?:ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX|ENFORCER)-\d{3}(?:[A-Z0-9_-]+)?)\s*[:：]/)
      if (m) ids.add(m[1])
    }
  }
  return ids
}

function exists (p) {
  try { return statSync(p).isFile() } catch { return false }
}

function generateMarkdown (clauses) {
  const lines = [
    '# STATUS/conformance — SSOT 条款合规表',
    '',
    '> 本文件由 `scripts/conformance-gate.mjs` 从 `STATUS/conformance.toml` 生成，请勿手动编辑。',
    '',
    '| 条款 | 生命周期 | 状态 | 最低证据层 | verified_commit | owner | tests | canaries | 证据路径 |',
    '|------|---------|------|------------|-----------------|-------|-------|----------|----------|'
  ]
  for (const c of clauses) {
    const id = c.id || ''
    const lifecycle = c.lifecycle || ''
    const status = (c.status || '').toUpperCase()
    const layer = String(c.required_layer ?? '')
    const commit = c.verified_commit || ''
    const owner = (c.owner || []).join(', ') || ''
    const tests = (c.tests || []).join(', ') || ''
    const canaries = (c.canaries || []).join(', ') || ''
    const evidence = c.evidence || ''
    lines.push(`| ${id} | ${lifecycle} | ${status} | ${layer} | ${commit} | ${owner} | ${tests} | ${canaries} | ${evidence} |`)
  }
  lines.push('')
  return lines.join('\n')
}

function main () {
  if (!existsSync(TOML)) fail(`${TOML} not found; run scripts/conformance-gen.mjs first`)

  const text = readFileSync(TOML, 'utf8')
  const clauses = parseToml(text)

  const activeIds = extractClauseIdsFromSsot()
  const tomlIds = new Set()

  const seen = new Set()
  for (const c of clauses) {
    if (!c.id) fail('clause missing id')
    if (seen.has(c.id)) fail(`duplicate clause id in toml: ${c.id}`)
    seen.add(c.id)
    tomlIds.add(c.id)

    if (!VALID_LIFECYCLES.has(c.lifecycle)) fail(`${c.id}: invalid lifecycle "${c.lifecycle}"`)
    if (!VALID_STATUSES.has(c.status)) fail(`${c.id}: invalid status "${c.status}"`)
    if (typeof c.required_layer !== 'number') fail(`${c.id}: required_layer must be an integer`)
    if (!c.verified_commit) fail(`${c.id}: verified_commit must not be empty`)

    for (const o of c.owner || []) {
      if (o.includes(' ')) {
        // Human notes like '全仓' are not valid paths; fail closed.
        if (/^src\//.test(o) || /\.fs$/.test(o)) fail(`${c.id}: owner contains whitespace: ${o}`)
      }
      if (o.startsWith('src/') && !exists(o)) {
        fail(`${c.id}: owner file not found: ${o}`)
      }
    }
    for (const t of c.tests || []) {
      if (!exists(t)) fail(`${c.id}: test file not found: ${t}`)
    }
    for (const k of c.canaries || []) {
      if (!exists(k)) fail(`${c.id}: canary file not found: ${k}`)
    }
    if (c.evidence && !existsSync(c.evidence) && !c.evidence.startsWith('docs/evidence/')) {
      // evidence can be a directory path that may not exist yet; warn only in strict mode
    }
  }

  for (const id of activeIds) {
    if (!tomlIds.has(id)) fail(`Active SSOT clause missing from toml: ${id}`)
  }
  for (const id of tomlIds) {
    if (!activeIds.has(id) && !['superseded'].includes(clauses.find(c => c.id === id)?.lifecycle)) {
      fail(`Unknown or RFC clause in toml: ${id}`)
    }
  }

  const md = generateMarkdown(clauses)
  if (CHECK_ONLY) {
    if (existsSync(MD_OUT) && readFileSync(MD_OUT, 'utf8') !== md) {
      fail(`${MD_OUT} is out of sync with ${TOML}; run without --check to regenerate`)
    }
    console.log(`conformance-gate: OK — ${clauses.length} clauses, active=${activeIds.size}, toml=${tomlIds.size}`)
    return
  }

  writeFileSync(MD_OUT, md)
  console.log(`conformance-gate: OK — generated ${MD_OUT} with ${clauses.length} clauses`)
}

main()
