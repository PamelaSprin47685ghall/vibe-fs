import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as grounding from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-opencode-'))
  mkdirSync(join(dir, 'requirements', 'alpha'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'ground truth\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'before\n', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolBatch = (providerID, path) => [
  { info: { id: 'c1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'pending', input: { filePath: path }, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: { filePath: path }, output: 'before\n', time: { start: 0, end: 0 } } }] },
]

test('WHAT[requirement-grounding-007] actual pair and grounding transforms append result-only bytes for each tested provider without synthetic calls', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    assert.equal(grounding.cursorSeparator, '\0\uFEFF')
    for (const provider of ['anthropic', 'cursor', 'openai']) {
      const opened = await grounding.createJournal(dir)
      try {
        await grounding.requestPaths(opened.journal, dir, provider, [sourcePath])
        const input = toolBatch(provider, sourcePath)
        const paired = await pair.tryInject(provider, pair.text, input)
        assert.equal(paired.ok, true)
        const result = await grounding.projectWithJournal(opened.journal, provider, paired.value)
        assert.equal(result.ok, true)
        assert.equal(result.value.length, input.length)
        assert.deepEqual(result.value.map((message) => message.info), input.map((message) => message.info))
        assert.deepEqual(result.value[0], input[0], 'pending tool call remains untouched')
        const terminal = result.value.at(-1).parts[0].state.output
        assert.ok(terminal.startsWith('before\n'))
        assert.ok(terminal.includes('\0\uFEFF# ground truth'))
        assert.ok(terminal.includes('requirement_source_path = "requirements/alpha/WHAT.md"'))
        assert.equal(result.value.some((message) => message.info?.source === grounding.source), false)
        const pairAt = terminal.indexOf(pair.text.trim())
        if (paired.value.at(-1).parts[0].state.output.includes(pair.text.trim())) {
          assert.ok(pairAt >= 0)
          assert.ok(terminal.indexOf('requirement_source_path') > pairAt)
        }
        assert.deepEqual(input, toolBatch(provider, sourcePath), 'input is not mutated')
      } finally { grounding.disposeJournal(opened.journal) }
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-007] candidate discovery tools do not ground, while an explicit read does', async () => {
  const { dir, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    for (const tool of ['grep', 'glob', 'list']) {
      const result = await grounding.observationDecision(opened.journal, dir, 'candidate', tool, { filePath: sourcePath, path: join(dir, 'src') }, `${sourcePath}:1:before\n`)
      assert.equal(result.ok, true)
      assert.equal(result.needsGrounding, false)
      assert.equal(result.requested, 0)
      assert.deepEqual(result.packages, [])
    }
    const read = await grounding.observationDecision(opened.journal, dir, 'candidate', 'read', { filePath: sourcePath }, 'before\n')
    assert.equal(read.ok, true)
    assert.equal(read.needsGrounding, true)
    assert.equal(read.requested, 1)
    assert.deepEqual(read.packages, ['alpha'])
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})

test('WHAT[requirement-grounding-007] original Markdown bytes survive the actual result suffix unchanged', { todo: 'GAP-086: current LlmFacing instruction rendering adds comments and normalizes newlines; resolve the representation contract' }, async () => {
  const { dir, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  try {
    const content = 'first\r\n\r\nsecond  \r\n'
    writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), content)
    const path = join(dir, 'src', 'main.fs')
    await grounding.requestPaths(opened.journal, dir, 'raw-bytes', [path])
    const result = await grounding.projectWithJournal(opened.journal, 'raw-bytes', toolBatch('anthropic', path))
    assert.equal(result.ok, true)
    assert.ok(result.value.at(-1).parts[0].state.output.includes(content))
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})

test.todo('WHAT[requirement-grounding-007] actual plugin hook composition fixes guidance before grounding for all paths; manually calling transforms in that order proves their composition only (GAP-085)')
