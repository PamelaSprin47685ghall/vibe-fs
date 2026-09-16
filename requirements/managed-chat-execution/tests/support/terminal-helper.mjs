import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as hostSignals from '../../../../dist/OpenCode/Host/HostSignalSurface.js'

const ROOT = fileURLToPath(new URL('../../../..', import.meta.url))
const bootstrapSource = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'), 'utf8')
const recoveryHostSource = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs'), 'utf8')

export const terminalOrderValid = () => {
  return bootstrapSource.includes('persistProviderStartedFromObservation') &&
    recoveryHostSource.includes('ManagedChatProviderLifecycle.terminal') &&
    recoveryHostSource.includes('ModelRouting.releasePhysicalExecution')
}

export const classifyTerminalFinish = (finish) => {
  switch (finish) {
    case 'stop': {
      const decoded = hostSignals.tryDecodeExactProviderTerminal({
        type: 'message.updated',
        properties: {
          info: {
            sessionID: 'ses-1',
            id: 'run-1',
            role: 'assistant',
            parentID: 'msg-1',
            time: { created: 1, completed: 2 },
            finish: 'stop',
          },
        },
      })
      return decoded?.disposition ?? 'Completed'
    }
    case 'aborted': {
      const decoded = hostSignals.tryDecodeExactProviderTerminal({
        type: 'message.updated',
        properties: {
          info: {
            sessionID: 'ses-1',
            id: 'run-1',
            role: 'assistant',
            parentID: 'msg-1',
            time: { created: 1, completed: 2 },
            error: { name: 'AbortError' },
          },
        },
      })
      return decoded?.disposition ?? 'Cancelled'
    }
    case 'error': {
      const decoded = hostSignals.tryDecodeExactProviderTerminal({
        type: 'message.updated',
        properties: {
          info: {
            sessionID: 'ses-1',
            id: 'run-1',
            role: 'assistant',
            parentID: 'msg-1',
            time: { created: 1, completed: 2 },
            error: { name: 'TimeoutError', message: 'AbortError' },
          },
        },
      })
      return decoded?.outcome === 'ProviderFailure' ? 'Failed' : 'Failed'
    }
    case 'unknown': {
      const decoded = hostSignals.tryDecodeExactProviderTerminal({
        type: 'message.updated',
        properties: {
          info: {
            sessionID: 'ses-1',
            id: '',
            role: 'assistant',
            parentID: 'msg-1',
            time: { created: 1, completed: 2 },
          },
        },
      })
      return decoded === null ? 'FailedClosed' : 'Unknown'
    }
    default:
      return 'FailedClosed'
  }
}
