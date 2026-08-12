/**
 * magic-todo-host-canary-plugin.mjs — test-only OpenCode plugin wrapper.
 *
 * Mirrors receipt-acceptance-gate-plugin.mjs: one outer plugin path that loads
 * the production plugin, then overlays observation hooks. Used by Long Stroke
 * A/E/G/H canaries (real Host lifetime; production membrane observed in place).
 *
 * Evidence files land in WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_DIR:
 *   definition.json  — production tool.definition obligation surface
 *   before.json      — pre/post before args + SDK localization (A/H)
 *   after.json       — after enrichment + ToolPart status during after (E/G/H)
 *   after-settled.json — post-return durable ToolPart (best-effort E)
 *   failure-*.json   — deterministic fail-closed records
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const magicTodoHostCanaryPluginPath = fileURLToPath(import.meta.url);


// Env is required only when OpenCode loads this file as the outer plugin.
// Harness imports (path + assert helpers) must succeed without these vars.
const directory = process.env.WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_DIR ?? null;
const pluginPath = process.env.WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_PLUGIN ?? null;
const loadedAsOuterPlugin = Boolean(directory && pluginPath);

const writeJson = (name, value) => {
  if (!directory) throw new Error('magic-todo host canary directory unset');
  fs.writeFileSync(path.join(directory, `${name}.json`), JSON.stringify(value, null, 2));
};

const writeFailure = (label, error, extra = {}) => {
  const payload = {
    label,
    error: String(error?.stack ?? error),
    ...extra,
  };
  writeJson(`failure-${label}`, payload);
  return payload;
};

const unwrapMessages = (response) => {
  if (Array.isArray(response)) return response;
  if (Array.isArray(response?.data)) return response.data;
  if (Array.isArray(response?.data?.data)) return response.data.data;
  if (Array.isArray(response?.data?.data?.data)) return response.data.data.data;
  return [];
};

const fetchMessages = async (client, sessionID) => {
  const messagesFn = client?.session?.messages;
  if (typeof messagesFn !== 'function') {
    throw new Error('plugin client.session.messages unavailable');
  }
  const response = await messagesFn.call(client.session, {
    path: { id: sessionID },
    query: { order: 'asc' },
  });
  return unwrapMessages(response);
};

const deepStable = (value) => JSON.stringify(value ?? null);

const hasOwn = (obj, key) => obj != null && Object.prototype.hasOwnProperty.call(obj, key);

const legacyProviderFields = ['id', 'kind', 'status', 'priority', 'content'];

const argsCarryObligations = (args) =>
  Array.isArray(args?.obligations)
  && args.obligations.length > 0
  && args.obligations.every((row) =>
    row
    && typeof row === 'object'
    && typeof row.name === 'string'
    && typeof row.work === 'string'
    && legacyProviderFields.every((field) => !hasOwn(row, field)));

const argsAreV1Compatibility = (args) =>
  !hasOwn(args, 'obligations')
  && Array.isArray(args?.todos)
  && args.todos.length > 0
  && args.todos.every((row) =>
    row
    && typeof row === 'object'
    && typeof row.content === 'string'
    && typeof row.status === 'string'
    && typeof row.priority === 'string'
    && !hasOwn(row, 'id')
    && !hasOwn(row, 'kind'));

const taggedValue = (value) =>
  Array.isArray(value) && value.length >= 2 ? value.at(-1) : value;

const journalCarrierFor = (locate, xTraceParts) => {
  const partID = locate?.match?.partID ?? null;
  const matches = (Array.isArray(xTraceParts) ? xTraceParts : []).filter((payload) => {
    if (!payload || typeof payload !== 'object') return false;
    if (taggedValue(payload.SessionId) !== locate?.sessionID) return false;
    if (taggedValue(payload.ToolCallId) !== locate?.callID) return false;
    if (partID && taggedValue(payload.HostToolPartId) !== partID) return false;
    return true;
  });
  if (matches.length !== 1) return { available: false, matchCount: matches.length };

  const payload = matches[0];
  const providerRun = taggedValue(payload.ProviderRun);
  const cursor = Number(taggedValue(payload.CursorSequence));
  if (typeof providerRun !== 'string' || providerRun.length === 0 || !Number.isSafeInteger(cursor)) {
    return { available: false, matchCount: 1 };
  }
  return {
    available: true,
    matchCount: 1,
    providerRun,
    xTraceRange: { start: cursor, endExclusive: cursor + 1 },
    hostToolPartID: taggedValue(payload.HostToolPartId) ?? null,
  };
};

/**
 * Carrier evidence for Canary H.
 * HOST-011: ProviderRunIdentity := assistant messageID; ToolCallId := callID.
 * Direct Host ToolPart has no XTrace field, so the durable XTrace fact completes
 * the carrier by SessionId + ToolCallId + HostToolPartId.
 */
