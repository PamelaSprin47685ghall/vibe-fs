import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as registry from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspaceEvents from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

// The sandbox resolves the active tenure from the unified lifecycle work, so the
// store is a real workspace store with one opened plan work.
const openPlanStore = async (root, workId) => {
  execFileSync('git', ['init', '--quiet', root])
  const store = workspaceEvents.acquire(await journalSurface.JournalSurface_initializeStateRoot(root))
  const opened = await lifecycle.seedOpen(store, 'plan', workId, root, 1n)
  assert.equal(opened.ok, true, opened.error)
  return store
}

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
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'plan-surface-'))
  const workId = 'plan-shared-path'
  try {
    surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Draft')
    const store = await openPlanStore(root, workId)
    const run = body => registry.runJsPlan(store.gitCommonDir, root, workId,
      `class Js extends JsProgram { async run() { ${body} } }`)
    assert.match(await run('return { text: await this.cat(PLAN_PATH), retired: [typeof this.file, typeof this.edit, typeof this.rewrite] };'), /# Draft/)
    assert.match(await run('return { retired: typeof this.file };'), /undefined/)
    for (const target of ['other.md', '../plan.md', '/etc/passwd', 'plan.md/../other.md']) {
      assert.match(await run(`return await this.cat(${JSON.stringify(target)});`), /PATH_DENIED/)
      assert.match(await run(`this.write(${JSON.stringify(target)}, 'changed'); return {};`), /PATH_DENIED/)
    }
    const write = "this.write(PLAN_PATH, '# Final'); return { changed: true };"
    assert.match(await run(write), /PLAN_REVIEW_REQUIRED/)
    assert.equal(surface.PlanningSurface.readPlan(root, workId).content, '# Draft')
    assert.equal((await lifecycle.seedReview(store, workId, 'Stayed')).ok, true)
    assert.match(await run(write), /# ok/)
    assert.equal(surface.PlanningSurface.readPlan(root, workId).content, '# Final')
    const relay = await lifecycle.seedDeliver(store, workId, 'shared-plan-run', 'shared-plan-call', 11n)
    assert.equal(relay.ok, true, relay.error)
    assert.match(await run(write), /PLAN_REVIEW_REQUIRED/)
    assert.match(await run('return { text: await this.cat(PLAN_PATH) };'), /# Final/)
  } finally {
    fs.rmSync(root, { recursive: true, force: true })
  }
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
