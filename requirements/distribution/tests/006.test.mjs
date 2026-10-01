import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { REPO_ROOT } from '../../../scripts/verify-package.mjs'
import { readText } from '../../../dist/Resources/PackageResources.js'

test('WHAT[distribution-006] production resource loader throws for a missing resource while an existing resource is readable', () => {
  assert.ok(readText('provider/role/manager/en.md').trim().length > 0)
  assert.throws(() => readText('enforcer/does-not-exist-rule/enforcer.md'), /package resource missing/)
  assert.equal(existsSync(join(REPO_ROOT, 'resources/enforcer/catalog.json')), false)
})

test('WHAT[distribution-006] every production resource reader uses the sole infrastructure owner and missing-resource failure reaches its caller', async () => {
  const { readdirSync, readFileSync: read } = await import('node:fs')
  const { join, relative } = await import('node:path')

  // The sole infrastructure owner for semantic resources is
  // Resources/PackageResources.fs; bilingual provider assets chain through
  // ProviderResourceBytes → PackageResources. Every src/ file that reads the
  // packaged `resources/` tree must be one of those owners (ablation JSON
  // manifests are structured configuration, read by their own loader).
  const SRC_ROOT = join(REPO_ROOT, 'src', 'Wanxiangshu')
  const collect = (dir) => {
    const out = []
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) out.push(...collect(full))
      else if (entry.name.endsWith('.fs')) out.push(full)
    }
    return out
  }

  // The Resources/ tree is the infrastructure itself: PackageResources owns
  // physical reads, the other modules are semantic assemblers on top of it.
  // Ablation/Manifest reads its own structured JSON configuration.
  const isOwner = (rel) => rel.startsWith('Resources/') || rel === 'Ablation/Manifest.fs'
  const scanViolators = (files, rootDir = SRC_ROOT) => {
    const found = []
    for (const file of files) {
      const rel = relative(rootDir, file)
      if (isOwner(rel)) continue
      const text = read(file, 'utf8')
      // Reading packaged semantic resources means touching the `resources/`
      // tree or the PackageResources API; physical workspace I/O is unrelated.
      if (/PackageResources\s*\.\s*(readText|exists)/.test(text)) {
        found.push(`${rel}: calls PackageResources directly`)
      }
      if (/["']resources\/(?!ablation)/.test(text) && /readFileSync|readFile\b/.test(text)) {
        found.push(`${rel}: reads packaged resources/ directly`)
      }
    }
    return found
  }
  // Violation oracle (verification-system-004): the scan must actually detect
  // a non-owner that calls PackageResources — an empty list on the current
  // tree is only meaningful if the rule fires on a controlled violation.
  // The probe lives in an isolated temp tree that mirrors the src layout, so
  // the real workspace is never written (a crash cannot leave residue, and
  // parallel verification inputs stay clean).
  const { mkdtempSync, rmSync: rmProbeDir, writeFileSync: writeProbe } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const probeRoot = mkdtempSync(join(tmpdir(), 'wxs-006-probe-'))
  const probeDir = join(probeRoot, 'OpenCode', 'Tools')
  const { mkdirSync: mkdirProbe } = await import('node:fs')
  mkdirProbe(probeDir, { recursive: true })
  const syntheticViolator = join(probeDir, 'synthetic-owner-probe.fs')
  writeProbe(syntheticViolator, 'module Probe =\n    let text = PackageResources.readText "provider/role/manager/en.md"\n')
  try {
    // The scan rule takes explicit source material: the probe tree is passed
    // in with the same relative layout the real scan uses.
    const detected = scanViolators(collect(probeRoot), probeRoot)
    assert.deepEqual(detected, ['OpenCode/Tools/synthetic-owner-probe.fs: calls PackageResources directly'])
    // Isolation evidence: the probe never existed under the real src root.
    const { existsSync: probeExists } = await import('node:fs')
    assert.equal(probeExists(join(SRC_ROOT, 'OpenCode', 'Tools', 'synthetic-owner-probe.fs')), false)
  } finally {
    rmProbeDir(probeRoot, { recursive: true, force: true })
  }
  // Real-tree scan stays read-only.
  const violators = scanViolators(collect(SRC_ROOT))
  assert.deepEqual(violators, [])

  // Missing-resource failure is fatal and reaches the caller: the loader
  // throws instead of returning a built-in fallback (already asserted above
  // for enforcer; repeat for a provider asset through the bilingual chain).
  const { readText: providerReadText } = await import('../../../dist/Participant/Provider/ProviderResources.js')
  assert.throws(() => providerReadText('en', 'does-not-exist/semantic-path'), /missing/i)
})
