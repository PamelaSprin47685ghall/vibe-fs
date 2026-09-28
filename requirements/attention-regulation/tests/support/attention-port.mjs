import * as toolModule from '@opencode-ai/plugin'
import * as attention from '../../../../dist/Interaction/Attention/Surface.js'
import * as tools from '../../../../dist/OpenCode/Tools/AttentionToolSurface.js'

export { attention, tools, toolModule }
export const context = (sessionID = 'session-a', callID = 'call-1') => ({ sessionID, callID, messageID: 'run-1' })

export function recordingPort() {
  const fixture = { state: attention.empty(), reads: 0, appends: [], accept: true, fault: null }
  fixture.tools = tools.create(toolModule, () => {
    fixture.reads += 1
    return fixture.state
  }, async (session, providerRun, fact) => {
    fixture.appends.push({ session, providerRun, fact })
    if (fixture.fault) throw fixture.fault
    if (!fixture.accept) return false
    fixture.state = attention.record(fact.session, fact.occurrence, fact.text, fixture.state)
    return true
  })
  return fixture
}
