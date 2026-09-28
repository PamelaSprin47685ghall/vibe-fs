import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as guidance from '../../../../dist/Enforcer/Guidance/TipSurface.js'
import { runtimeInstallFromPackage } from '../../../../dist/Resources/PromptSurface.js'

runtimeInstallFromPackage()
export { guidance }
export const main = 'guidance-main'
export const blogger = 'guidance-blogger'
export const tip = 'primitive-obsession'

export async function link(journal, owner = main, companion = blogger) {
  const result = await guidance.appendCompanionLink(journal, { session: owner, bloggerSession: companion, bloggerAgent: 'blogger' })
  assert.equal(result.ok, true, result.error)
}

export async function observe(journal, ordinal = 1, field = tip, owner = main, companion = blogger) {
  const result = await guidance.appendObservation(journal, {
    session: owner, bloggerSession: companion, requestId: `request-${owner}-${ordinal}`, frameEpoch: 0,
    previousIngestedThrough: ordinal - 1, nextIngestedThrough: ordinal,
    previousCutoff: ordinal - 1, nextCutoff: ordinal, nextCoveredPrefixDigest: `digest-${ordinal}`,
    textRef: `blob-${ordinal}`, textDigest: `sha-${ordinal}`, providerRun: `run-${owner}-${ordinal}`,
    toolCallIds: [`call-${ordinal}`], tipRuleId: field, fieldNameAtCommit: field, observedPrefixEpoch: 0,
  })
  assert.equal(result.ok, true, result.error)
}

export async function withJournal(run) {
  const directory = mkdtempSync(join(tmpdir(), 'guidance-proof-'))
  let journal
  let closed = true
  const open = async () => {
    const result = await guidance.createJournal(directory)
    assert.equal(result.ok, true, result.error)
    journal = result.journal
    closed = false
    return journal
  }
  const close = () => {
    if (!closed) guidance.disposeJournal(journal)
    closed = true
  }
  try {
    await open()
    return await run({ get journal() { return journal }, close, reopen: async () => { close(); return open() } })
  } finally {
    close()
    rmSync(directory, { recursive: true, force: true })
  }
}
