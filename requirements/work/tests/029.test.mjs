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
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const program = (body) => `class Js extends JsProgram { async run() { ${body} } }`

const withPlan = async (body) => {
  const root = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'plan-sdk-'))
  const workId = 'plan-work'
  try {
    execFileSync('git', ['init', '--quiet', root])
    const store = workspaceEvents.acquire(journalSurface.JournalSurface_initializeStateRoot(root))
    assert.equal((await lifecycle.seedOpen(store, 'plan', workId, root, 1n)).ok, true)
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Draft\nKeep this fact.')
    assert.equal(written.ok, true)
    const run = (body) => registry.runJsPlan(store.gitCommonDir, root, workId, program(body))
    await body({ root, workId, store, run, planPath: written.path })
  } finally {
    fs.rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[work-029] Plan path derivation is deterministic and rejects insecure work identities', () => {
  const root = '/workspace/repo/.Herdchestra/wanxiangshu'
  assert.deepEqual(surface.PlanningSurface.resolvePlanPath(root, 'task_42-v1'), {
    ok: true, path: root + '/plan/task_42-v1/plan.md', error: null,
  })
  for (const id of ['../escape', '..', '.hidden', '/absolute', 'work/nested', 'work\\escape', 'work\0null', '', '   ']) {
    assert.equal(surface.PlanningSurface.resolvePlanPath(root, id).ok, false, id)
  }
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
        `return await this._api.js.cat(${JSON.stringify(target)});`,
        `this.write(${JSON.stringify(target)}, 'damaged'); return {};`,
        `return this._api.js.write(${JSON.stringify(target)}, 'damaged');`,
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
    assert.match(await run("return this._api.js.write(PLAN_PATH, '# Bypass');"), /PLAN_REVIEW_REQUIRED/)
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
    assert.match(await run('return await this._api.js.cat(PLAN_PATH);'), /PATH_DENIED/)
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
    const common = journalSurface.JournalSurface_initializeStateRoot(runtime.directory)
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
