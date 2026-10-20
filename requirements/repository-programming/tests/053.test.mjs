import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as toolModule from '@opencode-ai/plugin'
import { argumentNames, argumentSchema, createRegistered, execute } from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
import { clearAll, takeView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

test('WHAT[repository-programming-053] the actual registered schema exposes optional flat view fields and nullable mapping values', () => {
  const root = mkdtempSync(join(tmpdir(), 'view-schema-053-'))
  try {
    const tool = createRegistered(toolModule, 'Coder', 'en', root, null)
    assert.deepEqual(argumentNames(tool), ['program', 'unpark', 'abandon', 'prefixUntil', 'thenMap'])
    assert.equal(argumentSchema(tool, 'prefixUntil').safeParse(undefined).success, true)
    const mapping = argumentSchema(tool, 'thenMap')
    assert.equal(mapping.safeParse(undefined).success, true)
    assert.equal(mapping.safeParse({ 0: null, 3: 'summary' }).success, true)
    assert.equal(mapping.safeParse({ 3: 42 }).success, false)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-053] malformed or legacy view declarations refuse before the program can write', async () => {
  const root = mkdtempSync(join(tmpdir(), 'view-gate-053-'))
  try {
    const tool = createRegistered(toolModule, 'Coder', 'en', root, null)
    const program = "class Js extends JsProgram { async run() { this.write('sentinel', 'bad'); return {}; } }"
    for (const fields of [
      { view: { prefixUntil: 0, thenMap: {} } },
      { prefixUntil: 0 },
      { thenMap: {} },
      { prefixUntil: 0, thenMap: { '-1': 'invalid' } },
      { prefixUntil: 0, thenMap: { 2: '' } },
    ]) {
      const output = await execute(tool, { program, ...fields }, {
        sessionID: 'view-053', messageID: 'run-053', callID: 'call-053',
      })
      assert.match(output, /VIEW_DECLARATION_INVALID/)
      assert.equal(existsSync(join(root, 'sentinel')), false)
      assert.equal(takeView('view-053'), null)
    }
  } finally {
    clearAll()
    rmSync(root, { recursive: true, force: true })
  }
})
