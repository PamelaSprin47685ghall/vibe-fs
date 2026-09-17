import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'
import { validateModuleLinkage } from '../../../scripts/checks/js-module-linkage.mjs'
import { validateSurfaceManifest } from '../../../scripts/checks/js-surface-manifest.mjs'
import {
  BUILD_VERIFICATION_FILES,
  SURFACE_MANIFEST,
  scanAll,
  semanticImportEdges,
  semanticTestFiles,
} from '../../../scripts/lib/test-surface-scan.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), '../../..'))

const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const relativePath = (path) => relative(process.cwd(), path).replace(/\\/g, '/')

const distImport = (prefix, module) => `${prefix}dist/${module}`

const wholeScan = scanAll(join(ROOT, 'requirements'))

const wholeSemanticFiles = new Set(semanticTestFiles(join(ROOT, 'requirements')).map(relativePath))

const wholeSemanticImportEdges = semanticImportEdges(join(ROOT, 'requirements'))

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_law_owner_surface_registry', () => {
  assert.ok(SURFACE_MANIFEST.length > 0)
  const failures = validateSurfaceManifest(SURFACE_MANIFEST, ROOT)
  assert.deepEqual(failures, [], failures.join('\n'))
})

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_manifest_rejects_unemitted_or_invalid_evidence', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-surface-manifest-'))
  const ownerWhat = join(temporaryRoot, 'requirements', 'owner', 'WHAT.md')
    const source = join(temporaryRoot, 'src', 'Wanxiangshu', 'Owner', 'Surface.fs')
  const fsproj = join(temporaryRoot, 'src', 'Wanxiangshu', 'Wanxiangshu.fsproj')
  const dist = join(temporaryRoot, 'dist', 'Owner', 'Surface.js')
  const testFile = join(temporaryRoot, 'requirements', 'owner', 'tests', 'surface.test.mjs')
  mkdirSync(dirname(ownerWhat), { recursive: true })
  mkdirSync(dirname(source), { recursive: true })
  mkdirSync(dirname(dist), { recursive: true })
  mkdirSync(dirname(testFile), { recursive: true })

  try {
    writeFileSync(ownerWhat, '# OWNER-001\n')
        writeFileSync(source, 'module Owner.Surface\n')
    writeFileSync(fsproj, '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
    writeFileSync(dist, 'export const value = 1\n')
    writeFileSync(testFile, [
      "import test from 'node:test'",
      `import * as surface from '${distImport('../../../', 'Owner/Surface.js')}'`,
      "test('WHAT[OWNER-001] owner behavior', () => { assert.equal(surface.value, 1) })",
    ].join('\n'))
    const entry = {
      module: 'Owner/Surface.js',
      owner: 'owner',
      laws: ['OWNER-001'],
      source: 'src/Wanxiangshu/Owner/Surface.fs',
      representation: 'json',
      kind: 'pure',
    }

    assert.deepEqual(validateSurfaceManifest([entry], temporaryRoot), [])

    rmSync(dist)
    assert.match(validateSurfaceManifest([entry], temporaryRoot).join('\n'), /missing emitted surface/)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})
