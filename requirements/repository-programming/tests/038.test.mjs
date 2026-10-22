import assert from 'node:assert/strict'
import test from 'node:test'
import { generateSurfaceForRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName, failureCode, parkedHandle, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

// repository-programming-002/008: the four layers stay identical. Park is a
// lifecycle capability every js-* surface carries (PLAN S1 B.17): the fragment
// registry, the generated base class, the runtime binding and the description
// must all agree that `park` is present. It is not a filesystem permission
// projection, so it does not enter the members list (which stays byte-identical
// for existing roles); it is unconditionally mounted in the base class and
// description instead.

const capabilities = ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove']

test('WHAT[repository-programming-038] the park member is mounted in the generated base class', () => {
  const surface = generateSurfaceForRole('Engineer', capabilities)
  assert.equal(surface.baseClassSource.includes('async park(payload)'), true, 'the base class must carry the park method')
})

test('WHAT[repository-programming-038] park is present even on a read-only surface (lifecycle capability)', () => {
  const readonly = generateSurfaceForRole('Manager', ['Read', 'Glob', 'Grep'])
  assert.equal(readonly.baseClassSource.includes('async park(payload)'), true, 'park is a lifecycle member, not a filesystem permission projection')
})

test('WHAT[repository-programming-038] the park description and example name the three-state contract', () => {
  const surface = generateSurfaceForRole('Engineer', capabilities)
  assert.match(surface.description, /park\(payload\)/, 'description must teach the park member')
  assert.match(surface.description, /unpark/, 'description must teach the unpark resume form')
  assert.match(surface.description, /abandon/, 'description must teach the abandon give-up form')
})

test('WHAT[repository-programming-038] park stays out of the filesystem members list (027 compatibility)', () => {
  const surface = generateSurfaceForRole('Engineer', capabilities)
  const names = surface.members.map((fragment) => fragment.memberName)
  assert.deepEqual(
    names,
    ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where'],
    'the members list carries the filesystem primitives in canonical order without park',
  )
})

for (const action of ['unpark', 'abandon']) {
  test(`WHAT[repository-programming-038] ${action} after the original deadline preserves the live parked program and finally`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-park-deadline-'))
    const session = `park-deadline-${action}`
    const program = `class Js extends JsProgram {
      async run() {
        try { const value = await this.park({ stage: 'waiting' }); return { resumed: value }; }
        finally { await this.write('finalized', 'finished'); }
      }
    }`
    try {
      const first = await execute(directory, 'Engineer', 'en', session, { program }, 1000, Date.now() + 250, 4096, null)
      assert.equal(caseName(first), 'Parked', render(first))
      const handle = parkedHandle(first)
      await new Promise((resolve) => setTimeout(resolve, 400))
      const input = action === 'unpark' ? { unpark: { handle, value: 'ready' } } : { abandon: { handle } }
      const result = await execute(directory, 'Engineer', 'en', session, input, 1000, Date.now() + 1000, 4096, null)
      assert.equal(caseName(result), action === 'unpark' ? 'Succeeded' : 'Failed', render(result))
      if (action === 'abandon') assert.equal(failureCode(result), 'ABANDONED')
      else assert.match(render(result), /ready/)
      assert.equal(readFileSync(join(directory, 'finalized'), 'utf8'), 'finished')
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
}
