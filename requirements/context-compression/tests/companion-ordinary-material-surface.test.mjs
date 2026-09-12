// requirements/context-compression/tests/companion-ordinary-material-surface.test.mjs — WHAT[CONTEXT-COMPRESSION-018]
//
// Verifies that CompanionTransform owns applyCompanionForOrdinaryMaterial entry point.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[CONTEXT-COMPRESSION-018] CompanionTransform owns ordinary-material entry and consumes Host suppression as a capability', () => {
  const companion = read('src/Wanxiangshu/Context/Companion/Transform.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(companion, /let\s+applyCompanionForOrdinaryMaterial/)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Context/Companion/Program.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Context/Companion/Errors.fs')), false)
  assert.doesNotMatch(companion, /\b(?:CompanionProgram|CompanionContext|CompanionError|TransformRaw)\b/)
  assert.match(companion, /replaceMessagesInPlace\s+rawOutObj\s+rawMessages/)
  assert.match(companion, /\(isExplicitResume:\s*string option -> obj -> bool\)/)
  assert.match(companion, /if isExplicitResume projectionSessionIdOpt outObj then/)
  assert.doesNotMatch(companion, /ExplicitResumeSuppression/)
  assert.match(pt, /CompanionTransform\.applyCompanionForOrdinaryMaterial/)
  assert.match(pt, /TransformBranchCapabilities[\s\S]*IsExplicitResume/)
  // Explicit TransformMode shape — same as plugin-transforms-invariant gate, strongest public contract for composition topology
  assert.match(pt, /type\s+private\s+TransformMode/)
  assert.match(pt, /\|\s*ExplicitResumeDisclosure/)
  assert.match(pt, /\|\s*StrengthReplica\s+of\s+StrengthReplicaRuntime/)
  assert.match(pt, /\|\s*Ordinary/)
  assert.match(pt, /let\s+private\s+determineTransformMode/)
  assert.match(pt, /match\s+determineTransformMode/)
  assert.doesNotMatch(pt, /let\s+private\s+isExplicitResumeProviderMaterial/)
})

test('WHAT[CONTEXT-COMPRESSION-018] explicit-resume nudge path with marked suppression prevents companion double-send', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses_explicit_resume_nudge_suppression'
    const continueID = 'msg-continue-nudge-1'

    await acceptAuthorityRoot(runtime, sessionID, 'coder')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )

    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    const providerOutput = {
      messages: [
        {
          info: { id: continueID, sessionID, role: 'user' },
          parts: physicalOutput.parts,
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.equal(createdIds.length, 0, 'suppression path must not create companion child sessions')
    assert.equal(runtime.prompts.length, 0, 'suppression path must not double-send prompts')
  })
})

test('WHAT[CONTEXT-COMPRESSION-018] fresh binding without suppression history admits ordinary material', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_fresh_ordinary_binding'
    const userMessageID = 'msg-user-fresh-1'

    await acceptAuthorityRoot(runtime, sessionID, 'manager')

    const userOutput = {
      message: { id: userMessageID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'ordinary work' }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: userMessageID },
      userOutput,
    )

    const providerOutput = {
      messages: [
        {
          info: { id: userMessageID, sessionID, role: 'user' },
          parts: [{ type: 'text', text: 'ordinary work' }],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.ok(providerOutput.messages.length > 0)
    assert.equal(providerOutput.messages[0].info.id, userMessageID)
  })
})
