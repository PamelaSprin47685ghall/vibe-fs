import assert from 'node:assert/strict'
import test from 'node:test'
import { generateSurfaceForRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'

test('WHAT[repository-programming-027] both working roles receive the same generated members for equal capabilities', () => {
  const capabilities = ['Read', 'Write', 'Edit', 'Glob', 'Grep']
  const engineer = generateSurfaceForRole('Engineer', capabilities)
  const devops = generateSurfaceForRole('DevOps', capabilities)
  assert.equal(engineer.toolName, 'js-engineer')
  assert.equal(devops.toolName, 'js-devops')
  assert.equal(engineer.baseClassSource, devops.baseClassSource)
  assert.deepEqual(engineer.members.map(m => m.memberName), ['file', 'glob', 'grep', 'edit', 'rewrite', 'write'])
  assert.deepEqual(devops.members, engineer.members)
})

test.todo('WHAT[repository-programming-027] registered direct tools and programming tools enforce the same path, encoding, symlink and transaction boundaries')
