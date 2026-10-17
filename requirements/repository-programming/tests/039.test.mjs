import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  execute,
  caseName,
  parkedHandle,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')

const read = (path) => readFileSync(path, 'utf8')

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-session-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId) =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-039] parked sites carry a session owner so session close can reap them', () => {
  const scope = read(join(SOURCE_ROOT, 'OpenCode/Host/PluginSessionScope.fs'))
  assert.match(scope, /JsParkedSites\.reapSession sessionId/, 'session close must force-reap the session-owned sites')
})

test('WHAT[repository-programming-039] the parked-site table is one process-level static module shared by its consumers, not redefined by them', () => {
  const sites = read(join(SOURCE_ROOT, 'Repository/Programming/Js/OpenCode/ParkedSites.fs'))
  assert.match(sites, /module JsParkedSites\b/, 'the table is defined once in ParkedSites.fs')
  assert.match(sites, /let concurrencyLimit = 8/, 'the default ceiling is 8')

  const workflow = read(join(SOURCE_ROOT, 'Repository/Programming/Js/OpenCode/ToolWorkflow.fs'))
  assert.match(workflow, /JsParkedSites\.register/, 'the orchestration layer references the shared table')

  const scope = read(join(SOURCE_ROOT, 'OpenCode/Host/PluginSessionScope.fs'))
  assert.match(scope, /JsParkedSites\.reapSession/, 'the session-close layer references the shared table')

  const registry = read(join(SOURCE_ROOT, 'OpenCode/Tools/ToolRegistry.fs'))
  const transform = read(join(SOURCE_ROOT, 'OpenCode/Host/PairProgrammingThoughtTransform.fs'))
  for (const source of [ workflow, registry, transform, scope ]) {
    assert.doesNotMatch(source, /module JsParkedSites\b/, 'consumers must not redefine the table module')
  }
})

test('WHAT[repository-programming-039] a parked program is process-live: it survives across calls within the process', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const first = await runProgram(dir, `    await this.park({ stage: 'first' });`, 'sess-039')
    assert.equal(caseName(first), 'Parked')
    const handle = parkedHandle(first)

    // A second, unrelated call in the same process still sees the first site
    // parked (module-level table). The handle is still resolvable.
    const second = await runProgram(dir, `    await this.park({ stage: 'second' });`, 'sess-039')
    assert.equal(caseName(second), 'Parked')
    assert.notEqual(parkedHandle(second), handle, 'each park gets a distinct handle')
  } finally {
    cleanup()
  }
})
