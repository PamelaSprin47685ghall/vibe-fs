/**
 * magic-todo-host-canary-plugin.mjs — test-only OpenCode plugin wrapper.
 *
 * Mirrors receipt-acceptance-gate-plugin.mjs: one outer plugin path that loads
 * the production plugin, then overlays observation hooks. Used by Long Stroke
 * Phase 0 canaries A/E/G/H (real Host lifetime; no production membrane).
 *
 * Evidence files land in WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_DIR:
 *   definition.json  — tool.definition observation (todowrite V2 ad surface)
 *   before.json      — pre/post before args + SDK localization (A/H)
 *   after.json       — after enrichment + ToolPart status during after (E/G/H)
 *   after-settled.json — post-return durable ToolPart (best-effort E)
 *   failure-*.json   — deterministic fail-closed records
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const magicTodoHostCanaryPluginPath = fileURLToPath(import.meta.url);

/** In-place mutation marker; must NOT appear on durable pre-before ToolPart.input (A). */
export const MAGIC_TODO_HOST_CANARY_MUTATION = '__magic_todo_host_canary_mut__';
/** after rewrite bytes; must reach model-visible + durable history (E). */
export const MAGIC_TODO_HOST_CANARY_ENRICHED = 'MAGIC_TODO_HOST_CANARY_ENRICHED_RESULT_V1';

/** V2 provider-facing advertisement installed via tool.definition (Phase 0 observe). */
export const MAGIC_TODO_HOST_CANARY_V2_ADVERTISEMENT = Object.freeze({
  description:
    'Magic Todo V2 host-canary: tagged id/kind rows and reviewing status. Host sink remains V1.',
  parameters: {
    type: 'object',
    properties: {
      todos: {
        type: 'array',
        items: {
          type: 'object',
          properties: {
            content: { type: 'string' },
            status: {
              type: 'string',
              enum: ['pending', 'in_progress', 'reviewing', 'completed', 'cancelled'],
            },
            priority: { type: 'string', enum: ['high', 'medium', 'low'] },
            id: { type: 'string' },
            kind: { type: 'string', enum: ['existing', 'new'] },
          },
          required: ['content', 'status', 'priority', 'id', 'kind'],
        },
      },
    },
    required: ['todos'],
  },
  jsonSchema: {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    type: 'object',
    properties: {
      todos: {
        type: 'array',
        items: {
          type: 'object',
          properties: {
            content: { type: 'string' },
            status: {
              type: 'string',
              enum: ['pending', 'in_progress', 'reviewing', 'completed', 'cancelled'],
            },
            priority: { type: 'string', enum: ['high', 'medium', 'low'] },
            id: { type: 'string' },
            kind: { type: 'string', enum: ['existing', 'new'] },
          },
          required: ['content', 'status', 'priority', 'id', 'kind'],
        },
      },
    },
    required: ['todos'],
  },
});

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

/** Strip V2 identity fields in place so executor sees V1 compatibility rows (A/C). */
export const stripV2TodoIdentityFieldsInPlace = (args) => {
  if (!args || typeof args !== 'object' || !Array.isArray(args.todos)) return args;
  const next = args.todos.map((row) => {
    if (!row || typeof row !== 'object') return row;
    const { id: _id, kind: _kind, ...v1 } = row;
    return v1;
  });
  args.todos = next;
  return args;
};

const rowHasV2Identity = (row) =>
  row != null && typeof row === 'object' && (hasOwn(row, 'id') || hasOwn(row, 'kind'));

const argsCarryV2Identity = (args) =>
  Array.isArray(args?.todos) && args.todos.some(rowHasV2Identity);

const argsAreV1Compatibility = (args) =>
  Array.isArray(args?.todos)
  && args.todos.length > 0
  && args.todos.every((row) => row && typeof row === 'object' && !rowHasV2Identity(row));

/**
 * Carrier evidence for Canary H.
 * HOST-011: ProviderRunIdentity := assistant messageID; ToolCallId := callID.
 * HOST-011 defines the assistant message ID as ProviderRunIdentity. Direct Host
 * ToolPart has no XTrace field; without a journal mapping, that missing range blocks.
 */
