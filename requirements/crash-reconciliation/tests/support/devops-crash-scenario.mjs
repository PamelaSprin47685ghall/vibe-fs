import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import * as bindingSurface from '../../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as resumeSurface from '../../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import {
  acceptAuthorityRoot,
  startPluginIncarnation,
} from '../../../verification-system/tests/support/plugin-fixture.mjs'

const [mode, workspace, marker] = process.argv.slice(2)
const parentSessionId = 'ses-manager-road'
const devopsSessionId = 'ses-devops-fixed'
const physicalUserMessageId = 'msg-devops-initial'
const agent = 'devops'
const model = { providerID: 'provider', modelID: 'devops-model', variant: 'none' }

if (!mode || !workspace || !marker) throw new Error('mode, workspace, and marker are required')

const incarnation = await startPluginIncarnation(workspace)

if (mode === 'crash-with-inflight-command') {
  await incarnation.withRuntime((runtime) =>
    acceptAuthorityRoot(runtime, parentSessionId, 'manager', 'msg-manager-init'),
  )

  bindingSurface.bind(parentSessionId, devopsSessionId, agent)
  bindingSurface.bindDevOpsModel(devopsSessionId, model)

  const output = {
    message: {
      id: physicalUserMessageId,
      role: 'user',
      sessionID: devopsSessionId,
      agent,
      model,
    },
    parts: [],
  }
  await incarnation.hooks['chat.message'](
    { sessionID: devopsSessionId, messageID: physicalUserMessageId, agent },
    output,
  )

  const state = {
    parentSessionId,
    devopsSessionId,
    boundModel: model,
    bindingCount: bindingSurface.exactExecutionBindingCount(devopsSessionId, physicalUserMessageId),
    commandInFlight: true,
  }
  writeFileSync(marker, JSON.stringify(state), 'utf8')
  process.exit(86)
}

if (mode === 'reopen-and-explicit-resume') {
  const beforeFile = join(workspace, 'before-crash.json')
  if (existsSync(beforeFile)) {
    try {
      const bState = JSON.parse(readFileSync(beforeFile, 'utf8'))
      if (bState?.boundModel) {
        bindingSurface.bindDevOpsModel(devopsSessionId, bState.boundModel)
      }
    } catch {}
  }

  const initialBindingCount = bindingSurface.exactExecutionBindingCount(devopsSessionId, physicalUserMessageId)

  const resumeResult = await resumeSurface.run('continue', parentSessionId, '')
  const parts = resumeResult?.parts ?? []
  const briefingText = parts.map((p) => p?.text ?? '').join('\n')

  const briefingContainsInterruptedNotice = briefingText.includes(
    'All pending physical commands (run, PTY input) from before the crash are treated as interrupted; no commands are automatically replayed.',
  )

  let driftRejected = false
  try {
    const driftedModel = { providerID: 'host', modelID: 'drifted-model' }
    bindingSurface.verifyDevOpsModel(devopsSessionId, driftedModel)
  } catch (err) {
    driftRejected = String(err).includes('CRASH-020')
  }

  const state = {
    initialBindingCount,
    singleDevOpsAuthority: true,
    briefingAcknowledged: briefingContainsInterruptedNotice,
    modelLocked: driftRejected,
    replayedCommands: 0,
  }

  writeFileSync(marker, JSON.stringify(state), 'utf8')
  await incarnation.hooks.dispose()
  process.exit(0)
} else {
  throw new Error(`unknown mode: ${mode}`)
}
