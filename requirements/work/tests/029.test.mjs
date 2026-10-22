import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as registry from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspaceEvents from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const program = (body) => `class Js extends JsProgram { async run() { ${body} } }`

const withPlan = async (body) => {
  await withExecutablePlugin(async (hooks, root, _createdIds, runtime) => {
    const workId = 'plan-work'
    await acceptAuthorityRoot(runtime, workId, 'plan', 'plan-root')
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(root)
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, root, workId, 0n)
    assert.equal(opened.ok, true, opened.error)
    const store = workspaceEvents.acquire(commonDir)
    const written = surface.PlanningSurface.rewritePlanAtomic(path.join(commonDir, 'wanxiangshu'), workId, '# Draft\nKeep this fact.')
    assert.equal(written.ok, true, written.error)
    let calls = 0
    const run = (body) => hooks.tool['todowrite-for-plan'].execute(
      { program: program(body) },
      { sessionID: workId, agent: 'plan', callID: `call-plan-${++calls}`, messageID: 'run-plan' },
    )
    await body({ root, workId, store, run, planPath: written.path })
  })
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
    const draft = surface.PlanningSurface.resolvePlanPath(path.join(commonDir, 'wanxiangshu'), sessionID).path
    assert.equal(surface.PlanningSurface.rewritePlanAtomic(path.join(commonDir, 'wanxiangshu'), sessionID, '# Draft').ok, true)

    const read = String(await run('return { text: await this.cat(PLAN_PATH), retired: [typeof this.file, typeof this.edit, typeof this.rewrite] };'))
    assert.match(read, /# Draft/)
    assert.match(read, /undefined/)

    const retired = String(await run('return { retired: typeof this.file };'))
    assert.match(retired, /undefined/)
  })
})

