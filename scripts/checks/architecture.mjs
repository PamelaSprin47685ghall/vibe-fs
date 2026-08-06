#!/usr/bin/env node
// Focused architecture checks for VERIFY-005 layer 0.
// Usage: node scripts/checks/architecture.mjs

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'
const SRC_ROOT = 'src'
const FSPROJ = 'src/Wanxiangshu/Wanxiangshu.fsproj'
const PURE_DIRS = [`${PRODUCTION_ROOT}/Kernel/`, `${PRODUCTION_ROOT}/Domain/`]
const RESOURCE_DIR = `${PRODUCTION_ROOT}/Infrastructure/Resources/`
const UPPER_NAMESPACES = ['Wanxiangshu.OpenCode', 'Wanxiangshu.Session', 'Wanxiangshu.Process']
const LEGACY_TOKENS = [
  'docs/evidence',
  'docs/archive',
  'SSOT/',
  'STATUS/',
  'vibe-fs',
  'tests-mjs',
  'testkit',
  'Wanxiangshu.Next',
]
const HOST_SOURCE_PATH = /(?:\.\.\/opencode|packages)\/[\w.-]+(?:\/[\w.-]+)*\/src\//
const PACKAGE_RESOURCE_READ = /PackageResources\./

const violations = []
const fail = (gate, message) => violations.push({ gate, message })
const norm = (path) => path.replace(/\\/g, '/')
const isFs = (path) => path.endsWith('.fs')

const sources = new Map()
const read = (path) => {
  if (!sources.has(path)) sources.set(path, readFileSync(path, 'utf8'))
  return sources.get(path)
}

if (!existsSync(PRODUCTION_ROOT)) {
  console.error(`architecture: required directory '${PRODUCTION_ROOT}' does not exist`)
  process.exit(1)
}

const productionFiles = walk(PRODUCTION_ROOT, ['.fs', '.fsproj'])
const productionFs = productionFiles.filter(isFs).map(norm)

// ① sole F# source root under src/
{
  if (!existsSync(SRC_ROOT) || !statSync(SRC_ROOT).isDirectory()) {
    fail('source-root', `${SRC_ROOT}/ missing`)
  } else {
    for (const entry of readdirSync(SRC_ROOT, { withFileTypes: true })) {
      const full = join(SRC_ROOT, entry.name)
      if (entry.isDirectory()) {
        if (entry.name !== 'Wanxiangshu') {
          const stray = walk(full, ['.fs', '.fsproj'])
          if (stray.length > 0) {
            fail('source-root', `${full}/ contains F# sources; only ${PRODUCTION_ROOT}/ is allowed`)
          }
        }
      } else if (entry.name.endsWith('.fs') || entry.name.endsWith('.fsproj')) {
        fail('source-root', `${full}: F# source outside ${PRODUCTION_ROOT}/`)
      }
    }
  }
  const outside = walk(SRC_ROOT, ['.fs', '.fsproj']).filter((file) => {
    const rel = norm(relative('.', file))
    return rel !== PRODUCTION_ROOT && !rel.startsWith(`${PRODUCTION_ROOT}/`)
  })
  for (const file of outside) fail('source-root', `${norm(file)}: F# source outside ${PRODUCTION_ROOT}/`)
}

// ② each .fs compiled exactly once; ③ no missing declared files
{
  if (!existsSync(FSPROJ)) {
    fail('fsproj-drift', `${FSPROJ} does not exist`)
  } else {
    const text = read(FSPROJ)
    const declared = [...text.matchAll(/Include="([^"]+\.fs)"/g)].map((m) => norm(`${PRODUCTION_ROOT}/${m[1]}`))
    const counts = new Map()
    for (const path of declared) counts.set(path, (counts.get(path) ?? 0) + 1)
    const onDisk = new Set(productionFs)

    for (const [path, n] of counts) {
      if (n > 1) fail('fsproj-drift', `${FSPROJ}: '${path}' declared ${n} times`)
      if (!onDisk.has(path)) fail('fsproj-drift', `${FSPROJ}: declares '${path}' which does not exist`)
    }
    for (const path of onDisk) {
      if (!counts.has(path)) fail('fsproj-drift', `${path}: on disk but not compiled by ${FSPROJ}`)
    }
  }
}

// ④ Kernel/Domain must not reference upper infrastructure namespaces
for (const file of productionFs) {
  if (!PURE_DIRS.some((dir) => file.startsWith(dir))) continue
  const text = read(file)
  for (const upper of UPPER_NAMESPACES) {
    if (text.includes(upper)) fail('dependency-direction', `${file}: pure core references '${upper}'`)
  }
}