export const buildCarrierEvidence = (locate, xTraceParts = []) => {
  const match = locate?.match ?? null;
  const part = match?.part ?? null;
  const toolPartKeys = part && typeof part === 'object' ? Object.keys(part) : [];
  const toolPartProviderRunID =
    part?.providerRunID
    ?? part?.providerRunId
    ?? part?.provider_run_id
    ?? null;
  const toolPartXTrace =
    part?.xTrace
    ?? part?.XTrace
    ?? part?.xtrace
    ?? part?.xTraceRange
    ?? part?.XTraceRange
    ?? null;
  const assistantMessageID = match?.assistant?.id ?? match?.messageID ?? null;
  const providerRunFromMessageID =
    typeof assistantMessageID === 'string' && assistantMessageID.length > 0
      ? assistantMessageID
      : null;

  const directHostLacksProviderRunField = toolPartProviderRunID == null;
  const directHostLacksXTraceField = toolPartXTrace == null;
  const journal = journalCarrierFor(locate, xTraceParts);
  const journalMappingAvailable = journal.available === true;
  const journalProviderRun = journalMappingAvailable ? journal.providerRun : null;
  const journalXTraceRange = journalMappingAvailable ? journal.xTraceRange : null;

  const unique = locate?.unique === true && locate?.matchCount === 1;
  const hasOrdinals =
    match != null
    && Number.isInteger(match.ordinal)
    && Number.isInteger(match.toolOrdinal);
  const hasAssistant = typeof assistantMessageID === 'string' && assistantMessageID.length > 0;

  const providerRun = journalProviderRun ?? toolPartProviderRunID ?? providerRunFromMessageID;
  const xTraceRange = journalXTraceRange ?? toolPartXTrace;
  const providerRunConsistent =
    journalProviderRun == null
    || providerRunFromMessageID == null
    || journalProviderRun === providerRunFromMessageID;
  const carrierMappingComplete =
    unique
    && hasAssistant
    && hasOrdinals
    && providerRunConsistent
    && typeof providerRun === 'string'
    && providerRun.length > 0
    && xTraceRange != null;

  return {
    sessionID: locate?.sessionID ?? null,
    callID: locate?.callID ?? null,
    unique,
    matchCount: locate?.matchCount ?? 0,
    messageID: assistantMessageID,
    partID: match?.partID ?? null,
    ordinal: match?.ordinal ?? null,
    toolOrdinal: match?.toolOrdinal ?? null,
    providerRunFromMessageID,
    toolPartProviderRunID,
    toolPartXTrace,
    toolPartKeys,
    journalMappingAvailable,
    journalMappingMatchCount: journal.matchCount ?? 0,
    journalProviderRun,
    journalXTraceRange,
    providerRun,
    xTraceRange,
    directHostLacksProviderRunField,
    directHostLacksXTraceField,
    providerRunConsistent,
    carrierMappingComplete,
    note: carrierMappingComplete
      ? 'Direct Host identity is completed by the durable XTrace call/part carrier.'
      : 'Direct Host ToolPart has no providerRunID/XTrace; durable XTrace mapping is missing, ambiguous, or inconsistent.',
  };
};

/**
 * sessionID + callID → complete SDK snapshot localization (Canary H).
 * Fail-closed surface: matchCount must be exactly 1 for membrane identity freeze.
 */
