import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { Header } from 'tar'
import { materializeVerificationArchive } from '../../../../scripts/lib/verification-archive.mjs'

export function registerArchiveNamespaceTests() {
  for (const replaced of ['parent', 'root', 'symbolic-root', 'parent-with-missing-root']) {
    test(`WHAT[verification-system-016] archive ${replaced} replacement cannot borrow an exact copy or delete its replacement`, async () => {
      const fixture = fs.mkdtempSync(path.join(os.tmpdir(), 'archive-namespace-'))
      const parentDirectory = path.join(fixture, 'parent')
      fs.mkdirSync(parentDirectory)
      const blocks = []
      for (const [name, type, bytes] of [['bundle', 'Directory', Buffer.alloc(0)], ['bundle/input', 'File', Buffer.from('selected')]]) {
        const header = new Header({ path: name, type, mode: type === 'Directory' ? 0o755 : 0o644, size: bytes.length })
        header.encode()
        blocks.push(header.block, bytes, Buffer.alloc((512 - bytes.length % 512) % 512))
      }
      const archiveBytes = Buffer.concat([...blocks, Buffer.alloc(1024)])
      try {
        const candidate = await materializeVerificationArchive({ archiveBytes, archiveSha256: createHash('sha256').update(archiveBytes).digest('hex'), parentDirectory, rootDirectory: 'bundle', errorPrefix: 'archive-test' })
        candidate.revalidate()
        const rootMode = fs.statSync(candidate.root).mode & 0o777
        const target = replaced.startsWith('parent') ? parentDirectory : candidate.root
        const parked = path.join(fixture, 'parked')
        fs.renameSync(target, parked)
        if (replaced === 'symbolic-root') fs.symlinkSync(parked, target)
        else if (replaced === 'parent-with-missing-root') fs.mkdirSync(target)
        else fs.cpSync(parked, target, { recursive: true, preserveTimestamps: true })
        if (replaced === 'parent' || replaced === 'root') {
          fs.chmodSync(target, fs.statSync(parked).mode & 0o777)
          fs.chmodSync(candidate.root, rootMode)
        }
        assert.throws(() => candidate.revalidate(), { code: 'archive-test-entry-invalid' })
        assert.throws(() => candidate.dispose(), { code: 'archive-test-entry-invalid' })
        if (replaced !== 'parent-with-missing-root') assert.equal(fs.readFileSync(path.join(candidate.root, 'bundle/input'), 'utf8'), 'selected')
        else assert.equal(fs.existsSync(target), true)
        fs.rmSync(target, { recursive: true })
        fs.renameSync(parked, target)
        candidate.revalidate()
        candidate.dispose()
        candidate.dispose()
        assert.deepEqual(fs.readdirSync(parentDirectory), [])
      } finally {
        fs.rmSync(fixture, { recursive: true, force: true })
      }
    })
  }
}
