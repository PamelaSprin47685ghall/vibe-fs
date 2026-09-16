import assert from 'node:assert/strict'
import test from 'node:test'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'

for (const language of ['en', 'zh-CN']) {
  test(`WHAT[REPOSITORY-PROGRAMMING-003] ${language} Engineer examples follow the current read/write boundary`, () => {
    const readonly = generate('Engineer', ['Read', 'Grep'], language)
    const writing = generate('Engineer', ['Read', 'Grep', 'Edit'], language)
    assert.equal(readonly.examples.length, 1)
    assert.match(readonly.examples[0], /RetryPolicy/)
    assert.doesNotMatch(readonly.examples[0], /this\.(?:edit|write|rewrite|runCommand)/)
    assert.equal(writing.examples.length, 1)
    assert.match(writing.examples[0], /this\.edit/)
  })

  test(`WHAT[REPOSITORY-PROGRAMMING-003] ${language} DevOps example repairs directly and leaves runtime verification explicit`, () => {
    const result = generate('DevOps', ['Read', 'Grep', 'Glob', 'Edit', 'Write'], language)
    assert.equal(result.examples.length, 1)
    assert.match(result.examples[0], /this\.edit/)
    assert.match(result.examples[0], /verificationStillRequired: true/)
    assert.doesNotMatch(result.examples[0], /fork|resume|testsPassed/)
  })

  test(`WHAT[REPOSITORY-PROGRAMMING-003] ${language} impossible legacy and management inputs do not receive file-work examples`, () => {
    // The generator receives capabilities, not authority. Even fabricated input
    // must not resurrect an office-specific teaching example for a removed role.
    for (const role of ['Coder', 'Inspector', 'Browser', 'Inquiry', 'Distiller', 'Manager', 'Orchestrator']) {
      const result = generate(role, ['Read', 'Grep', 'Edit'], language)
      assert.deepEqual(result.examples, [], role)
    }
  })
}
