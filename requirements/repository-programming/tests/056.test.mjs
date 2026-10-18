import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  installDelegationHost,
  clearDelegationHost,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-forkjoin-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body, sessionId = 'sess-056') =>
  execute(dir, 'Manager', 'en', sessionId, { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-056] fork and join are projected only where the capability exists', async () => {
  const { generateSurfaceForRole } = await import('../../../dist/Repository/Programming/Js/GeneratorSurface.js')
  const manager = generateSurfaceForRole('Manager', ['Read', 'Glob', 'Grep', 'Fork', 'Join'])
  const engineer = generateSurfaceForRole('Engineer', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  const managerMembers = manager.members.map((fragment) => fragment.memberName)
  assert.equal(managerMembers.includes('fork'), true)
  assert.equal(managerMembers.includes('join'), true)
  assert.equal(engineer.members.map((fragment) => fragment.memberName).includes('fork'), false)
  assert.equal(engineer.baseClassSource.includes('async fork('), false)
})

test('WHAT[repository-programming-056] fork delegates through the installed host and join collects it', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-fork-${Math.random().toString(36).slice(2, 8)}`
  try {
    const seen = []
    installDelegationHost(
      sessionId,
      async (payload) => {
        seen.push(payload)
        return `FORK-RECEIPT:${payload.charge}`
      },
      async () => 'JOIN-RECEIPT',
    )
    const outcome = await runProgram(
      dir,
      `    const receipt = await this.fork({ charge: 'index the parser', name: 'kid' });
    const joined = await this.join();
    return { receipt, joined };`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /FORK-RECEIPT:index the parser/)
    assert.match(doc, /JOIN-RECEIPT/)
    assert.equal(seen.length, 1)
    assert.equal(seen[0].charge, 'index the parser', 'the payload reaches the host unchanged')
    assert.equal(seen[0].name, 'kid')
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-056] without an installed host fork refuses with DELEGATION_UNAVAILABLE', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    await this.fork({ charge: 'nowhere' });`, 'sess-056-nohost')
    assert.equal(caseName(outcome), 'Failed')
    const doc = render(outcome)
    assert.match(doc, /# failed/)
    assert.match(doc, /DELEGATION_UNAVAILABLE/)
  } finally {
    cleanup()
  }
})
