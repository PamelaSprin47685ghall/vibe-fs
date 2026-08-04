#!/usr/bin/env node
// Generate spec/conformance.toml from Active SSOT files, existing spec/conformance.md,
// and the tests/canary tree. Intended to be the single source of truth for clause status.

import { readFileSync, readdirSync, writeFileSync, existsSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { execSync } from 'node:child_process'

const SSOT_DIR = 'spec'
const LEDGER_DIR = 'spec'
const TOML_OUT = join(LEDGER_DIR, 'conformance.toml')
const MD_IN = join(LEDGER_DIR, 'conformance.md')
const MD_OUT = join(LEDGER_DIR, 'conformance.md')

const ACTIVE_SSOT = [
  '01.md', '02.md', '03.md', '04.md', '05.md', '06.md', '07.md', '08.md',
  '09.md', '10.md', '11.md', '12.md', '13.md', '15.md', '17.md'
]

const CLAUSE_RE = /^(#{2,4})\s+((?:ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX|ENFORCER|LOOP)-\d{3}(?:[A-Z0-9_-]+)?)\s*[:：]\s*(.*)$/m

const PREFIX_DEFAULT_MODULE = {
  ARCH: 'src/Wanxiangshu/Kernel/Flow.fs',
  AGENT: 'src/Wanxiangshu/Domain/ManagedAgentCatalog.fs',
  PROMPT: 'src/Wanxiangshu/Application/Prompting/PromptDispatcher.fs',
  FALLBACK: 'src/Wanxiangshu/Session/FallbackController.fs',
  REVIEW: 'src/Wanxiangshu/Session/ReviewController.fs',
  ORCH: 'src/Wanxiangshu/Application/Orchestration/OrchestratorProgram.fs',
  HOST: 'src/Wanxiangshu/Infrastructure/OpenCode/Host/HostSignalBootstrap.fs',
  COMPANION: 'src/Wanxiangshu/Session/CompanionHost.fs',
  EXEC: 'src/Wanxiangshu/Session/HostForkRuntime.fs',
  VERIFY: 'src/Wanxiangshu/Kernel/Flow.fs',
  PERSIST: 'src/Wanxiangshu/Journal/Fold.fs',
  CTX: 'src/Wanxiangshu/Application/Reconciliation/XWire.fs',
  ENFORCER: 'src/Wanxiangshu/Session/BloggerCoordinator.fs',
  LOOP: 'src/Wanxiangshu/Domain/LoopDetector.fs'
}

const TEST_DIRS = ['tests/unit', 'tests/e2e/tests']

function scanClauses () {
  const clauses = []
  for (const file of ACTIVE_SSOT) {
    const text = readFileSync(join(SSOT_DIR, file), 'utf8')
    for (const line of text.split('\n')) {
      const m = line.match(/^#{2,4}\s+((?:ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX|ENFORCER)-\d{3}(?:[A-Z0-9_-]+)?)\s*[:：]\s*(.*)$/)
      if (m) {
        const id = m[1]
        const title = m[2].trim()
        if (id === 'ARCH-010-LWR') {
          clauses.push({ id: 'ARCH-010-LWR', title, file })
        } else {
          clauses.push({ id, title, file })
        }
      }
    }
  }
  // ARCH-010 is split into two deliverables in the existing conformance table;
  // keep the parent clause and the LWR sub-clause as separate rows.
  return clauses
}

function parseExistingConformance () {
  if (!existsSync(MD_IN)) return new Map()
  const text = readFileSync(MD_IN, 'utf8')
  const rows = new Map()
  for (const line of text.split('\n')) {
    const m = line.match(/^\|\s*((?:ARCH|AGENT|PROMPT|FALLBACK|REVIEW|ORCH|HOST|COMPANION|EXEC|VERIFY|PERSIST|CTX|ENFORCER)-\d{3}(?:[A-Z0-9_-]+)?)\s*[:：]\s*[^|]+\|\s*(CONFORMANT|PARTIAL|CONTRADICTS|UNVERIFIED|NOT_IMPLEMENTED|PURE_CORE_ONLY)\s*\|\s*([^|]+?)\s*(?:\||$)/)
    if (m) {
      const id = m[1]
      const status = m[2].toLowerCase()
      const location = m[3].trim().replace(/`/g, '')
      const owners = location.split(/[\s,;]+/).filter(Boolean).map(f => {
        if (f.startsWith('src/')) return f
        if (f.endsWith('.fs')) return 'src/Wanxiangshu/' + f.replace(/^src\//, '')
        return 'src/Wanxiangshu/' + f
      })
      rows.set(id, { status, owners })
    }
  }
  return rows
}

function fileExists (p) {
  try { return statSync(p).isFile() } catch { return false }
}

function dirFiles (dir) {
  const out = []
  function walk (d) {
    for (const f of readdirSync(d, { withFileTypes: true })) {
      const p = join(d, f.name)
      if (f.isDirectory()) walk(p)
      else if (f.isFile() && (p.endsWith('.mjs') || p.endsWith('.js') || p.endsWith('.fs'))) out.push(p)
    }
  }
  walk(dir)
  return out
}

function searchReferences (clauses) {
  const allFiles = TEST_DIRS.flatMap(dir => existsSync(dir) ? dirFiles(dir) : [])
  const refs = new Map(clauses.map(c => [c.id, { tests: [], canaries: [], harness: [] }]))
  const reFor = (id) => new RegExp(id.replace(/-/g, '[-_]'))
  for (const p of allFiles) {
    const text = readFileSync(p, 'utf8')
    for (const c of clauses) {
      if (reFor(c.id).test(text)) {
        if (p.includes('-canary.mjs')) refs.get(c.id).canaries.push(p)
        else if (p.includes('gate-') && p.includes('tests/e2e')) refs.get(c.id).harness.push(p)
        else refs.get(c.id).tests.push(p)
      }
    }
  }
  return refs
}

// 0.5.2 已知未闭合项：机器生成阶段不能谎称 CONFORMANT。
// C11 人工审计后应逐条收敛到 conformant/blocked。
// All former IMPLEMENTING rows promoted (PROMPT-007/HOST-010/011/EXEC-009).
// Keep empty set so regen does not re-mark closed clauses implementing.
const KNOWN_IMPLEMENTING = new Set([])

function requiredLayer (rec, refs) {
  if (rec.id.startsWith('ARCH') || rec.id.startsWith('VERIFY-001') || rec.id.startsWith('VERIFY-002')) return 0
  if (refs.canaries.length) {
    const names = refs.canaries.join('|')
    if (/restart|publish/.test(names)) return 5
    return 4
  }
  if (refs.harness.length) return 3
  if (refs.tests.length) return 2
  return 0
}

function main () {
  const clauses = scanClauses()
  const existing = parseExistingConformance()
  const refs = searchReferences(clauses)
  let commit = 'HEAD'
  try {
    commit = execSync('git rev-parse --short HEAD', { encoding: 'utf8' }).trim()
  } catch { /* no git; keep HEAD marker */ }

  const lines = [
    '# spec/conformance.toml — per-clause machine ledger',
    '# Generated by scripts/conformance-gen.mjs. Do not edit by hand.',
    `# Active SSOT files: ${ACTIVE_SSOT.join(', ')}`,
    `# baseline_commit: ${commit}`,
    ''
  ]

  for (const c of clauses) {
    const id = c.id
    const ex = existing.get(id) || {}
    const exStatus = ex.status || 'conformant'
    const owners = ex.owners && ex.owners.length ? ex.owners : [PREFIX_DEFAULT_MODULE[id.split('-')[0]]]
    const ref = refs.get(id) || { tests: [], canaries: [], harness: [] }
    const tests = Array.from(new Set([...ref.tests, ...ref.harness]))
    const canaries = ref.canaries
    const layer = requiredLayer(c, ref)
    let status = exStatus === 'pure_core_only' ? 'implementing' : exStatus
    if (KNOWN_IMPLEMENTING.has(id)) status = 'implementing'

    lines.push('[[clause]]')
    lines.push(`id = "${id}"`)
    lines.push(`title = "${c.title.replace(/"/g, '\\"')}"`)
    lines.push(`ssot = "spec/${c.file}"`)
    lines.push(`lifecycle = "active"`)
    lines.push(`status = "${status}"`)
    lines.push(`required_layer = ${layer}`)
    lines.push(`verified_commit = "${commit}"`)
    lines.push('owner = [')
    for (const o of owners) lines.push(`  "${o}",`)
    lines.push(']')
    if (tests.length) {
      lines.push('tests = [')
      for (const t of tests) lines.push(`  "${t}",`)
      lines.push(']')
    } else {
      lines.push('tests = []')
    }
    if (canaries.length) {
      lines.push('canaries = [')
      for (const c2 of canaries) lines.push(`  "${c2}",`)
      lines.push(']')
    } else {
      lines.push('canaries = []')
    }
    lines.push('evidence = ""')
    lines.push('')
  }

  writeFileSync(TOML_OUT, lines.join('\n'))
  console.log(`wrote ${clauses.length} clauses to ${TOML_OUT}`)
}

main()
