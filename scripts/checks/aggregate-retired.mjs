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

  // ④ Build driver must not silently rest dist — any unconditional clean
  // destroys incremental semantics the spec demands. The only legitimate
  // reset site is gated on `buildMode === 'clean'` (the --clean flag); any
  // other unconditional call would remove the pure-incremental path.
  const buildScript = join(root, 'scripts/build.mjs')
  if (existsSync(buildScript)) {
    const source = readFileSync(buildScript, 'utf8')
    const resetCalls = [...source.matchAll(/resetOutputDirectory\s*\(\s*([^,*)]+)/g)]
    for (const call of resetCalls) {
      const callOffset = call.index ?? 0
      // Look only at the nearest enclosing statement blocks, not arbitrary
      // backwards text — a '--clean' string in a help banner must not
      // satisfy this gate. An indented reset sits inside a guarded block;
      // a top-level unconditional reset is at column 0.
      const lineStart = source.lastIndexOf('\n', callOffset - 1) + 1
      const column = callOffset - lineStart
      const precedingBlock = source.slice(Math.max(0, callOffset - 300), callOffset)
      const guarded =
        column > 0 // inside a block
        && /\bif\s*\(|buildMode\s*===\s*'clean'|--clean\b|clean:\s*true/.test(precedingBlock)
      if (!guarded) {
        issues.push({
          code: 'unconditional-clean',
          path: 'scripts/build.mjs',
          message: `resetOutputDirectory at offset ${callOffset} is not guarded by '--clean' / buildMode='clean'`,
        })
      }
    }
    // ⑤ --plan is the only read-only preview; unknown flags must hard-fail
    // before any side-effect. Check the unknown-arg gate exists.
    if (!/unknown option\(s\)/i.test(source) || !/process\.argv\.slice\(2\)/.test(source)) {
      issues.push({
        code: 'unknown-arg-silent',
        path: 'scripts/build.mjs',
        message: 'build.mjs does not error on unknown argv flags — --plan/--clean must be an exhaustive surface',
      })
    }
  }

  return { issues }
}

export function checkAggregateRetired(rootOrCtx) {
  const ctx = typeof rootOrCtx === 'string' ? { root: rootOrCtx } : (rootOrCtx ?? {})
  const res = check(ctx)
  return { ok: res.issues.length === 0, issues: res.issues }
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
