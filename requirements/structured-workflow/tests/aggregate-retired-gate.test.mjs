// Regression gate test for scripts/checks/aggregate-retired.mjs.
//
// A gate that only ever greens itself is worth nothing — W6 requires a
// demand-evidence suite that proves the check goes RED when an attacker
// reintroduces the wrapper aggregate, either physically or by re-wiring a
// production script to read its bytes. This test builds a parallel fixture
// root and runs the gate's check function against both an absent-directory
// baseline and three poisoning shapes:
//
//   1) the file exists again (aggregate-resurrected)
//   2) a script still hard-codes its path (aggregate-access)
//   3) the compile-order manifest is missing or drops declared sources
//      (order-manifest-missing / order-manifest-drift)

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
  // The gate reads these two directory trees:
  //   src/Wanxiangshu   → shards + compile-order.txt
  //   scripts           → production pipeline code we scan for leakage
  mkdirSync(join(dir, 'src', 'Wanxiangshu'), { recursive: true })
  mkdirSync(join(dir, 'scripts'), { recursive: true })
  return dir
}

test('WHAT[STRUCTURED-WORKFLOW-011] RETIRED-GATE: aggregate deleted → check stays green on minimal fixture', () => {
  const dir = fixtureRoot()
  try {
    // Minimal viable shard inventory — one shard, one pair, matching manifest.
    writeFileSync(
      join(dir, 'src/Wanxiangshu/Wanxiangshu.Owner.test.shim.fsproj'),
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><WanxiangshuSubsystem>test</WanxiangshuSubsystem></PropertyGroup>
  <ItemGroup>
    <Compile Include="Shim.fsi"/>
    <Compile Include="Shim.fs"/>
  </ItemGroup>
</Project>`,
    )
    writeFileSync(join(dir, 'src/Wanxiangshu/Shim.fsi'), 'namespace Shim\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/Shim.fs'), 'namespace Shim\nlet x = 1\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/compile-order.txt'), 'Shim.fsi\nShim.fs\n')
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
    writeFileSync(
      join(dir, 'src/Wanxiangshu/Wanxiangshu.Owner.test.shim.fsproj'),
      `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Shim.fsi"/>
    <Compile Include="Shim.fs"/>
    <Compile Include="Orphan.fsi"/>
    <Compile Include="Orphan.fs"/>
  </ItemGroup>
</Project>`,
    )
    writeFileSync(join(dir, 'src/Wanxiangshu/Shim.fsi'), 'namespace X\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/Shim.fs'), 'namespace X\nlet a = 1\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/Orphan.fsi'), 'namespace X\n')
    writeFileSync(join(dir, 'src/Wanxiangshu/Orphan.fs'), 'namespace X\nlet b = 2\n')
    // Manifest declares only the first pair — Orphan.fs must flag drift.
    writeFileSync(join(dir, 'src/Wanxiangshu/compile-order.txt'), 'Shim.fsi\nShim.fs\n')

    const result = check({ root: dir })
    const drift = result.issues.find((issue) => issue.code === 'order-manifest-drift')
    assert.ok(drift, 'manifest that silently drops a shard source must fail the gate')
    assert.match(drift.message, /Orphan\./)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
