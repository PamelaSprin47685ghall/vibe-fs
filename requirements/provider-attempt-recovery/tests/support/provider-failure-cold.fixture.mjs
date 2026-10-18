import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { createHash, randomUUID } from 'node:crypto'

const [packageRoot, receiptPath, resultPath] = process.argv.slice(2)
const source = JSON.parse(fs.readFileSync(receiptPath, 'utf8'))
const imported = relative => import(pathToFileURL(path.join(packageRoot, 'dist', relative)).href)
const journals = await imported('Persistence/Journal/Surface.js')
const failures = await imported('Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js')
const writerId = 'cold-' + randomUUID()
const opened = await journals.JournalSurface_bootWithWriterId(source.directory, writerId, 'budget-cold-' + randomUUID(), process.pid, new Date().toISOString())
assert.equal(opened.ok, true, JSON.stringify(opened))
try {
  const actual = failures.snapshot(opened.journal, source.initial.sessionId)
  assert.equal(actual.failures, source.scenario === 'after-release' || source.scenario === 'valid' ? 1 : 0)
  assert.deepEqual(actual, source.after)
  for (const file of source.filesAfter) {
    const bytes = fs.readFileSync(path.join(source.directory, 'wanxiangshu', 'events', file.name))
    assert.equal(bytes.toString('base64'), file.bytes)
    assert.equal(createHash('sha256').update(bytes).digest('hex'), file.sha256)
  }
  const writerCreated = fs.readdirSync(path.join(source.directory, 'wanxiangshu', 'events')).some(name => name.startsWith(writerId))
  assert.equal(writerCreated, false)
  fs.writeFileSync(resultPath, JSON.stringify({ pid: process.pid, sourcePid: source.pid, writerId, writerCreated, actual, originalBytesPreserved: true }) + '\n')
} finally {
  journals.JournalSurface_dispose(opened.journal)
}
