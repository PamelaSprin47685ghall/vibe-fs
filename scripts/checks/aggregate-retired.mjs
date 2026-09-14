#!/usr/bin/env node
// WP6 regression gate: the legacy Wanxiangshu.fsproj must stay gone — every
// production consumer must reach only the compile-shard graph via the
// canonical inventory, never re-read the wrapper. Re-adding the file, letting
// a check read its bytes, or hard-coding the aggregate path back into the
// standard script tree must all vacate the gate red.
// Usage: node scripts/checks/aggregate-retired.mjs

import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const AGGREGATE_PATH = 'src/Wanxiangshu/Wanxiangshu.fsproj'
const ROOT_ROOT = resolve(fileURLToPath(new URL('.', import.meta.url)), '..', '..')

const norm = (value) => value.replace(/\\/g, '/')

function collectScriptPaths(root) {
  const paths = []
  const visit = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) {
        if (entry.name === 'node_modules' || entry.name.startsWith('.')) continue
        visit(full)
      } else if (entry.name.endsWith('.mjs')) {
        paths.push(norm(full))
      }
    }
  }
  for (const dir of [join(root, 'scripts'), join(root, 'scripts/checks'), join(root, 'scripts/lib')]) {
    if (existsSync(dir)) visit(dir)
  }
  return paths
}

export function check(ctx = {}) {
  const root = ctx.root ?? ROOT_ROOT
  const issues = []

  // ① The file itself must not come back. Anybody restoring it resurrects
  // the two-source-of-truth problem the cutover eliminated.
  if (existsSync(join(root, AGGREGATE_PATH))) {
    issues.push({
      code: 'aggregate-resurrected',
      message: `${AGGREGATE_PATH}: wrapper aggregate fsproj restored — delete it; the compile-order manifest + shard graph own the production source list`,
    })
  }

  // ② Production scripts must not hard-code the wrapper's path; a stale CLI
  // option or unconditional read is a fallback path waiting to trigger.
  const forbiddenPatterns = [
    /Wanxiangshu\.fsproj\b/,
  ]

  for (const scriptPath of collectScriptPaths(root)) {
    if (!existsSync(scriptPath)) continue
    if (scriptPath.endsWith('aggregate-retired.mjs')) continue
    const text = readFileSync(scriptPath, 'utf8')
    for (const pattern of forbiddenPatterns) {
      const match = pattern.exec(text)
      if (match) {
        issues.push({
          code: 'aggregate-access',
          path: scriptPath,
          message: `script references the retired wrapper aggregate (pattern ${pattern})`,
        })
      }
    }
  }

  // ③ The canonical order manifest must exist and claim all shard-declared
  // sources — a manifest that skips files silently masking drift.
  const manifestPath = join(root, 'src/Wanxiangshu/compile-order.txt')
  if (!existsSync(manifestPath)) {
    issues.push({
      code: 'order-manifest-missing',
      message: 'src/Wanxiangshu/compile-order.txt is missing — the canonical compile order must be declared next to the shards',
    })
  } else {
    const orderText = readFileSync(manifestPath, 'utf8')
    const orderedPaths = orderText.split(/\r?\n/).map((line) => line.trim()).filter((line) => line && !line.startsWith('#'))
    const seen = new Set(orderedPaths)
    if (orderedPaths.length === 0) {
      issues.push({ code: 'order-manifest-empty', message: 'compile-order.txt declares no sources' })
    }
    // Every shard-declared .fs must appear; an omitted file is drift.
    const shardRoot = join(root, 'src/Wanxiangshu')
    const declaredMissing = []
    if (existsSync(shardRoot)) {
      for (const entry of readdirSync(shardRoot)) {
        if (!entry.startsWith('Wanxiangshu.Owner.') && !entry.startsWith('Wanxiangshu.Shard.')) continue
        if (!entry.endsWith('.fsproj')) continue
        const text = readFileSync(join(shardRoot, entry), 'utf8')
        for (const m of text.matchAll(/<Compile\s+Include="([^"]+\.fs)"/g)) {
          const rel = m[1].replace(/\\/g, '/')
          if (!seen.has(rel)) declaredMissing.push(rel)
        }
      }
    }
    if (declaredMissing.length > 0) {
      issues.push({
        code: 'order-manifest-drift',
        message: `compile-order.txt omits ${declaredMissing.length} shard-declared sources: ${declaredMissing.slice(0, 5).join(', ')}`,
      })
    }
  }

  return { issues }
}

export function main() {
  const result = check({ root: process.cwd() })
  if (result.issues.length === 0) {
    console.log('aggregate-retired: OK — no active pipeline can reach the deleted wrapper')
    process.exit(0)
  }
  for (const issue of result.issues) {
    console.error(`[${issue.code}] ${issue.path ? issue.path + ': ' : ''}${issue.message}`)
  }
  process.exit(1)
}

const isEntry = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]
if (isEntry) main()
