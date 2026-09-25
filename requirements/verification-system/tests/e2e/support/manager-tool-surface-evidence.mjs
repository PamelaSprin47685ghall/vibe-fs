/**
 * manager-tool-surface-evidence.mjs — production Manager tool-surface evidence.
 *
 * The Manager provider surface is observed on the wire the Host actually sent: which
 * tools were advertised, on the Manager session, and whether the retired ledger tool
 * (`todowrite`) ever came back. Nothing here reads the SDK's shape or the scenario's
 * expectation: `requests` holds requests as they arrived.
 *
 * ── why the wrapper-plugin membrane canary is gone ──────────────────────────
 *
 * This module used to ship a test-only outer plugin that wrapped the production plugin
 * and recorded the `todowrite` membrane (definition decoration, before-rewritten args,
 * non-enumerable V1 compatibility view, after-hook enrichment, frozen ToolPart status)
 * into artifacts. That membrane no longer exists: the session cognitive write entry is
 * the plain `assume` tool (`OpenCode/Tools/AssumeTool.fs`), which decodes `update` and
 * `todos` and renders the committed canvas itself — no provider-arg rewriting and no
 * after-hook result enrichment to observe. Keeping the wrapper would assert a retired
 * product behavior, so the surface claim is now proven by wire evidence alone
 * (host-boundary-019 owns its own membrane carriers).
 */

const extractToolNamesFromRequest = (request) => {
  const tools = request?.tools;
  if (!Array.isArray(tools)) return [];
  const out = [];
  for (const tool of tools) {
    const name = tool?.function?.name ?? tool?.name;
    if (typeof name === 'string') out.push(name);
  }
  return out;
};

/**
 * Collect provider-wire tool names seen on Manager-lane chat requests.
 *
 * @param {object} scenario Long Stroke Scenario (provider + optional bound child session)
 * @param {{ childSessionId?: string | null }} [opts]
 */
export const collectManagerProviderToolEvidence = (scenario, opts = {}) => {
  const provider = scenario?.provider;
  const requests = Array.isArray(provider?.requests) ? provider.requests : [];
  const childSessionId =
    typeof opts.childSessionId === 'string' && opts.childSessionId.length > 0
      ? opts.childSessionId
      : null;
  const boundManager =
    typeof provider?.sessionFor === 'function' ? provider.sessionFor('manager') : null;

  const managerSessions = new Set(
    [childSessionId, boundManager].filter((id) => typeof id === 'string' && id.length > 0),
  );

  const chatRequests = [];
  for (const request of requests) {
    const sessionID = request?.sessionID ?? request?.sessionId ?? null;
    if (managerSessions.size > 0) {
      if (!sessionID || !managerSessions.has(sessionID)) continue;
    }
    // Without a bound Manager session, only consider requests that already carry tools
    // and a manager-shaped tool surface (fork+join+horizon+suicide) — never invent matches.
    const tools = extractToolNamesFromRequest(request);
    if (managerSessions.size === 0) {
      const hasManagerSpine =
        tools.includes('fork')
        && tools.includes('join')
        && tools.includes('horizon')
        && tools.includes('suicide');
      if (!hasManagerSpine) continue;
    }
    chatRequests.push({
      sessionID,
      tools,
      messageCount: Array.isArray(request?.messages) ? request.messages.length : 0,
    });
  }

  const unionTools = [...new Set(chatRequests.flatMap((row) => row.tools))].sort();
  return {
    managerSessionIds: [...managerSessions],
    requestCount: chatRequests.length,
    unionTools,
    assumeAdvertised: unionTools.includes('assume'),
    // Recorded, never asserted as a sum: the retired ledger tool must be absent.
    todowriteAdvertised: unionTools.includes('todowrite'),
    samples: chatRequests.slice(0, 8),
  };
};

/**
 * Assert the Manager wire surface is the production one. `assume` is the session
 * cognitive write entry, the Manager spine is present, and the retired `todowrite`
 * ledger never appears.
 *
 * @param {{ managerProviderWire?: ReturnType<typeof collectManagerProviderToolEvidence> | null }} [opts]
 */
export const assertManagerToolSurface = (opts = {}) => {
  const wire = opts.managerProviderWire ?? null;
  if (!wire || wire.requestCount === 0) {
    throw new Error('MANAGER_TOOL_SURFACE: missing Manager provider-wire evidence');
  }
  if (!wire.assumeAdvertised) {
    throw new Error(
      `MANAGER_TOOL_SURFACE: Manager provider wire must advertise the production assume cognitive surface: ${JSON.stringify(wire)}`,
    );
  }
  if (wire.todowriteAdvertised) {
    throw new Error(
      `MANAGER_TOOL_SURFACE: Manager provider wire must not advertise the retired todowrite ledger: ${JSON.stringify(wire)}`,
    );
  }
  for (const required of ['fork', 'horizon', 'join', 'resume', 'assume', 'suicide']) {
    if (!wire.unionTools.includes(required)) {
      throw new Error(`MANAGER_TOOL_SURFACE: missing Manager tool ${required}`);
    }
  }
  return { ok: true, unionTools: wire.unionTools, requestCount: wire.requestCount };
};