import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { z } from 'zod'
import {
  createRegistered,
  description,
  argumentNames,
  argumentSchema,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'

const withTool = (check, role = 'Coder') => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-049-schema-'))
  const tool = definition => definition
  tool.schema = z
  try {
    check(createRegistered({ tool }, role, 'en', dir, null))
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

const sample = 'class Js extends JsProgram { async run() { return { ok: true }; } }'

test('WHAT[repository-programming-049] todowrite has exactly three required string arguments', () => {
  for (const role of ['Coder', 'Engineer', 'Manager', 'DevOps', 'Plan', 'Orchestrator']) {
    withTool(handle => {
      assert.deepEqual(argumentNames(handle), ['name', 'program', 'planB'], `${role} must expose exactly three required Host parameters`)
      const program = argumentSchema(handle, 'program')
      assert.equal(program.safeParse(undefined).success, false, 'program is required')
      assert.equal(program.safeParse(null).success, false)
      assert.equal(program.safeParse(42).success, false)
      assert.equal(program.safeParse(sample).success, true)
      assert.match(program.description, /program|JsProgram/)
      const name = argumentSchema(handle, 'name')
      const planB = argumentSchema(handle, 'planB')
      for (const schema of [name, planB]) {
        assert.equal(schema.safeParse(undefined).success,false)
        assert.equal(schema.safeParse(42).success,false)
        assert.equal(schema.safeParse('value').success,true)
      }
      const modelSchema = z.object({ name, program, planB }).strict()
      assert.deepEqual(z.toJSONSchema(modelSchema).required, ['name', 'program', 'planB'])
      assert.equal(modelSchema.safeParse({}).success, false, 'an empty tool call must be schema-invalid')
      for (const retired of ['unpark', 'abandon', 'handle']) {
        assert.equal(argumentSchema(handle, retired), null, retired + ' is not a Host tool argument')
      }
    }, role)
  }
})

test('WHAT[repository-programming-049] tool description leads with the three required string arguments', () => {
  withTool(handle => {
    const text = description(handle)
    assert.match(text.slice(0,420), /program.*required|required.*program/i)
    assert.match(text.slice(0,520), /class Js extends JsProgram/)
    assert.match(text, /async run\(\)/)
    assert.doesNotMatch(text.slice(0,520), /optional.*program/i)
  })
})

test('WHAT[repository-programming-049] the actual OpenCode plugin exposes required name, program and planB to the LLM', async () => {
  const { withExecutablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  await withExecutablePlugin(async hooks => {
    for (const role of ['manager', 'engineer', 'devops', 'plan', 'orchestrator']) {
      const registered = hooks.tool[`todowrite-for-${role}`]
      assert.ok(registered, role + ' must expose a generated tool')
      assert.deepEqual(Object.keys(registered.args), ['name', 'program', 'planB'])
      const schema = z.toJSONSchema(z.object(registered.args))
      assert.deepEqual(schema.required, ['name', 'program', 'planB'], role + ' must require program')
      assert.equal(schema.properties.program.type, 'string')
      assert.equal(z.object(registered.args).safeParse({}).success, false)
      assert.match(registered.description.slice(0,520), /program.*required|required.*program|唯一.*program|program.*必填|必填.*program/i)
    }
  })
})
