import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

export async function assertParentReplacementInvalidatesVerification({ verify, root, logDirectory, diagnostic = () => {} }) {
  let observedReplacement = false
  let preventedReplacement
  const result = await verify({
    root,
    logDirectory,
    output: { write: diagnostic },
    runStep: async ({ label, cwd = root }) => {
      if (label === 'check') {
        const sourceDirectory = path.join(cwd, 'src')
        const backupDirectory = path.join(cwd, 'owned-original-src')
        const target = path.join(sourceDirectory, 'Foo.fs')
        const original = fs.readFileSync(target)
        const before = fs.statSync(target)
        const parentBefore = fs.statSync(sourceDirectory)
        assert.equal(fs.existsSync(backupDirectory), false)
        try {
          fs.renameSync(sourceDirectory, backupDirectory)
        } catch (error) {
          if (!['EROFS', 'EACCES', 'EPERM'].includes(error?.code)) throw error
          assert.equal(fs.existsSync(backupDirectory), false)
          const after = fs.statSync(target)
          const parentAfter = fs.statSync(sourceDirectory)
          assert.equal(parentAfter.ino, parentBefore.ino)
          assert.equal(parentAfter.ctimeMs, parentBefore.ctimeMs)
          assert.equal(after.ino, before.ino)
          assert.equal(after.ctimeMs, before.ctimeMs)
          assert.equal(after.mtimeMs, before.mtimeMs)
          assert.deepEqual(fs.readFileSync(target), original)
          diagnostic(JSON.stringify({ label, preventedReplacement: true, observedReplacement, code: error.code }))
          preventedReplacement = { error }
          throw error
        }
        try {
          fs.mkdirSync(sourceDirectory)
          const changed = Buffer.from('module Foo\nlet x = 999\n')
          assert.notDeepEqual(changed, original)
          fs.writeFileSync(target, changed)
          assert.deepEqual(fs.readFileSync(target), changed)
          observedReplacement = true
        } finally {
          fs.rmSync(sourceDirectory, { recursive: true, force: true })
          fs.renameSync(backupDirectory, sourceDirectory)
        }
        const after = fs.statSync(target)
        assert.equal(after.ino, before.ino)
        assert.equal(after.ctimeMs, before.ctimeMs)
        assert.equal(after.mtimeMs, before.mtimeMs)
        assert.deepEqual(fs.readFileSync(target), original)
        const parentAfter = fs.statSync(sourceDirectory)
        diagnostic(JSON.stringify({
          label, observedReplacement, fileInode: after.ino,
          fileCtimeUnchanged: after.ctimeMs === before.ctimeMs,
          parentCtimeBefore: parentBefore.ctimeMs,
          parentCtimeAfter: parentAfter.ctimeMs,
        }))
      }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 1 }
    },
  })
  const check = result.steps.find(step => step.label === 'check')
  if (preventedReplacement) {
    assert.equal(observedReplacement, false, 'a prevented replacement must not consume changed input')
    assert.equal(check?.status, 'failed')
    assert.equal(check.error, preventedReplacement.error)
  } else {
    assert.equal(observedReplacement, true, 'the check stage must read actual replacement input bytes')
    assert.equal(check?.status, 'ok', 'the replacement and identity assertions must complete without a swallowed step error')
  }
  diagnostic(JSON.stringify({ outcome: result.outcome, exitCode: result.exitCode, failureReason: result.failureReason, steps: result.steps }))
  assert.equal(result.exitCode, 1, 'preventing or detecting parent replacement must invalidate verification')
  assert.equal(result.outcome, 'fail')
  return result
}
