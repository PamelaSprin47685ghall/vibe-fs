import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

const packageResourcesUrl = pathToFileURL(
  path.join(root, 'dist/Resources/PackageResources.js'),
).href

const walkFs = (dir) => {
  const out = []
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) out.push(...walkFs(full))
    else if (entry.isFile() && entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

test('WHAT[DISTRIBUTION-006] DISTRIBUTION_resource_missing_fails_fast_no_fallback', async () => {
  const { readText } = await import(packageResourcesUrl)
  assert.throws(
    () => readText('enforcer/does-not-exist-rule/enforcer.md'),
    (err) => {
      const message = String(err?.message ?? err)
      assert.match(message, /package resource missing/)
      assert.match(message, /does-not-exist-rule/)
      return true
    },
    'missing resource must throw package resource missing, never fall back',
  )
  assert.equal(
    fs.existsSync(path.join(root, 'resources', 'catalog.json')),
    false,
    'catalog.json must not exist — directory is the single source of truth for the rulebook',
  )
})

test('WHAT[DISTRIBUTION-006] DISTRIBUTION_resource_io_lives_only_under_infrastructure_resources', () => {
  const resourcesDir = path.join(root, 'src', 'Wanxiangshu', 'Resources')
  const productionFiles = walkFs(path.join(root, 'src', 'Wanxiangshu'))
  const offenders = productionFiles.filter(
    (f) =>
      !f.startsWith(resourcesDir) &&
      f.endsWith('.fs') &&
      /PackageResources\./.test(fs.readFileSync(f, 'utf8')),
  )
  assert.deepEqual(
    offenders,
    [],
    `PackageResources. references outside ${path.relative(root, resourcesDir)}: ${offenders.join(', ')}`,
  )
})