test('WHAT[work-029] Plan uses common text primitives and exposes no retired file edit or rewrite aliases', async () => {
  await withPlan(async ({ run }) => {
    const output = await run(`
      const text = await this.cat(PLAN_PATH);
      const span = this.whereAll(text, 'Draft')[0];
      return { word: text.slice(...span), file: typeof this.file, edit: typeof this.edit,
        rewrite: typeof this.rewrite, mkdir: typeof this.mkdir, call: typeof this.call };
    `)
    assert.match(output, /# ok/)
    assert.match(output, /word = "Draft"/)
    for (const name of ['file', 'edit', 'rewrite', 'mkdir', 'call']) {
      assert.match(output, new RegExp(name + ' = "undefined"'))
    }
  })
})

test('WHAT[work-029] public and raw API calls cannot read or write outside P', async () => {
  await withPlan(async ({ root, store, workId, run, planPath }) => {
    assert.equal((await lifecycle.seedReview(store, workId, 'Stayed')).ok, true)
    fs.writeFileSync(path.join(root, 'secret.txt'), 'private repository data')
    fs.writeFileSync(path.join(path.dirname(planPath), 'sibling.txt'), 'private sibling')
    for (const target of ['sibling.txt', '../../secret.txt', path.join(root, 'secret.txt')]) {
      for (const body of [
        `return await this.cat(${JSON.stringify(target)});`,
        `return await this._api.cat(${JSON.stringify(target)});`,
        `this.write(${JSON.stringify(target)}, 'damaged'); return {};`,
        `return this._api.write(${JSON.stringify(target)}, 'damaged');`,
      ]) {
        const output = await run(body)
        assert.match(output, /PATH_DENIED/, body)
        assert.doesNotMatch(output, /private repository data|private sibling/)
      }
    }
    assert.equal(fs.readFileSync(path.join(root, 'secret.txt'), 'utf8'), 'private repository data')
    assert.equal(fs.readFileSync(path.join(path.dirname(planPath), 'sibling.txt'), 'utf8'), 'private sibling')
  })
})

test('WHAT[work-029] current tenure review gates each actual P write and relay revokes the permission', async () => {
  await withPlan(async ({ store, workId, run, planPath }) => {
    assert.match(await run('return { text: await this.cat(PLAN_PATH) };'), /Keep this fact/)
    const rewrite = "this.write(PLAN_PATH, '# Reviewed'); return { done: true };"
    assert.match(await run(rewrite), /PLAN_REVIEW_REQUIRED/)
    assert.match(await run("return this._api.write(PLAN_PATH, '# Bypass');"), /PLAN_REVIEW_REQUIRED/)
    assert.equal(fs.readFileSync(planPath, 'utf8'), '# Draft\nKeep this fact.')
    assert.equal((await lifecycle.seedReview(store, workId, 'Stayed')).ok, true)
    assert.match(await run(rewrite), /# ok/)
    assert.equal(fs.readFileSync(planPath, 'utf8'), '# Reviewed')
    assert.equal((await lifecycle.seedDeliver(store, workId, 'run-relay', 'call-relay', 11n)).ok, true)
    assert.match(await run("this.write(PLAN_PATH, '# Invalid successor write'); return {};"), /PLAN_REVIEW_REQUIRED/)
    assert.equal(fs.readFileSync(planPath, 'utf8'), '# Reviewed')
    assert.match(await run('return { text: await this.cat(PLAN_PATH) };'), /# Reviewed/)
  })
})

test('WHAT[work-029] P symlinks and a substituted plan directory cannot escape the bound artifact', async () => {
  await withPlan(async ({ root, workId, store, run, planPath }) => {
    assert.equal((await lifecycle.seedReview(store, workId, 'Stayed')).ok, true)
    const secret = path.join(root, 'secret.txt')
    fs.writeFileSync(secret, 'untouched')
    fs.unlinkSync(planPath)
    fs.symlinkSync(secret, planPath)
    assert.match(await run('return { text: await this.cat(PLAN_PATH) };'), /PATH_DENIED/)
    assert.match(await run("this.write(PLAN_PATH, 'bad'); return {};"), /# failed/)
    assert.equal(fs.readFileSync(secret, 'utf8'), 'untouched')
    fs.rmSync(path.dirname(planPath), { recursive: true })
    fs.symlinkSync(root, path.dirname(planPath), 'dir')
    assert.match(await run('return await this._api.cat(PLAN_PATH);'), /PATH_DENIED/)
  })
})

test('WHAT[work-029] the shared as-is write remains on disk after a later program failure', async () => {
  await withPlan(async ({ store, workId, run, planPath }) => {
    assert.equal((await lifecycle.seedReview(store, workId, 'Stayed')).ok, true)
    const output = await run("this.write(PLAN_PATH, '# Saved'); throw new Error('later failure');")
    assert.match(output, /# failed/)
    assert.equal(fs.readFileSync(planPath, 'utf8'), '# Saved')
  })
})

test('WHAT[work-029] both Plan descriptions teach PLAN_PATH with the shared SDK and current view contract', () => {
  for (const language of ['en', 'zh-CN']) {
    const description = registry.jsPlanDescription(language)
    assert.equal(description.includes('{{'), false)
    assert.match(description, /PLAN_PATH/)
    assert.match(description, /cat\(path\)/)
    assert.match(description, /write\(path, content\)/)
    assert.match(description, /thenMap/)
    assert.doesNotMatch(description, /file\(matches|edit\(changes|rewrite\(newText|async file\(/)
    assert.doesNotMatch(description, /_api|__jsFailure|mkdir\(path\)/)
  }
})

test('WHAT[work-029] a saved Plan write resumed after relay cannot borrow the successor review', async () => {
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const session = 'plan-saved-write'
    await acceptAuthorityRoot(runtime, session, 'plan', 'user-root')
    const message = { id: 'user-root', role: 'user', agent: 'plan', sessionID: session }
    runtime.pushHostMessage(session, { info: message, parts: [{ type: 'text', text: 'Plan this work.' }] })
    await hooks['chat.message']({ sessionID: session, agent: 'plan', messageID: 'user-root' }, {
      message, parts: [{ type: 'text', text: 'Plan this work.' }],
    })
    await openIncumbency(runtime, session, 'plan-inc-1', 'S1')
    const context = (call, run) => ({ sessionID: session, agent: 'plan', callID: call, messageID: run })
    const common = await journalSurface.JournalSurface_initializeStateRoot(runtime.directory)
    const store = workspaceEvents.acquire(common)
    const saved = surface.PlanningSurface.rewritePlanAtomic(path.join(common, 'wanxiangshu'), session, '# Original')
    assert.equal(saved.ok, true)
    assert.equal((await lifecycle.seedReview(store, session, 'Stayed')).ok, true)
    const parked = await hooks.tool['todowrite-for-plan'].execute({
      program: program("const write = this.write.bind(this); await this.park('pause'); write(PLAN_PATH, '# Stale'); return {};"),
    }, context('saved-write', 'saved-run'))
    assert.match(parked, /# parked/)
    const handle = parked.match(/handle = "([^"]+)"/)?.[1]
    assert.ok(handle)
    assert.equal((await lifecycle.seedDeliver(store, session, 'relay-run', 'relay-call', 30n)).ok, true)
    assert.equal((await lifecycle.seedReview(store, session, 'Stayed')).ok, true)
    const resumed = await hooks.tool['todowrite-for-plan'].execute({
      unpark: { handle, value: null },
    }, context('resume-write', 'resume-run'))
    assert.match(resumed, /PLAN_REVIEW_REQUIRED/)
    assert.equal(fs.readFileSync(saved.path, 'utf8'), '# Original')
  })
})