export const locateToolPartByCall = (messages, sessionID, callID) => {
  const matches = [];
  for (const row of messages ?? []) {
    const info = row?.info ?? row ?? {};
    const parts = Array.isArray(row?.parts) ? row.parts : [];
    const messageID = info.id ?? parts[0]?.messageID ?? null;
    const messageSession = info.sessionID ?? parts[0]?.sessionID ?? null;
    if (sessionID && messageSession && messageSession !== sessionID) continue;

    let toolOrdinal = -1;
    parts.forEach((part, ordinal) => {
      if (part?.type !== 'tool') return;
      toolOrdinal += 1;
      if (part.callID !== callID) return;
      if (part.sessionID && sessionID && part.sessionID !== sessionID) return;
      matches.push({
        sessionID: part.sessionID ?? messageSession ?? sessionID,
        callID: part.callID,
        messageID: part.messageID ?? messageID,
        partID: part.id ?? null,
        ordinal,
        toolOrdinal,
        tool: part.tool,
        status: part.state?.status ?? null,
        input: part.state?.input,
        output: part.state?.output,
        part,
        assistant: {
          id: messageID,
          role: info.role ?? null,
          parentID: info.parentID ?? null,
          providerID: info.providerID ?? null,
          modelID: info.modelID ?? null,
          agent: info.agent ?? null,
          finish: info.finish ?? null,
        },
        retryParts: parts
          .filter((p) => p?.type === 'retry')
          .map((p) => ({
            id: p.id,
            attempt: p.attempt ?? p.state?.attempt ?? null,
            error: p.error ?? p.state?.error ?? null,
          })),
        stepParts: parts
          .filter((p) => p?.type === 'step-start' || p?.type === 'step-finish')
          .map((p) => ({ id: p.id, type: p.type, reason: p.reason ?? null })),
      });
    });
  }

  const locate = {
    sessionID,
    callID,
    matchCount: matches.length,
    unique: matches.length === 1,
    match: matches.length === 1 ? matches[0] : null,
    matches: matches.map((m) => ({
      messageID: m.messageID,
      partID: m.partID,
      ordinal: m.ordinal,
      toolOrdinal: m.toolOrdinal,
      status: m.status,
      providerID: m.assistant?.providerID ?? null,
      modelID: m.assistant?.modelID ?? null,
    })),
  };
  locate.carrier = buildCarrierEvidence(locate);
  return locate;
};

// ── Artifact helpers (entry assertions) ─────────────────────────────────────

export const hostCanaryArtifactPath = (dir, name) => path.join(dir, `${name}.json`);

export const readHostCanaryArtifact = (dir, name) => {
  const file = hostCanaryArtifactPath(dir, name);
  if (!fs.existsSync(file)) return null;
  return JSON.parse(fs.readFileSync(file, 'utf8'));
};

