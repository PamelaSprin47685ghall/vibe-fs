import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { validateModuleLinkage } from '../../../scripts/checks/js-module-linkage.mjs'
import { BUILD_VERIFICATION_FILES } from '../../../scripts/lib/test-surface-scan.mjs'

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), '../../..'))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

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
