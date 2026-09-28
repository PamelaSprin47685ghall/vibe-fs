import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, parse } from './support/casebook.mjs'
import * as fetchSurface from '../../../dist/Repository/Knowledge/Casebook/FetchSurface.js'

test('WHAT[knowledge-reuse-001] public fetch preserves stored prose without claiming correctness and does not modify the subject', async () => {
  const local = sandbox()
  try {
    const { shelfmark } = await createCase(local)
    const tool = fetchSurface.contract({ tool: { schema: { string: () => ({}) } } }, local.dir, local.store)
    const fetch = shelfmark => tool.execute({ shelfmark }, { sessionID: 'reader', agent: 'engineer' })
    const result = await fetch(shelfmark)
    assert.equal(parse(result).answer, 'Answer B')
    assert.match(result, /No change was found|没有变化/)
    assert.doesNotMatch(result, /verified correct|已验证正确/)
    assert.equal(readFileSync(join(local.dir, 'subject.txt'), 'utf8'), 'version-B')
    assert.match(await fetch('Absent · 00000000'), /no entry|没有条目/i)
  } finally { local.close() }
})
