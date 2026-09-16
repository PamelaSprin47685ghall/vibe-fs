import assert from 'node:assert/strict'
import { dirname, join, relative, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { walk } from '../../../scripts/lib/walk.mjs'

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), '../../..'))
const relativePath = (path) => relative(process.cwd(), path).replace(/\\/g, '/')

test('WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs', () => {
  const testFiles = walk(join(ROOT, 'requirements'), ['.test.mjs', '.test.js', '.test.fs', '.test.ts', '.test.fsx'])
  assert.deepEqual(
    testFiles.filter((file) => !file.endsWith('.test.mjs')).map(relativePath),
    [],
    'every automated semantic test must be a .mjs file',
  )
})
