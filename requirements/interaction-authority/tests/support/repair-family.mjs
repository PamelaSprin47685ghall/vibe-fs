import * as HostSessionNudge from '../../../../dist/Interaction/Dispatch/OpenCode/SessionNudge.js'

/** Keep the semantic test unaware of Fable DU shape and deep module exports. */
export const sendRepairFamilyKind = async (...args) => {
  const outcome = await HostSessionNudge.trySendRepairFamily(...args)
  return outcome.cases()[outcome.tag]
}
