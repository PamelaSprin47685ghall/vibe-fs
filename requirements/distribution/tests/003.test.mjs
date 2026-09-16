import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const root = process.cwd()
const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
const exists = (relative) => fs.existsSync(path.join(root, relative))

test('WHAT[DISTRIBUTION-003] DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path', () => {
  assert.equal(typeof pkg.main, 'string', 'main must be declared')
  assert.equal(pkg.exports['.'], pkg.main, 'exports["."] must equal main')
  assert.match(pkg.main, /^\.?\/?dist\//, 'main must live under dist/')
  assert.ok(exists(pkg.main), `main must exist on disk: ${pkg.main}`)
})
