/**
 * manager-review-tools-canary-plugin.mjs — external observer canary plugin for Manager review read-only tools.
 *
 * Dynamically loads the production Wanxiangshu OpenCode plugin and overlays observer hooks on:
 *   - tool.definition
 *   - tool.execute.before
 *   - tool.execute.after
 *
 * Observes the sole manager review tool (js-manager)
 * and control tools (read, grep, glob, js-engineer, js-devops), recording:
 *   - schema decoration with contract (type, enum, required)
 *   - before: args reference identity (===), 'contract' in args, business args preservation, private Symbol presence
 *   - after: received args vs before args identity (===), contract restore, Symbol disappearance
 *   - 3 terminal states: normal, executor throw (missing file), cancellation (long task + abort)
 *   - durable ToolPart in Host message store preserving contract input
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const managerReviewToolsCanaryPluginPath = fileURLToPath(import.meta.url);

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '../../../..');
const defaultProductionPluginPath = path.join(repoRoot, 'dist', 'OpenCode', 'Plugin', 'Plugin.js');

const collectorUrl =
  process.env.WANXIANGSHU_REVIEW_TOOLS_CANARY_COLLECTOR
  || process.env.WANXIANGSHU_CHAT_CANARY_COLLECTOR
  || null;

const productionPluginPath =
  process.env.WANXIANGSHU_REVIEW_TOOLS_PRODUCTION_PLUGIN
  || process.env.WANXIANGSHU_E2E_MAGIC_TODO_HOST_CANARY_PLUGIN
  || (fs.existsSync(defaultProductionPluginPath) ? defaultProductionPluginPath : null);

const REVIEW_TOOLS = Object.freeze(['js-manager']);
const CONTROL_TOOLS = Object.freeze(['read', 'grep', 'glob', 'js-engineer', 'js-devops']);

const emit = async (kind, value) => {
  if (!collectorUrl) return;
  try {
    const res = await fetch(collectorUrl, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ kind, value }),
    });
    if (!res.ok) {
      // Best-effort reporting to collector
    }
  } catch (err) {
    // Non-fatal if collector is temporarily closing
  }
};

const getContractSymbols = (target) => {
  if (!target || (typeof target !== 'object' && typeof target !== 'function')) return [];
  return Object.getOwnPropertySymbols(target)
    .filter((s) => s.description === 'manager-review-contract' || String(s).includes('manager-review-contract'));
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
  if (typeof messagesFn !== 'function') return [];
  try {
    const response = await messagesFn.call(client.session, {
      path: { id: sessionID },
      query: { order: 'asc' },
    });
    return unwrapMessages(response);
  } catch {
    return [];
  }
};

const locateToolPart = (messages, sessionID, callID) => {
  for (const row of messages ?? []) {
    const info = row?.info ?? row ?? {};
    const parts = Array.isArray(row?.parts) ? row.parts : [];
    const messageSession = info.sessionID ?? parts[0]?.sessionID ?? null;
    if (sessionID && messageSession && messageSession !== sessionID) continue;

    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      if (part?.type !== 'tool') continue;
      if (part.callID !== callID) continue;
      return {
        found: true,
        callID: part.callID,
        tool: part.tool,
        status: part.state?.status ?? null,
        input: part.state?.input ?? null,
        output: part.state?.output ?? null,
        error: part.state?.error ?? null,
      };
    }
  }
  return { found: false, callID, status: null, input: null, output: null, error: null };
};

// Map of callID -> { preBeforeArgsRef, preBeforeArgsSnapshot, toolName, sessionID }
const inflightCalls = new Map();

let productionPlugin = null;
if (productionPluginPath && fs.existsSync(productionPluginPath)) {
  try {
    const imported = await import(pathToFileURL(productionPluginPath).href);
    productionPlugin = imported.default;
  } catch (err) {
    // If production plugin cannot be imported, productionPlugin remains null
  }
}

export default {
  id: 'wanxiangshu-manager-review-tools-canary',
  async server(input) {
    const client = input?.client ?? null;
    const hooks = productionPlugin?.server ? await productionPlugin.server(input) : {};

    const productionDefinition = hooks['tool.definition'];
    const productionBefore = hooks['tool.execute.before'];
    const productionAfter = hooks['tool.execute.after'];

    return {
      ...hooks,

      'tool.definition': async (hookInput, hookOutput) => {
        if (typeof productionDefinition === 'function') {
          await productionDefinition(hookInput, hookOutput);
        }

        const toolName = hookInput?.toolID ?? hookInput?.name ?? hookInput?.tool ?? '';
        const isReview = REVIEW_TOOLS.includes(toolName);
        const isControl = CONTROL_TOOLS.includes(toolName);

        if (isReview || isControl) {
          const parameters = hookOutput?.parameters;
          const properties = parameters?.properties ?? {};
          const required = Array.isArray(parameters?.required) ? parameters.required : [];
          const contractProp = properties.contract ?? null;

          const record = {
            toolName,
            isReviewTool: isReview,
            isControlTool: isControl,
            hasContractProperty: contractProp !== null,
            contractType: contractProp?.type ?? null,
            contractEnum: Array.isArray(contractProp?.enum) ? [...contractProp.enum] : null,
            contractDescription: contractProp?.description ?? null,
            requiredIncludesContract: required.includes('contract'),
            requiredList: [...required],
            propertiesKeys: Object.keys(properties).sort(),
          };

          await emit('tool.definition.observed', record);
        }
      },

      'tool.execute.before': async (hookInput, hookOutput) => {
        const toolName = hookInput?.tool ?? hookInput?.name ?? hookInput?.toolID ?? '';
        const sessionID = hookInput?.sessionID ?? null;
        const callID = hookInput?.callID ?? null;
        const isReview = REVIEW_TOOLS.includes(toolName);
        const isControl = CONTROL_TOOLS.includes(toolName);

        const argsPre = hookOutput?.args;
        const argsPreRef = argsPre;
        const preKeys = argsPre && typeof argsPre === 'object' ? Object.keys(argsPre).sort() : [];
        const preContractInArgs = argsPre && typeof argsPre === 'object' ? ('contract' in argsPre) : false;
        const preContractValue = argsPre?.contract ?? null;
        const preSymbols = getContractSymbols(argsPre);

        if (callID) {
          inflightCalls.set(callID, {
            toolName,
            sessionID,
            argsPreRef,
            preContractValue,
            preKeys,
            preArgsSnapshot: argsPre ? { ...argsPre } : null,
          });
        }

        if (typeof productionBefore === 'function') {
          await productionBefore(hookInput, hookOutput);
        }

        if (isReview || isControl) {
          const argsPost = hookOutput?.args;
          const argsIdentityPreserved = (argsPreRef === argsPost);
          const postKeys = argsPost && typeof argsPost === 'object' ? Object.keys(argsPost).sort() : [];
          const postContractInArgs = argsPost && typeof argsPost === 'object' ? ('contract' in argsPost) : false;
          const postSymbols = getContractSymbols(argsPost);
          const hasSymbolPost = postSymbols.length > 0;

          // Check non-contract business arguments preserved
          const businessKeysPreserved = preKeys
            .filter((k) => k !== 'contract')
            .every((k) => postKeys.includes(k) && argsPost?.[k] === argsPreRef?.[k]);

          // SDK durable snapshot probe
          let durableToolPart = null;
          if (client && sessionID && callID) {
            const messages = await fetchMessages(client, sessionID);
            durableToolPart = locateToolPart(messages, sessionID, callID);
          }

          const record = {
            stage: 'before',
            toolName,
            sessionID,
            callID,
            isReviewTool: isReview,
            isControlTool: isControl,
            argsIdentityPreserved,
            preContractInArgs,
            preContractValue,
            postContractInArgs,
            preHasSymbol: preSymbols.length > 0,
            postHasSymbol: hasSymbolPost,
            postSymbolCount: postSymbols.length,
            businessKeysPreserved,
            preKeys,
            postKeys,
            durableToolPartInputHasContract: durableToolPart?.input
              ? ('contract' in durableToolPart.input)
              : null,
            durableToolPartStatus: durableToolPart?.status ?? null,
          };

          await emit('tool.execute.before.observed', record);
        }
      },

      'tool.execute.after': async (hookInput, hookOutput) => {
        const toolName = hookInput?.tool ?? hookInput?.name ?? hookInput?.toolID ?? '';
        const sessionID = hookInput?.sessionID ?? null;
        const callID = hookInput?.callID ?? null;
        const isReview = REVIEW_TOOLS.includes(toolName);
        const isControl = CONTROL_TOOLS.includes(toolName);

        const stored = callID ? inflightCalls.get(callID) : null;
        const argsInAfter = hookInput?.args;
        const identityWithBefore = stored ? (stored.argsPreRef === argsInAfter) : null;

        const preAfterSymbols = getContractSymbols(argsInAfter);
        const preAfterContractInArgs = argsInAfter && typeof argsInAfter === 'object' ? ('contract' in argsInAfter) : false;

        if (typeof productionAfter === 'function') {
          await productionAfter(hookInput, hookOutput);
        }

        if (isReview || isControl) {
          const postAfterSymbols = getContractSymbols(argsInAfter);
          const postAfterContractInArgs = argsInAfter && typeof argsInAfter === 'object' ? ('contract' in argsInAfter) : false;
          const postAfterContractValue = argsInAfter?.contract ?? null;

          let durableToolPart = null;
          if (client && sessionID && callID) {
            const messages = await fetchMessages(client, sessionID);
            durableToolPart = locateToolPart(messages, sessionID, callID);
          }

          const record = {
            stage: 'after',
            toolName,
            sessionID,
            callID,
            isReviewTool: isReview,
            isControlTool: isControl,
            identityWithBefore,
            preAfterContractInArgs,
            preAfterHasSymbol: preAfterSymbols.length > 0,
            postAfterContractInArgs,
            postAfterContractValue,
            postAfterHasSymbol: postAfterSymbols.length > 0,
            postAfterSymbolCount: postAfterSymbols.length,
            contractRestored: isReview ? (postAfterContractInArgs && !postAfterSymbols.length) : true,
            output: typeof hookOutput?.output === 'string' ? hookOutput.output.slice(0, 500) : null,
            durableToolPartStatus: durableToolPart?.status ?? null,
            durableToolPartInputHasContract: durableToolPart?.input
              ? ('contract' in durableToolPart.input)
              : null,
          };

          await emit('tool.execute.after.observed', record);
        }
      },
    };
  },
};
