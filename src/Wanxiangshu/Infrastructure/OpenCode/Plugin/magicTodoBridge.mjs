/**
 * Magic Todo ephemeral before→after bridge (protocol §12).
 *
 * Speculative / unwired: not registered in SpikePlugin yet.
 * Process-local only — AgentJournal remains durable truth.
 * Crash recovery MUST ignore this Map.
 */

const MagicTodoBridge = Symbol("wanxiangshu.magic-todo.bridge")

/** @type {Map<string, object>} */
const bridges = new Map()

/**
 * @param {string} sessionID
 * @param {string} callID
 * @param {object} value — settledOld, normalizedProposal, previousReview, revisePreview, compatibilityProjection
 */
export function installMagicTodoBridge(sessionID, callID, value) {
  const key = `${sessionID}:${callID}`
  const carrier = {}
  Object.defineProperty(carrier, MagicTodoBridge, {
    enumerable: false,
    configurable: false,
    writable: false,
    value,
  })
  bridges.set(key, carrier)
}

/**
 * @param {string} sessionID
 * @param {string} callID
 * @returns {object | undefined}
 */
export function takeMagicTodoBridge(sessionID, callID) {
  const key = `${sessionID}:${callID}`
  const carrier = bridges.get(key)
  bridges.delete(key)
  return carrier?.[MagicTodoBridge]
}

/**
 * @param {string} sessionID
 * @param {string} callID
 */
export function clearMagicTodoBridge(sessionID, callID) {
  bridges.delete(`${sessionID}:${callID}`)
}

/** Drop all bridges for a session (tool/turn failure cleanup). */
export function clearMagicTodoBridgesForSession(sessionID) {
  const prefix = `${sessionID}:`
  for (const key of [...bridges.keys()]) {
    if (key.startsWith(prefix)) bridges.delete(key)
  }
}

export { MagicTodoBridge }
