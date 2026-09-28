import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as grounding from '../../../dist/Requirement/Grounding/Surface.js'
import * as host from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-delivery-'))
  mkdirSync(join(dir, 'requirements', 'alpha', 'tests'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHY.md'), 'why\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'what-v1\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'HOW.md'), 'how\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'tests', 'z.test.mjs'), 'z\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'tests', 'a.test.mjs'), 'a\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'source\n', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const terminalRead = (path) => [{
  info: { id: 'r1', role: 'assistant', providerID: 'anthropic' },
  parts: [{ type: 'tool', tool: 'read', callID: 'source-read', state: { status: 'completed', input: { filePath: path }, output: 'source\n', time: { start: 0, end: 0 } } }],
}]

test('WHAT[requirement-grounding-011] grounding appends knowledge without fabricating additional Host messages or changing their roles', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const opened = await host.createJournal(dir)
    const source = join(dir, 'src', 'main.fs')
    await host.requestPaths(opened.journal, dir, 's-authority', [source])
    const original = terminalRead(source)
    const projected = await host.projectWithJournal(opened.journal, 's-authority', original)
    assert.equal(projected.ok, true)
    const terminalOutput = projected.value.at(-1).parts[0].state.output
    assert.ok(terminalOutput.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
    assert.equal(projected.value.some((m) => m.info?.source === host.source), false)
    assert.equal(projected.value.length, original.length)
    assert.deepEqual(projected.value.map((message) => message.info), original.map((message) => message.info))
    assert.equal(original[0].parts[0].state.output, 'source\n')
    host.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test.todo('WHAT[requirement-grounding-011] injected documents cannot alter actual Authority Roots, identity bindings or execution permissions; unchanged Host metadata is only transport evidence (GAP-085)')
