#!/usr/bin/env node
// Focused architecture checks for verification-system-005 layer 0.
// Usage: node scripts/checks/architecture.mjs

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'
const SRC_ROOT = 'src'
const OWNER_FSPROJ = /^src\/Wanxiangshu\/Wanxiangshu\.(?:Owner|Shard)\..+\.fsproj$/
const PURE_DIRS = [`${PRODUCTION_ROOT}/Foundation/`]
const RESOURCE_DIR = `${PRODUCTION_ROOT}/Resources/`
const UPPER_NAMESPACES = ['Wanxiangshu.OpenCode', 'Wanxiangshu.Session', 'Wanxiangshu.Process']
const PACKAGE_RESOURCE_READ = /PackageResources\./

const norm = (path) => path.replace(/\\/g, '/')
const isFs = (path) => path.endsWith('.fs')

export function scanArchitecture(productionFiles, read) {
  const violations = []
  const fail = (gate, message) => violations.push({ gate, message })

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

  // ② each .fs belongs to exactly one owner-locality project; flattened Fable emit mirrors the same set.
  {
    // W5 cutover: the wrapper aggregate is deleted and the compile-order
    // manifest carries the canonical list — this gate only needs to assert
    // every on-disk .fs has exactly one owning shard.
    const ownerProjects = productionFiles.filter((file) => OWNER_FSPROJ.test(norm(file)))
    const declared = ownerProjects.flatMap((project) => {
      const text = read(project)
      return [...text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/?\s*>/g)].map((m) =>
        norm(`${PRODUCTION_ROOT}/${m[1]}`),
      )
    })
    const counts = new Map()
    for (const path of declared) counts.set(path, (counts.get(path) ?? 0) + 1)
    const onDisk = new Set(productionFs)
    for (const [path, n] of counts) {
      if (n > 1) fail('fsproj-drift', `${path}: compiled by ${n} owner-locality projects`)
      if (!onDisk.has(path)) fail('fsproj-drift', `owner-locality project declares '${path}' which does not exist`)
    }
    for (const path of onDisk) {
      if (!counts.has(path)) fail('fsproj-drift', `${path}: on disk but not compiled by an owner-locality project`)
    }
  }

  // ⑤ Kernel/Domain must not reference upper infrastructure namespaces
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

  return { violations, productionFsCount: productionFs.length }
}

export function check(context) {
  const root = context?.root ?? process.cwd()
  const read = (path) => {
    if (context && typeof context.readText === 'function') return context.readText(path)
    if (!sources.has(path)) sources.set(path, readFileSync(path, 'utf8'))
    return sources.get(path)
  }
  const sources = new Map()

  if (!existsSync(join(root, PRODUCTION_ROOT))) {
    return {
      issues: [
        {
          code: 'architecture-root-missing',
          message: `architecture: required directory '${PRODUCTION_ROOT}' does not exist`,
        },
      ],
    }
  }

  const productionFiles = context?.sourceFiles
    ? context.sourceFiles()
    : walk(join(root, PRODUCTION_ROOT), ['.fs', '.fsproj']).map((p) => relative(root, p))
  const { violations } = scanArchitecture(productionFiles, read)

  const issues = violations.map(({ gate, message }) => ({
    code: gate,
    message,
  }))

  return { issues }
}

function runCli() {
  const result = check({ root: process.cwd() })
  if (result.issues.length === 0) {
    console.log('architecture: OK')
    process.exit(0)
  }

  const byGate = new Map()
  for (const { code, message } of result.issues) {
    if (!byGate.has(code)) byGate.set(code, [])
    byGate.get(code).push(message)
  }

  console.error(`architecture: ${result.issues.length} violation(s)\n`)
  for (const [gate, messages] of byGate) {
    console.error(`${gate} (${messages.length})`)
    for (const message of messages) console.error(`  ${message}`)
    console.error('')
  }
  process.exit(1)
}

const isEntry = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]
if (isEntry) {
  runCli()
}