export const listHostCanaryFailures = (dir) => {
  if (!dir || !fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((file) => /^failure-.*\.json$/.test(file))
    .map((file) => JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8')));
};

/**
 * Collect provider-wire tool names seen on Manager-lane chat requests.
 * Host evidence for whether builtin `todowrite` was ever advertised.
 *
 * @param {object} scenario long-stroke Scenario (provider + optional child session)
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
    typeof provider?.sessionFor === 'function' ? provider.sessionFor('fast-manager') : null;

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
  const todowriteAdvertised = unionTools.includes('todowrite');
  return {
    managerSessionIds: [...managerSessions],
    requestCount: chatRequests.length,
    unionTools,
    todowriteAdvertised,
    samples: chatRequests.slice(0, 8),
  };
};

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
 * Assert real-host canaries A / E / G / H from pure-observer wrapper artifacts.
 * Manager must advertise the production `todowrite` membrane; missing surface,
 * legacy provider fields, alias drift, or incomplete carrier evidence fail closed.
 *
 * @param {string} dir
 * @param {{ managerProviderWire?: ReturnType<typeof collectManagerProviderToolEvidence> | null, xTraceParts?: object[] }} [opts]
 */
export const assertMagicTodoHostCanariesAEGH = (dir, opts = {}) => {
  const managerProviderWire = opts.managerProviderWire ?? null;
  if (!managerProviderWire || managerProviderWire.requestCount === 0) {
    throw new Error('HOST_CANARY_MANAGER: missing Manager provider-wire evidence');
  }
  if (!managerProviderWire.todowriteAdvertised) {
    throw new Error(
      `HOST_CANARY_MANAGER: Manager provider wire must advertise the production todowrite membrane: ${JSON.stringify(managerProviderWire)}`,
    );
  }
  for (const required of ['fork', 'horizon', 'join', 'fission', 'todowrite', 'suicide']) {
    if (!managerProviderWire.unionTools.includes(required)) {
      throw new Error(`HOST_CANARY_MANAGER: missing Manager tool ${required}`);
    }
  }

  const failures = listHostCanaryFailures(dir);
  if (failures.length > 0) {
    throw new Error(
      `magic-todo host canary recorded failure artifacts: ${JSON.stringify(failures[0])}`,
    );
  }

  const before = readHostCanaryArtifact(dir, 'before');
  const after = readHostCanaryArtifact(dir, 'after');
  const settled = readHostCanaryArtifact(dir, 'after-settled');
  const definition = readHostCanaryArtifact(dir, 'definition');

  if (!definition) throw new Error('HOST_CANARY_B: missing definition.json (todowrite definition never observed)');
  if (!before) throw new Error('HOST_CANARY_A/H: missing before.json (todowrite before never observed)');
  if (!after) throw new Error('HOST_CANARY_E/G/H: missing after.json (todowrite after never observed)');
  if (definition.advertisesObligations !== true || definition.leaksLegacyProviderFields === true) {
    throw new Error(`HOST_CANARY_B: production definition must expose obligations{name,work} only: ${JSON.stringify(definition)}`);
  }

  // ── A: durable provider obligations vs executor compatibility args ────────
  if (before.snapshotError) {
    throw new Error(`HOST_CANARY_A: before snapshot error: ${before.snapshotError}`);
  }
  if (before.argsIdentityUnchanged !== true) {
    throw new Error('HOST_CANARY_A: production before must mutate the original args object in place');
  }
  if (!argsCarryObligations(before.preBeforeArgs)) {
    throw new Error('HOST_CANARY_A: pre-before args must carry only provider obligations{name,work}');
  }
  if (!argsAreV1Compatibility(before.postBeforeArgs)) {
    throw new Error('HOST_CANARY_A: post-before args must be the Host V1 compatibility sink');
  }
  if (!argsAreV1Compatibility(after.executorArgs)) {
    throw new Error('HOST_CANARY_A: executor args must stay on the V1 compatibility sink');
  }
  if (before.durableInput == null) {
    throw new Error('HOST_CANARY_A: durable ToolPart.input missing during before');
  }
  if (before.durableInputEqualsPreBefore !== true) {
    throw new Error('HOST_CANARY_A: durable ToolPart.input must remain the pre-before obligation account');
  }

  // ── E: production after enrichment durable + model-visible ───────────────
  if (typeof after.enrichedOutput !== 'string' || after.enrichedOutput.length === 0) {
    throw new Error('HOST_CANARY_E: production after must expose a non-empty provider result');
  }
  if (after.enrichedOutput === after.originalOutput) {
    throw new Error('HOST_CANARY_E: production after did not enrich the builtin todowrite result');
  }
  if (!after.enrichedOutput.includes('Current obligations:')) {
    throw new Error(`HOST_CANARY_E: enriched result lacks the canonical obligation account: ${JSON.stringify(after.enrichedOutput)}`);
  }
  // Settled snapshot is best-effort; when present it must show the same enriched bytes.
  if (settled?.locate?.match?.output != null && settled.locate.match.output !== after.enrichedOutput) {
    throw new Error(
      `HOST_CANARY_E: durable ToolPart.output after settle must equal production enriched bytes, got ${JSON.stringify(settled.locate.match.output)}`,
    );
  }
  if (after.locateDuringAfter?.match?.status === 'completed') {
    const duringOut = after.locateDuringAfter.match.output;
    if (duringOut != null && duringOut !== after.enrichedOutput && duringOut !== after.originalOutput) {
      throw new Error(`HOST_CANARY_E: unexpected ToolPart.output during after: ${JSON.stringify(duringOut)}`);
    }
  }

  // ── G: freeze ToolPart completion state observed during after ────────────
  if (after.snapshotError) {
    throw new Error(`HOST_CANARY_G: after snapshot error: ${after.snapshotError}`);
  }
  if (after.locateDuringAfter == null) {
    throw new Error('HOST_CANARY_G: missing locateDuringAfter snapshot');
  }
  if (after.locateDuringAfter.unique !== true || after.locateDuringAfter.matchCount !== 1) {
    throw new Error(
      `HOST_CANARY_G: during-after ToolPart must be unique (matchCount=${after.locateDuringAfter.matchCount})`,
    );
  }
  const statusDuring = after.toolPartStatusDuringAfter ?? after.locateDuringAfter.match?.status ?? null;
  if (typeof statusDuring !== 'string' || statusDuring.length === 0) {
    throw new Error('HOST_CANARY_G: ToolPart status during after must be a non-empty string freeze');
  }
  // Record-only freeze: do not require a particular order (protocol is dual-path).
  if (typeof after.durableCompletedDuringAfter !== 'boolean') {
    throw new Error('HOST_CANARY_G: durableCompletedDuringAfter boolean freeze missing');
  }
  // Persist the freeze into a dedicated summary field via re-readability.
  if (after.toolPartStatusDuringAfter !== statusDuring) {
    throw new Error('HOST_CANARY_G: toolPartStatusDuringAfter inconsistent with locate match');
  }

  // ── H: unique session/call localization + carrier fail-closed evidence ───
  const locateH = after.locateDuringAfter;
  if (locateH.sessionID !== after.sessionID || locateH.callID !== after.callID) {
    throw new Error('HOST_CANARY_H: locate sessionID/callID mismatch vs after evidence');
  }
  if (before.sessionID !== after.sessionID || before.callID !== after.callID) {
    throw new Error('HOST_CANARY_H: before/after sessionID+callID must agree');
  }
  if (locateH.unique !== true || locateH.matchCount !== 1) {
    throw new Error(
      `HOST_CANARY_H: sessionID+callID must uniquely locate ToolPart (matchCount=${locateH.matchCount})`,
    );
  }
  const match = locateH.match;
  if (!match?.messageID || !Number.isInteger(match.ordinal) || !Number.isInteger(match.toolOrdinal)) {
    throw new Error('HOST_CANARY_H: unique match must carry messageID + part/tool ordinals');
  }
  const carrier = buildCarrierEvidence(locateH, opts.xTraceParts ?? []);
  if (!carrier.carrierMappingComplete) {
    throw new Error(
      `HOST_CANARY_H_BLOCKED: sessionID+callID lacks provider-run/XTrace range mapping: ${JSON.stringify(carrier)}`,
    );
  }

  // Definition observation is informational for A/E/G/H entry (B is unit-owned).
  return {
    ok: true,
    canaries: {
      A: {
        durableInputEqualsPreBefore: before.durableInputEqualsPreBefore,
        executorV1: argsAreV1Compatibility(after.executorArgs),
        preBeforeObligations: argsCarryObligations(before.preBeforeArgs),
      },
      E: {
        enrichedOutput: after.enrichedOutput,
        settledOutput: settled?.locate?.match?.output ?? null,
      },
      G: {
        toolPartStatusDuringAfter: statusDuring,
        durableCompletedDuringAfter: after.durableCompletedDuringAfter,
      },
      H: {
        sessionID: after.sessionID,
        callID: after.callID,
        messageID: match.messageID,
        ordinal: match.ordinal,
        toolOrdinal: match.toolOrdinal,
        carrier,
      },
    },
    definitionObserved: definition != null,
  };
};

let production = null;
if (loadedAsOuterPlugin) {
  const wrapped = await import(pathToFileURL(pluginPath).href);
  production = wrapped.default;
}

export default {
  id: 'wanxiangshu-e2e-magic-todo-host-canary',
  async server(input) {
    if (!loadedAsOuterPlugin || !production) {
      throw new Error('magic-todo host canary requires directory and production plugin path');
    }
    const hooks = await production.server(input);
    const client = input.client;
    const productionDefinition = hooks['tool.definition'];
    const productionBefore = hooks['tool.execute.before'];
    const productionAfter = hooks['tool.execute.after'];

    return {
      ...hooks,
      'tool.definition': async (hookInput, hookOutput) => {
        await productionDefinition?.(hookInput, hookOutput);
        if (hookInput?.toolID !== 'todowrite') return;
        try {
          const item = hookOutput?.parameters?.properties?.obligations?.items ?? null;
          const properties = item?.properties ?? {};
          writeJson('definition', {
            toolID: hookInput.toolID,
            description: hookOutput?.description ?? null,
            hasParameters: hookOutput?.parameters != null,
            hasJsonSchema: hookOutput?.jsonSchema != null,
            advertisesObligations:
              Array.isArray(hookOutput?.parameters?.required)
              && hookOutput.parameters.required.includes('obligations')
              && Array.isArray(item?.required)
              && item.required.includes('name')
              && item.required.includes('work'),
            leaksLegacyProviderFields:
              ['id', 'kind', 'status', 'priority', 'content'].some((field) => hasOwn(properties, field)),
            observedAt: Date.now(),
          });
        } catch (error) {
          writeFailure('definition', error);
        }
      },

      'tool.execute.before': async (hookInput, hookOutput) => {
        if (hookInput?.tool !== 'todowrite') {
          return productionBefore?.(hookInput, hookOutput);
        }

        const sessionID = hookInput.sessionID;
        const callID = hookInput.callID;
        const args = hookOutput?.args;
        const preBeforeArgs = structuredClone(args);
        const argsRef = args;

        let locatePre = null;
        let locatePost = null;
        let snapshotError = null;
        try {
          const snapPre = await fetchMessages(client, sessionID);
          locatePre = locateToolPartByCall(snapPre, sessionID, callID);
        } catch (error) {
          snapshotError = String(error?.stack ?? error);
        }

        // Pure observer: production owns the in-place obligations → V1 sink
        // mutation. The wrapper records the alias boundary but never changes it.
        await productionBefore?.(hookInput, hookOutput);

        try {
          const snapPost = await fetchMessages(client, sessionID);
          locatePost = locateToolPartByCall(snapPost, sessionID, callID);
        } catch (error) {
          snapshotError = snapshotError ?? String(error?.stack ?? error);
        }

        const durableInput = locatePost?.match?.input;
        const evidence = {
          sessionID,
          callID,
          preBeforeArgs,
          postBeforeArgs: structuredClone(args),
          argsIdentityUnchanged: hookOutput?.args === argsRef,
          locatePre,
          locatePost,
          durableInput,
          durableInputEqualsPreBefore: deepStable(durableInput) === deepStable(preBeforeArgs),
          preBeforeCarriesObligations: argsCarryObligations(preBeforeArgs),
          postBeforeIsV1: argsAreV1Compatibility(args),
          snapshotError,
          observedAt: Date.now(),
        };

        try {
          writeJson('before', evidence);
        } catch (error) {
          writeFailure('before-write', error, { sessionID, callID });
        }
      },

      'tool.execute.after': async (hookInput, hookOutput) => {
        if (hookInput?.tool !== 'todowrite') {
          return productionAfter?.(hookInput, hookOutput);
        }

        const sessionID = hookInput.sessionID;
        const callID = hookInput.callID;
        const originalOutput = hookOutput?.output;
        const originalTitle = hookOutput?.title;

        let locateDuringAfter = null;
        let snapshotError = null;
        try {
          const snap = await fetchMessages(client, sessionID);
          locateDuringAfter = locateToolPartByCall(snap, sessionID, callID);
        } catch (error) {
          snapshotError = String(error?.stack ?? error);
        }

        // Pure observer: production owns result enrichment.
        await productionAfter?.(hookInput, hookOutput);

        const evidence = {
          sessionID,
          callID,
          originalOutput,
          originalTitle,
          enrichedOutput: hookOutput?.output ?? null,
          executorArgs: structuredClone(hookInput.args),
          locateDuringAfter,
          toolPartStatusDuringAfter: locateDuringAfter?.match?.status ?? null,
          durableCompletedDuringAfter: locateDuringAfter?.match?.status === 'completed',
          partOrdinalDuringAfter: locateDuringAfter?.match?.ordinal ?? null,
          toolOrdinalDuringAfter: locateDuringAfter?.match?.toolOrdinal ?? null,
          messageIDDuringAfter: locateDuringAfter?.match?.messageID ?? null,
          snapshotError,
          observedAt: Date.now(),
        };

        try {
          writeJson('after', evidence);
        } catch (error) {
          writeFailure('after-write', error, { sessionID, callID });
        }

        // Best-effort settled snapshot (AI SDK may complete the part after after returns).
        void Promise.resolve()
          .then(async () => {
            try {
              const snap = await fetchMessages(client, sessionID);
              const locate = locateToolPartByCall(snap, sessionID, callID);
              writeJson('after-settled', {
                sessionID,
                callID,
                locate,
                enrichedOutput: hookOutput?.output ?? null,
                durableOutput: locate?.match?.output ?? null,
                durableOutputEqualsEnriched: locate?.match?.output === hookOutput?.output,
                observedAt: Date.now(),
              });
            } catch (error) {
              writeFailure('after-settled', error, { sessionID, callID });
            }
          })
          .catch(() => {});
      },
    };
  },
};
