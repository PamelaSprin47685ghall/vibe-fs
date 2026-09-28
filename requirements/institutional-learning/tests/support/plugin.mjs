import { acceptAuthorityRoot, activateLife } from '../../../verification-system/tests/support/plugin-fixture.mjs'

export const context = (sessionID, callID) => ({ sessionID, agent: 'engineer', messageID: `run-${sessionID}`, callID })
export const admit = async (runtime, sessionID) => {
  await acceptAuthorityRoot(runtime, sessionID, 'engineer', `root-${sessionID}`)
  await activateLife(runtime, sessionID, `root-${sessionID}`)
}
