/**
 * magic-todo-host-canary-plugin.mjs — test-only OpenCode plugin wrapper.
 *
 * Mirrors receipt-acceptance-gate-plugin.mjs: one outer plugin path that loads
 * the production plugin, then overlays observation hooks. Used by Long Stroke
 * Phase 0 canaries A/E/G/H (real Host lifetime; no production membrane).
 *
 * Evidence files land in WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_DIR:
 *   definition.json  — tool.definition observation (todowrite)
 *   before.json      — pre/post before args + SDK localization
 *   after.json       — after enrichment + ToolPart status during after
 *   after-settled.json — post-return durable ToolPart (best-effort)
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

const directory = process.env.WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_DIR;
const pluginPath = process.env.WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_PLUGIN;

if (!directory || !pluginPath) {
  throw new Error('magic-todo host canary requires directory and production plugin path');
}

const writeJson = (name, value) => {
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

  return {
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
};

const deepStable = (value) => JSON.stringify(value ?? null);

const wrapped = await import(pathToFileURL(pluginPath).href);
const production = wrapped.default;

export default {
  id: 'wanxiangshu-e2e-magic-todo-host-canary',
  async server(input) {
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
          writeJson('definition', {
            toolID: hookInput.toolID,
            description: hookOutput?.description ?? null,
            hasParameters: hookOutput?.parameters != null,
            hasJsonSchema: hookOutput?.jsonSchema != null,
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
        if (args && typeof args === 'object') {
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
          snapshotError,
          observedAt: Date.now(),
        };

        try {
          writeJson('after', evidence);
        } catch (error) {
          writeFailure('after-write', error, { sessionID, callID });
        }

        // Best-effort settled snapshot (AI SDK may complete the part after after returns).
        queue Promise.resolve()
          .then(async () => {
            try {
              const snap = await fetchMessages(client, sessionID);
              const locate = locateToolPartByCall(snap, sessionID, callID);
              writeJson('after-settled', {
                sessionID,
                callID,
                locate,
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
