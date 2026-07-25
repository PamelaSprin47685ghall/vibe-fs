export function expectationLane(scenario, session, role, turn, requestKind = 'chat', parentSession = undefined) {
  return parentSession === undefined
    ? { scenario, session, role, turn, requestKind }
    : { scenario, session, role, turn, requestKind, parentSession };
}

export function bindLaneSession(provider, sessionID, ...aliases) {
  for (const alias of aliases) provider.bindSession(alias, sessionID);
}
