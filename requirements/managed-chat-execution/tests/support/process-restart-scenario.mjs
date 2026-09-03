import { writeFileSync } from 'node:fs'

import * as statusSurface from '../../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as bindingSurface from '../../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as routingSurface from '../../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import {
  acceptAuthorityRoot,
  startPluginIncarnation,
} from '../../../verification-system/tests/support/plugin-fixture.mjs'

const [mode, workspace, marker] = process.argv.slice(2)
const sessionId = 'ses-process-restart-canary'
const physicalUserMessageId = 'msg-process-restart-canary'
const decoyPhysicalUserMessageId = 'msg-process-restart-decoy'
const agent = 'fast-coder'

if (!mode || !workspace || !marker) throw new Error('mode, workspace, and marker are required')

const exactOwner = (value) =>
  value?.sessionId === sessionId && value?.physicalUserMessageId === physicalUserMessageId

const observe = async (withRuntime) => {
  const status = await withRuntime(({ journal }) =>
    statusSurface.query(journal, sessionId, physicalUserMessageId),
  )
  const decoyStatus = await withRuntime(({ journal }) =>
    statusSurface.query(journal, sessionId, decoyPhysicalUserMessageId),
  )
  const capacity = routingSurface.sharedCapacitySnapshot()
  const exactCapacity = {
    token: capacity.tokens.some((token) => exactOwner(token.owner)),
    custody: capacity.custodies.some((custody) => exactOwner(custody.owner)),
    execution: capacity.executions.some(exactOwner),
    waiter: capacity.waiters.some(exactOwner),
    owner: capacity.owners.some(exactOwner),
  }

  return {
    status: {
      accepted: status.accepted,
      providerStarted: status.providerStarted,
      terminal: status.terminal,
    },
    bindingCount: bindingSurface.exactExecutionBindingCount(sessionId, physicalUserMessageId),
    decoy: {
      status: {
        accepted: decoyStatus.accepted,
        providerStarted: decoyStatus.providerStarted,
        terminal: decoyStatus.terminal,
      },
      bindingCount: bindingSurface.exactExecutionBindingCount(sessionId, decoyPhysicalUserMessageId),
    },
    exactCapacity,
    capacityCounts: {
      ledgerEntries: capacity.ledgerEntries.length,
      tokens: capacity.tokens.length,
      custodies: capacity.custodies.length,
      executions: capacity.executions.length,
      waiters: capacity.waiters.length,
      owners: capacity.owners.length,
      lineage: capacity.lineage.length,
      active: capacity.activeCount,
    },
  }
}

const incarnation = await startPluginIncarnation(workspace)

if (mode === 'crash-after-accepted') {
  await incarnation.withRuntime((runtime) =>
    acceptAuthorityRoot(runtime, sessionId, agent, physicalUserMessageId),
  )
  const output = {
    message: {
      id: physicalUserMessageId,
      role: 'user',
      sessionID: sessionId,
      agent,
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [],
  }
  await incarnation.hooks['chat.message'](
    { sessionID: sessionId, messageID: physicalUserMessageId, agent },
    output,
  )
  writeFileSync(marker, JSON.stringify(await observe(incarnation.withRuntime)), 'utf8')
  process.exit(86)
}

if (mode === 'reopen-after-crash') {
  writeFileSync(marker, JSON.stringify(await observe(incarnation.withRuntime)), 'utf8')
  await incarnation.hooks.dispose()
} else {
  throw new Error(`unknown mode: ${mode}`)
}