// ⑤ Kernel/Domain must not use Fable.Core.JsInterop
for (const file of productionFs) {
  if (!PURE_DIRS.some((dir) => file.startsWith(dir))) continue
  if (read(file).includes('Fable.Core.JsInterop')) {
    fail('host-boundary', `${file}: pure core must not use Fable.Core.JsInterop`)
  }
}

// ⑥ package resource reads only under Infrastructure/Resources/
for (const file of productionFs) {
  if (file.startsWith(RESOURCE_DIR)) continue
  if (PACKAGE_RESOURCE_READ.test(read(file))) {
    fail('resource-boundary', `${file}: package resource I/O must live under ${RESOURCE_DIR}`)
  }
}

// ⑦ no generated F# sources
for (const file of productionFiles) {
  if (norm(file).endsWith('.gen.fs')) fail('no-gen-fs', `${norm(file)}: generated F# is forbidden`)
}

// ⑧ no legacy paths / names
const referencesLegacySrc = (text) => {
  const withoutHostCitations = text.replace(new RegExp(HOST_SOURCE_PATH.source, 'g'), '')
  return (
    withoutHostCitations.includes('../src') ||
    withoutHostCitations.includes('..\\src') ||
    withoutHostCitations.includes('/src/') ||
    withoutHostCitations.includes('\\src\\')
  )
}

for (const file of productionFs) {
  const text = read(file)
  if (referencesLegacySrc(text)) fail('legacy-vocabulary', `${file}: forbidden reference to legacy src path`)
  for (const token of LEGACY_TOKENS) {
    if (text.includes(token)) fail('legacy-vocabulary', `${file}: forbidden legacy token '${token}'`)
  }
}

// ⑨ RECOVERY-FAMILY: no local recovery-gate bypass; constructor must not start restore.
{
  const forbiddenCallers = [
    'PromptRecovery.RecoveryGate',
    'BloggerCrashRecovery.RecoveryGate',
    'AttachRecoveryGate',
    'AttachBloggerRecoveryGate',
  ]
  for (const file of productionFs) {
    const text = read(file)
    // Domain DSL may name RecoveryGate only as history; production wiring must not.
    if (file.includes('/Domain/SessionRecovery.fs')) continue
    for (const token of forbiddenCallers) {
      if (text.includes(token)) {
        fail('recovery-family', `${file}: forbidden local recovery gate '${token}'`)
      }
    }
  }

  const forkRuntime = `${PRODUCTION_ROOT}/Session/HostForkRuntime.fs`
  if (existsSync(forkRuntime)) {
    const text = read(forkRuntime)
    if (/do\s+recoveryTask\s*<-\s*restoreChildren/.test(text)) {
      fail('recovery-family', `${forkRuntime}: constructor must not start restoreChildren`)
    }
    // GREEN-4: second recovery ownership must not reappear (code only; comments ok).
    const codeOnly = text
      .split('\n')
      .filter((l) => !/^\s*\/\//.test(l) && !/^\s*\*/.test(l))
      .join('\n')
    if (/\brecoveryTask\b/.test(codeOnly)) {
      fail('recovery-family', `${forkRuntime}: recoveryTask must not exist (SessionRecoveryWorkflow owns restore)`)
    }
    if (/member[^\n]*AwaitRecovery/.test(codeOnly) || /EnsureChildRestoreStarted/.test(codeOnly)) {
      fail('recovery-family', `${forkRuntime}: AwaitRecovery / EnsureChildRestoreStarted deleted (GREEN-4)`)
    }
  }

  const dsl = `${PRODUCTION_ROOT}/Domain/SessionRecovery.fs`
  if (!existsSync(dsl)) {
    fail('recovery-family', `${dsl}: SessionRecovery DSL missing`)
  } else {
    const text = read(dsl)
    if (!/type FamilyRecoveryPermit\s*=\s*\n\s*private/.test(text) && !/FamilyRecoveryPermit\s*=\s*private/.test(text)) {
      fail('recovery-family', `${dsl}: FamilyRecoveryPermit must be private`)
    }
  }
}

if (violations.length === 0) {
  console.log(`architecture: OK — ${productionFs.length} 文件`)
  process.exit(0)
}

const byGate = new Map()
for (const { gate, message } of violations) {
  if (!byGate.has(gate)) byGate.set(gate, [])
  byGate.get(gate).push(message)
}

console.error(`architecture: ${violations.length} violation(s) — ${productionFs.length} 文件\n`)
for (const [gate, messages] of byGate) {
  console.error(`${gate} (${messages.length})`)
  for (const message of messages) console.error(`  ${message}`)
  console.error('')
}
process.exit(1)
