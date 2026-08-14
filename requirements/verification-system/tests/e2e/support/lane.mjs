export function bindLaneSession(provider, sessionID, ...aliases) {
  for (const alias of aliases) provider.bindSession(alias, sessionID);
}
