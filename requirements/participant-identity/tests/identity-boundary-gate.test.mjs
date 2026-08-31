import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const gate = fileURLToPath(new URL('../../../scripts/checks/participant-identity-boundary.mjs', import.meta.url))

const writeFixture = (root, relativePath, text) => {
  const path = join(root, relativePath)
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, text)
}

const writePassingBoundary = (root) => {
  writeFixture(
    root,
    'src/Wanxiangshu/Participant/Persona/Identity.fs',
    [
      'namespace Wanxiangshu.Participant.Persona',
      'type ParticipantIdentity = private { Value: string }',
      'type ParticipantIdentityEvidence = private { Identity: ParticipantIdentity }',
      'module ParticipantIdentity =',
      '    let resolveAtRoot value = value',
      '    let inheritFromOwner value owner = value, owner',
      '    let rehydrate owner input = owner, input',
      '    let selectedAgent value = value',
      '    let peerAgent value = value',
      '    let role value = value',
      '    let initialTier value = value',
      '    let persona value = value',
      '    let origin value = value',
    ].join('\n'),
  )
  writeFixture(
    root,
    'src/Wanxiangshu/Interaction/Authority/Facts.fs',
    [
      'namespace Wanxiangshu.Interaction.Authority',
      'type AuthorityRootAcceptedPayload =',
      '    { SchemaVersion: int',
      '      IdentitySeed: PromptIdentitySeed }',
    ].join('\n'),
  )
  writeFixture(
    root,
    'src/Wanxiangshu/Interaction/Authority/Model.fs',
    [
      'namespace Wanxiangshu.Interaction.Authority',
      'type IdentitySeed = PromptIdentitySeed',
      'type AuthorityExecutionProfile =',
      '    private',
      '        { StoredIdentitySeed: IdentitySeed }',
      '    member this.ParticipantIdentity = PromptIdentitySeed.participantIdentity this.StoredIdentitySeed',
    ].join('\n'),
  )
}

test('WHAT[PID-001] rejects SessionId keyed identity cache', () => {
  const root = mkdtempSync(join(tmpdir(), 'participant-identity-boundary-'))

  try {
    writePassingBoundary(root)
    writeFixture(
      root,
      'src/Wanxiangshu/Interaction/Dispatch/IdentityCache.fs',
      [
        'namespace Wanxiangshu.Interaction.Dispatch',
        'open System.Collections.Generic',
        'open Wanxiangshu.Foundation.Identity',
        'let identities = Dictionary<SessionId, ParticipantIdentityEvidence>()',
      ].join('\n'),
    )

    const result = spawnSync(process.execPath, [gate], {
      cwd: root,
      encoding: 'utf8',
    })

    assert.equal(result.error, undefined, result.error?.message)
    assert.equal(result.status, 1, `stdout:\n${result.stdout}\nstderr:\n${result.stderr}`)
    assert.match(result.stderr, /src\/Wanxiangshu\/Interaction\/Dispatch\/IdentityCache\.fs:4/)
    assert.match(result.stderr, /\[session-identity-cache\].*SessionId-keyed ParticipantIdentity\/IdentitySeed collection is forbidden/)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[PID-001] rejects SessionPersona revival', () => {
  const root = mkdtempSync(join(tmpdir(), 'participant-identity-boundary-'))

  try {
    writePassingBoundary(root)
    writeFixture(
      root,
      'src/Wanxiangshu/OpenCode/Host/RevivedIdentityBinding.fs',
      [
        'namespace Wanxiangshu.OpenCode.Host',
        'let bindIdentity (persona: SessionPersona) = persona',
      ].join('\n'),
    )

    const result = spawnSync(process.execPath, [gate], {
      cwd: root,
      encoding: 'utf8',
    })

    assert.equal(result.error, undefined, result.error?.message)
    assert.equal(result.status, 1, `stdout:\n${result.stdout}\nstderr:\n${result.stderr}`)
    assert.equal(
      result.stderr,
      [
        'participant-identity-boundary: 1 violation(s)',
        "  src/Wanxiangshu/OpenCode/Host/RevivedIdentityBinding.fs:2 [retired-identity-token] retired identity token 'SessionPersona' is forbidden in production source",
        '',
      ].join('\n'),
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
