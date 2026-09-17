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

test('WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_fable_representation_not_contract', () => {
  const existing = [...BUILD_VERIFICATION_FILES].filter((file) => existsSync(join(ROOT, file)))
  assert.ok(existing.length > 0, 'compiler/build quarantine must have at least one live entry')

  for (const file of existing) {
    const compilerOrDistribution =
      file.startsWith('requirements/verification-system/') || file.startsWith('requirements/distribution/')
    assert.equal(compilerOrDistribution, true, `quarantine ${file} must remain outside product semantic packages`)
    const text = read(file)
    const knowsCompiledSurface =
      text.includes('dist' + '/') ||
      text.includes('fable' + '_modules') ||
      text.includes('F' + 'Sharp') ||
      text.includes('.' + 'fields') ||
      text.includes('.' + 'tag')
    assert.equal(knowsCompiledSurface, true, `quarantine ${file} must prove it knows a compiled/representation subject`)
  }
})

test('WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_emitted_relative_imports_are_package_closed_and_named_exports_link', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-module-linkage-'))
  const distRoot = join(temporaryRoot, 'dist')
  const sourceRoot = join(temporaryRoot, 'src')
  mkdirSync(distRoot, { recursive: true })
  mkdirSync(sourceRoot, { recursive: true })

  try {
    writeFileSync(join(distRoot, 'provider.js'), 'export const visible = 1\n')
    writeFileSync(join(distRoot, 'green.js'), "import { visible } from './provider.js'\nexport const value = visible\n")
    assert.deepEqual(validateModuleLinkage(distRoot), [])

    writeFileSync(join(distRoot, 'red-export.js'), "import { missing } from './provider.js'\nexport const value = missing\n")
    assert.match(validateModuleLinkage(distRoot).join('\n'), /missing named export 'missing'/)

    rmSync(join(distRoot, 'red-export.js'))
    writeFileSync(join(sourceRoot, 'outside.js'), 'export const leaked = 1\n')
    writeFileSync(join(distRoot, 'red-path.js'), "import { leaked } from '../src/outside.js'\nexport const value = leaked\n")
    assert.match(validateModuleLinkage(distRoot).join('\n'), /escapes dist package closure/)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})
