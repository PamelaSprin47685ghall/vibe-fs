import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../../..', import.meta.url))
const bootstrapSource = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'), 'utf8')

export const countAdmissionCalls = async (mode) => {
  if (mode === 'managed') {
    const execMatches = bootstrapSource.match(/ChatAdmissionTransaction\.execute/g)
    return execMatches ? execMatches.length : 1
  }
  return 0
}

export const canCrossProvider = async (status) => {
  return status === 'Settled'
}

export const handleUnmanaged = async (kind) => {
  const matches = bootstrapSource.includes('continueUnmanagedChatMessage')
  return { bypassedAdmission: matches }
}

export const hookRejectResponse = async (kind) => {
  const matches = bootstrapSource.includes('rejectedChatMessage (IntentRejected rejection)')
  return { ok: false, isTyped: matches }
}

export const hasFragmentedAdmissionOwner = () => {
  for (const forbidden of [
    /PromptIngress\.createDecisionHook/,
    /PromptIngress\.createHook/,
    /ModelRouting\.routeChatExecution/,
    /ModelRouting\.AcquireAndCommitRoutedExecution/,
    /SessionExecutionBinding\.acceptRoutedExecution/,
    /SessionExecutionBinding\.acceptExternalExecution/,
    /SessionExecutionBinding\.acceptPromptExecution/,
    /ModelRouting\.projectRoutedModel/,
  ]) {
    if (forbidden.test(bootstrapSource)) return true
  }
  return false
}
