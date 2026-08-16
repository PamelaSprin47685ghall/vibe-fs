// Builtin coexistence plus the generated js-* registered Host tool. Primitive
// filesystem tools remain normal fallbacks; intent-level preference is only in
// the generated description.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  annotate,
  validateRecommendation,
  builtinTools,
  createRegistered,
  name,
  description,
  execute,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-host-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = {
    string: () => ({
      type: 'string',
      describe: (description) => ({ type: 'string', description }),
    }),
  }
  return { tool }
}

test('WHAT[REPOSITORY-PROGRAMMING-005] JS003_builtin_fallback_descriptions_are_left_untouched', () => {
  assert.deepEqual([...builtinTools()].sort(), ['edit', 'glob', 'grep', 'patch', 'read', 'write'])
  for (const builtinName of builtinTools()) {
    const original = `${builtinName} primitive fallback`
    const visible = annotate(builtinName, original, 'js-coder')
    assert.equal(visible, original)
    assert.doesNotMatch(visible, /DEPRECATED|js-coder/i)
  }
  assert.equal(annotate('join', 'Join a session', 'js-coder'), 'Join a session')
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS003_hook_must_not_recommend_invisible_tools', () => {
  assert.equal(validateRecommendation('js-coder', ['js-coder', 'read']).ok, true)
  const denied = validateRecommendation('js-coder', ['read'])
  assert.equal(denied.ok, false)
  assert.equal(denied.error.includes('not provider-visible'), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS073_spec_carries_generated_name_and_honest_description', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    assert.equal(name(registered), 'js-coder')
    assert.equal(description(registered).includes('class JsProgram'), true)
    assert.equal(description(registered).includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
    assert.equal(description(registered).includes('_api'), false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-016] JS073_spec_executes_program_and_renders_result', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)

    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const result = await execute(registered, { program }, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(result.startsWith('# ok\n'), true)
    assert.equal(result.includes('[data]'), true)
    assert.equal(result.includes('before'), true)
    assert.equal(result.includes('[fs]'), true)
    assert.equal(result.includes('status ='), false)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed via workflow')
    const missing = await execute(registered, {}, { sessionID: 'ses-test', agent: 'fast-coder' })
    assert.equal(missing.includes('error'), true)
    assert.equal(missing.includes("missing 'program' argument"), true)
  } finally {
    cleanup()
  }
})