export const buildCarrierEvidence = (locate) => {
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
  // Phase 0: no production journal callID→XTrace mapping is wired for todowrite.
  const journalMappingAvailable = false;
  const journalProviderRun = null;
  const journalXTraceRange = null;

  const unique = locate?.unique === true && locate?.matchCount === 1;
  const hasOrdinals =
    match != null
    && Number.isInteger(match.ordinal)
    && Number.isInteger(match.toolOrdinal);
  const hasAssistant = typeof assistantMessageID === 'string' && assistantMessageID.length > 0;

  const providerRun = journalProviderRun ?? toolPartProviderRunID ?? providerRunFromMessageID;
  const xTraceRange = journalXTraceRange ?? toolPartXTrace;
  const carrierMappingComplete =
    unique
    && hasAssistant
    && hasOrdinals
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
    journalProviderRun,
    journalXTraceRange,
    providerRun,
    xTraceRange,
    directHostLacksProviderRunField,
    directHostLacksXTraceField,
    carrierMappingComplete,
    note:
      'Direct Host ToolPart has no providerRunID/XTrace. Without a journal callID→provider-run/XTrace '
      + 'mapping, Host Canary H blocks the membrane.',
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
    // and a manager-shaped tool surface (fork+join+list+suicide) — never invent matches.
    const tools = extractToolNamesFromRequest(request);
    if (managerSessions.size === 0) {
      const hasManagerSpine =
        tools.includes('fork')
        && tools.includes('join')
        && tools.includes('list')
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
 * Assert Phase 0 real-host canaries A / E / G / H from wrapper artifacts.
 * When Manager provider wire never advertises `todowrite`, return a precise
 * fail-closed canary result (not a hang / missing-artifact mystery).
 *
 * Throws AssertionError-style Error with a canary-prefixed message on failure
 * that is NOT the documented V2-unavailable Host path.
 *
 * @param {string} dir
 * @param {{ managerProviderWire?: ReturnType<typeof collectManagerProviderToolEvidence> | null }} [opts]
 */
export const assertMagicTodoHostCanariesAEGH = (dir, opts = {}) => {
  const managerProviderWire = opts.managerProviderWire ?? null;
  if (!managerProviderWire || managerProviderWire.requestCount === 0) {
    throw new Error('HOST_CANARY_MANAGER: missing Manager provider-wire evidence');
  }
  if (managerProviderWire.todowriteAdvertised) {
    throw new Error(
      `HOST_CANARY_MANAGER: todowrite unexpectedly advertised before the safe membrane: ${JSON.stringify(managerProviderWire)}`,
    );
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

  if (!before) throw new Error('HOST_CANARY_A/H: missing before.json (todowrite before never observed)');
  if (!after) throw new Error('HOST_CANARY_E/G/H: missing after.json (todowrite after never observed)');

  // ── A: durable V2 input vs executor V1 args ──────────────────────────────
  if (before.snapshotError) {
    throw new Error(`HOST_CANARY_A: before snapshot error: ${before.snapshotError}`);
  }
  if (before.argsIdentityUnchanged !== true) {
    throw new Error('HOST_CANARY_A: before must mutate the original args object in place');
  }
  if (!argsCarryV2Identity(before.preBeforeArgs)) {
    throw new Error('HOST_CANARY_A: pre-before args must carry provider V2 id/kind');
  }
  if (!argsAreV1Compatibility(before.postBeforeArgs)) {
    throw new Error('HOST_CANARY_A: post-before args must be V1 compatibility (id/kind stripped)');
  }
  if (!argsAreV1Compatibility(after.executorArgs)) {
    throw new Error('HOST_CANARY_A: executor args (after hookInput.args) must be V1 compatibility');
  }
  if (before.durableInput == null) {
    throw new Error('HOST_CANARY_A: durable ToolPart.input missing during before');
  }
  if (before.durableInputEqualsPreBefore !== true) {
    throw new Error(
      'HOST_CANARY_A: durable ToolPart.input must remain pre-before V2 bytes (alias freeze)',
    );
  }
  if (before.durableInputShowsMutation === true) {
    throw new Error(
      'HOST_CANARY_A: durable ToolPart.input must NOT show in-place before mutation',
    );
  }
  if (deepStable(before.durableInput).includes(MAGIC_TODO_HOST_CANARY_MUTATION)) {
    throw new Error('HOST_CANARY_A: mutation marker leaked into durable ToolPart.input');
  }
  if (!deepStable(before.postBeforeArgs).includes(MAGIC_TODO_HOST_CANARY_MUTATION)) {
    throw new Error('HOST_CANARY_A: in-place mutation marker missing from post-before args');
  }
  if (!deepStable(after.executorArgs).includes(MAGIC_TODO_HOST_CANARY_MUTATION)) {
    throw new Error('HOST_CANARY_A: in-place mutation did not reach executor args');
  }

  // ── E: after enrichment durable + model-visible ──────────────────────────
  if (after.enrichedOutput !== MAGIC_TODO_HOST_CANARY_ENRICHED) {
    throw new Error(
      `HOST_CANARY_E: after must set output to enriched bytes, got ${JSON.stringify(after.enrichedOutput)}`,
    );
  }
  // Settled snapshot is best-effort; when present it must show the same enriched bytes.
  if (settled?.locate?.match) {
    const durableOut = settled.locate.match.output;
    if (durableOut !== MAGIC_TODO_HOST_CANARY_ENRICHED) {
      throw new Error(
        `HOST_CANARY_E: durable ToolPart.output after settle must equal enriched bytes, got ${JSON.stringify(durableOut)}`,
      );
    }
  }
  // Model-visible path: during-after / settle locate is the Host history surface
  // the next provider turn reads. Freeze that the enriched marker is the after output.
  if (after.locateDuringAfter?.match?.status === 'completed') {
    const duringOut = after.locateDuringAfter.match.output;
    if (duringOut != null && duringOut !== MAGIC_TODO_HOST_CANARY_ENRICHED && duringOut !== after.originalOutput) {
      // completed-during-after may still hold pre-enrichment bytes (order freeze is G);
      // only fail if some third value appears.
      throw new Error(
        `HOST_CANARY_E: unexpected ToolPart.output during after: ${JSON.stringify(duringOut)}`,
      );
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
  const carrier = locateH.carrier ?? buildCarrierEvidence(locateH);
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
        preBeforeV2: argsCarryV2Identity(before.preBeforeArgs),
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
          // Advertise V2 surface (observation + future membrane shape). Host execute
          // decoder remains the original V1 parameters bound at tool init.
          if (hookOutput && typeof hookOutput === 'object') {
            hookOutput.description = MAGIC_TODO_HOST_CANARY_V2_ADVERTISEMENT.description;
            hookOutput.parameters = MAGIC_TODO_HOST_CANARY_V2_ADVERTISEMENT.parameters;
            hookOutput.jsonSchema = MAGIC_TODO_HOST_CANARY_V2_ADVERTISEMENT.jsonSchema;
          }
          writeJson('definition', {
            toolID: hookInput.toolID,
            description: hookOutput?.description ?? null,
            hasParameters: hookOutput?.parameters != null,
            hasJsonSchema: hookOutput?.jsonSchema != null,
            advertisesV2:
              hookOutput?.parameters?.properties?.todos?.items?.properties?.id != null
              && hookOutput?.parameters?.properties?.todos?.items?.properties?.kind != null,
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

        // Host honors only in-place field mutation on the original args object.
        // Compatibility projection: strip V2 id/kind → V1 rows, plus mutation marker.
        if (args && typeof args === 'object') {
          stripV2TodoIdentityFieldsInPlace(args);
          if (Array.isArray(args.todos) && args.todos[0] && typeof args.todos[0] === 'object') {
            const first = args.todos[0];
            first.content = `${first.content ?? ''}${MAGIC_TODO_HOST_CANARY_MUTATION}`;
          }
          args[MAGIC_TODO_HOST_CANARY_MUTATION] = true;
        }

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
          durableInputShowsMutation:
            typeof durableInput === 'object' && durableInput !== null
              ? deepStable(durableInput).includes(MAGIC_TODO_HOST_CANARY_MUTATION)
              : false,
          preBeforeCarriesV2: argsCarryV2Identity(preBeforeArgs),
          postBeforeIsV1: argsAreV1Compatibility(args),
          snapshotError,
          observedAt: Date.now(),
        };

        try {
          writeJson('before', evidence);
        } catch (error) {
          writeFailure('before-write', error, { sessionID, callID });
        }

        return productionBefore?.(hookInput, hookOutput);
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

        // Production after first (casebook observation); then freeze enrichment last.
        await productionAfter?.(hookInput, hookOutput);

        if (hookOutput && typeof hookOutput === 'object') {
          hookOutput.output = MAGIC_TODO_HOST_CANARY_ENRICHED;
          if (!hookOutput.title) hookOutput.title = 'magic-todo-host-canary';
        }

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
                enrichedOutput: MAGIC_TODO_HOST_CANARY_ENRICHED,
                durableOutput: locate?.match?.output ?? null,
                durableOutputEqualsEnriched: locate?.match?.output === MAGIC_TODO_HOST_CANARY_ENRICHED,
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
