export function expectationLane(scenario, session, role, turn, requestKind = 'chat') {
  return { scenario, session, role, turn, requestKind };
}
