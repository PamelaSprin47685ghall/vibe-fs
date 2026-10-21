import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as registry from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[work-029] Plan path derivation resolves deterministically and rejects path traversal', () => {
  const root = '/workspace/test-repo/.Herdchestra/wanxiangshu'

  // Valid work IDs
  const valid1 = surface.PlanningSurface.resolvePlanPath(root, 'work-alpha')
  assert.equal(valid1.ok, true)
  assert.equal(valid1.path, '/workspace/test-repo/.Herdchestra/wanxiangshu/plan/work-alpha/plan.md')

  const valid2 = surface.PlanningSurface.resolvePlanPath(root, 'task_42-v1')
  assert.equal(valid2.ok, true)
  assert.equal(valid2.path, '/workspace/test-repo/.Herdchestra/wanxiangshu/plan/task_42-v1/plan.md')

  // Insecure work IDs / path traversal attempts MUST be rejected
  const traversalAttempts = [
    '../escape',
    '../../etc/passwd',
    'work/nested/../../escape',
    '/absolute/work',
    '..',
    '.hidden',
    'work\\traversal',
    'work\0nullbyte',
    '',
    '   ',
  ]

  for (const badId of traversalAttempts) {
    const res = surface.PlanningSurface.resolvePlanPath(root, badId)
    assert.equal(res.ok, false, `Traversal attempt "${badId}" must be rejected`)
    assert.ok(typeof res.error === 'string' && res.error.length > 0)
  }
})

test('WHAT[work-029] P is read, rewritten atomically with exclusive temp file, and edited with patch replacement', () => {
  const tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'plan-test-'))

  try {
    const workId = 'work-e2e-plan'

    // 1. Initially plan file does not exist
    const readMissing = surface.PlanningSurface.readPlan(tmpRoot, workId)
    assert.equal(readMissing.ok, false)

    // 2. Rewrite plan atomically
    const initialContent = '# Initial Plan\n- Step 1: Survey facts\n- Step 2: Formalize goal'
    const writeRes = surface.PlanningSurface.rewritePlanAtomic(tmpRoot, workId, initialContent)
    assert.equal(writeRes.ok, true)
    assert.ok(typeof writeRes.digest === 'string' && writeRes.digest.length === 64, 'digest must be 64-char sha256')
    assert.ok(fs.existsSync(writeRes.path), 'plan.md must exist on disk')

    // 3. Read back verified
    const readBack = surface.PlanningSurface.readPlan(tmpRoot, workId)
    assert.equal(readBack.ok, true)
    assert.equal(readBack.content, initialContent)

    // 4. Edit plan with patches
    const patches = [
      { find: '- Step 2: Formalize goal', put: '- Step 2: Formalize goal\n- Step 3: Implement' },
    ]
    const editRes = surface.PlanningSurface.editPlanAtomic(tmpRoot, workId, patches)
    assert.equal(editRes.ok, true)

    const readEdited = surface.PlanningSurface.readPlan(tmpRoot, workId)
    assert.equal(readEdited.ok, true)
    assert.ok(readEdited.content.includes('- Step 3: Implement'))

    // 5. Missing patch find target returns error
    const badPatch = [{ find: 'non-existent target string', put: 'replacement' }]
    const badEdit = surface.PlanningSurface.editPlanAtomic(tmpRoot, workId, badPatch)
    assert.equal(badEdit.ok, false)
    assert.match(badEdit.error, /Patch find target not found/)
  } finally {
    fs.rmSync(tmpRoot, { recursive: true, force: true })
  }
})

test('WHAT[work-029] shared Plan interface permits only PLAN_PATH and rejects retired members', async () => {
  // The shared Plan interface is exercised through the REAL Plan programming
  // surface (`todowrite-for-plan`), which is the only Plan entry the host
  // registers. The retired legacy Plan entry is gone, so this is also the
  // proof that the live surface carries the same interface.
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const sessionID = 'ses-work-029-plan-surface'
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'user-plan-root')
    await openIncumbency(runtime, sessionID, 'inc-work-029', 'S1')

    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, directory, sessionID, 0n)
    assert.equal(opened.ok, true, opened.error)

    const run = body => hooks.tool['todowrite-for-plan'].execute(
      { program: `class Js extends JsProgram { async run() { ${body} } }` },
      { sessionID, agent: 'plan', callID: 'call-work-029', messageID: 'run-work-029', providerRunID: 'run-work-029' },
    )

    // The draft P lives at the live surface's own plan path.
    const draft = path.join(directory, 'plan.md')
    fs.writeFileSync(draft, '# Draft')

    const read = String(await run('return { text: await this.cat(PLAN_PATH), retired: [typeof this.file, typeof this.edit, typeof this.rewrite] };'))
    assert.match(read, /# Draft/)
    assert.match(read, /undefined/)

    const retired = String(await run('return { retired: typeof this.file };'))
    assert.match(retired, /undefined/)
  })
})

test('WHAT[work-029] localized Plan pseudocode describes the same shared interface', () => {
  for (const language of ['en', 'zh-CN']) {
    const description = registry.jsPlanDescription(language)
    assert.equal(description.includes('{{'), false)
    for (const signature of ['PLAN_PATH', 'async cat(path)', 'write(path, content)', 'async ask(question)']) {
      assert.ok(description.includes(signature), signature)
    }
    for (const retired of ['this.file(', 'this.edit(', 'this.rewrite(']) assert.equal(description.includes(retired), false)
    assert.equal(description.includes('_api'), false)
    assert.equal(description.includes('this.ls('), false)
    assert.equal(description.includes('this.rg('), false)
  }
})
