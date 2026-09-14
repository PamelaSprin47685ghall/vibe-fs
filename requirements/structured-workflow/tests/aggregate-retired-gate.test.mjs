// Regression gate test for scripts/checks/aggregate-retired.mjs.
//
// A gate that only ever greens itself is worth nothing — W6 requires a
// demand-evidence suite that proves the check goes RED when an attacker
// reintroduces the wrapper aggregate, either physically or by re-wiring a
// production script to read its bytes. This test builds a parallel fixture
// root and runs the gate's check function against both an absent-directory
// baseline and poisoning shapes:
//
//   1) the file exists again (aggregate-resurrected)
//   2) a script still hard-codes its path (aggregate-access)
//   3) the compile-order manifest is missing or drops declared sources
//      (order-manifest-missing / order-manifest-drift)
//   4) build.mjs resets dist outside --clean (unconditional-clean)
//   5) build.mjs silently accepts unknown argv (unknown-arg-silent)

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = dirname(fileURLToPath(import.meta.url))
const ReP = join(ROOT, '../../..')

const { check } = await import(join(ReP, 'scripts/checks/aggregate-retired.mjs'))

const fixtureRoot = () => {
  const dir = mkdtempSync(join(tmpdir(), 'aggregate-retired-fixture-'))
  mkdirSync(join(dir, 'src', 'Wanxiangshu'), { recursive: true })
  mkdirSync(join(dir, 'scripts'), { recursive: true })
  return dir
}

const writeShimShard = (dir, extraSources = []) => {
  const sources = ['Shim', ...extraSources]
  writeFileSync(
    join(dir, 'src/Wanxiangshu/Wanxiangshu.Owner.test.shim.fsproj'),
    `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
${sources.flatMap((s) => [`    <Compile Include="${s}.fsi"/>`, `    <Compile Include="${s}.fs"/>`]).join('\n')}
  </ItemGroup>
</Project>`,
  )
  for (const s of sources) {
    writeFileSync(join(dir, 'src/Wanxiangshu', `${s}.fsi`), `namespace ${s}\n`)
    writeFileSync(join(dir, 'src/Wanxiangshu', `${s}.fs`), `namespace ${s}\nlet ${s.toLowerCase()} = 0\n`)
  }
}

const writeManifest = (dir, sources) => {
  writeFileSync(join(dir, 'src/Wanxiangshu/compile-order.txt'), sources.flatMap((s) => [`${s}.fsi`, `${s}.fs`]).join('\n') + '\n')
}

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: aggregate deleted → check stays green on minimal fixture', () => {
  const dir = fixtureRoot()
  try {
    writeShimShard(dir)
    writeManifest(dir, ['Shim'])
    writeFileSync(join(dir, 'scripts/noop.mjs'), '// nothing\n')

    const result = check({ root: dir })
    assert.deepEqual(result.issues, [])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: resurrecting wrapper fsproj → red', () => {
  const dir = fixtureRoot()
  try {
    writeFileSync(join(dir, 'src/Wanxiangshu/Wanxiangshu.fsproj'), '<Project/>\n')
    writeFileSync(join(dir, 'scripts/noop.mjs'), '// nothing\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/compile-order.txt'), '\n')

    const result = check({ root: dir })
    const resurrected = result.issues.find((issue) => issue.code === 'aggregate-resurrected')
    assert.ok(resurrected, 're-introducing the wrapper file must fail the gate')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: a script that hard-codes the aggregate path → red', () => {
  const dir = fixtureRoot()
  try {
    writeFileSync(
      join(dir, 'scripts/build.mjs'),
      `// Script probes the retired path — the gate must flag the call.
import { existsSync } from 'node:fs'
const DEPRECATED = 'src/Wanxiangshu/Wanxiangshu.fsproj'
if (existsSync(DEPRECATED)) process.exit(2)
`,
    )
    writeFileSync(join(dir, 'src/Wanxiangshu/compile-order.txt'), '')

    const result = check({ root: dir })
    const leaked = result.issues.filter((issue) => issue.code === 'aggregate-access')
    assert.ok(leaked.length > 0, 'a script hard-coding the wrapper path must fail the gate')
    assert.ok(
      leaked.some((issue) => (issue.path ?? '').includes('build.mjs')),
      'aggregate-access must name the offending script',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: compile-order manifest missing or drifting → red', () => {
  const dir = fixtureRoot()
  try {
    writeFileSync(join(dir, 'scripts/noop.mjs'), '// nothing\n')
    writeShimShard(dir, ['Orphan'])
    // Manifest declares only Shim — Orphan.fs must flag drift.
    writeManifest(dir, ['Shim'])

    const result = check({ root: dir })
    const drift = result.issues.find((issue) => issue.code === 'order-manifest-drift')
    assert.ok(drift, 'manifest that silently drops a shard source must fail the gate')
    assert.match(drift.message, /Orphan\./)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: unconditional resetOutputDirectory in build.mjs → red', () => {
  const dir = fixtureRoot()
  try {
    writeShimShard(dir)
    writeManifest(dir, ['Shim'])
    // An argv-parsing build.mjs whose reset is NOT behind --clean.
    // Accepting argv cleanly removes the second gate's noise; the only
    // signal here is the unguarded resetOutputDirectory call.
    writeFileSync(
      join(dir, 'scripts/build.mjs'),
      `const unknown = process.argv.slice(2).filter((a) => !['--clean', '--plan', '--help', '-h'].includes(a))
if (unknown.length > 0) { console.error('unknown option(s)'); process.exit(1) }
import { resetOutputDirectory } from './x.mjs'
resetOutputDirectory('dist')
console.log('build done')
`,
    )

    const result = check({ root: dir })
    const red = result.issues.find((issue) => issue.code === 'unconditional-clean')
    assert.ok(red, 'resetOutputDirectory without --clean gating must fail the gate')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: build.mjs that accepts unknown argv → red', () => {
  const dir = fixtureRoot()
  try {
    writeShimShard(dir)
    writeManifest(dir, ['Shim'])
    // A build.mjs that swallows argv entirely — the unknown-arg check must
    // catch the missing strict parse.
    writeFileSync(
      join(dir, 'scripts/build.mjs'),
      `const argv = process.argv.slice(2)
console.log('build done', argv.length)
`,
    )

    const result = check({ root: dir })
    const red = result.issues.find((issue) => issue.code === 'unknown-arg-silent')
    assert.ok(red, 'build.mjs that silently accepts unknown argv must fail the gate')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
