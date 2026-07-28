/**
 * Data-driven mock scripts — Script Forest (AGENTS.md KISS-N11).
 *
 * Runtime match key = provider-visible full request seal (idempotent).
 * Authoring sugar = content match (user / tools / role) on path steps.
 * No mute, no numbering, no session-bind identity.
 *
 * Schema:
 * {
 *   "scenario": "name",
 *   "setup?": { "project?", "env?", "strict?", "watchdogLabel?" },
 *   "session?": { "agent", "bind?": ["alias", ...] },
 *   "prompt?": { "agent", "text" } | "prompts?": [...],
 *   "flow?": [ { "wait": "id" } | { "restart": true } | { "prompt": {...} } | { "loadScripts": "file" } | ... ],
 *   "pass?": "message",
 *   "scripts": [ { id, lane, match, respond, neverEnd?, blocking? } ]
 * }
 *
 * lane.turn is authoring metadata only (not a runtime match key).
 * Parallel jobs must differ by user (or other visible) content.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPTS_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), 'scripts');

export function scriptsDir() {
  return SCRIPTS_DIR;
}

export function resolveScriptPath(filePath) {
  if (path.isAbsolute(filePath)) return filePath;
  return path.join(SCRIPTS_DIR, filePath.endsWith('.json') ? filePath : `${filePath}.json`);
}

export function readScript(filePath) {
  const abs = resolveScriptPath(filePath);
  const data = JSON.parse(fs.readFileSync(abs, 'utf8'));
  if (!data.scenario) throw new Error(`${abs}: missing scenario`);
  if (!Array.isArray(data.scripts)) throw new Error(`${abs}: missing scripts[]`);
  if (data.phases) {
    throw new Error(`${abs}: phases are forbidden — use one linear scripts[] keyed by first-message match`);
  }
  return { path: abs, ...data };
}

function normalizeLane(scenario, lane) {
  if (!lane || typeof lane !== 'object') throw new Error('script lane is required');
  return {
    scenario,
    session: lane.session,
    role: lane.role,
    turn: lane.turn,
    requestKind: lane.requestKind || 'chat',
    parentSession: lane.parentSession,
  };
}

function registerStep(provider, scenario, abs, step) {
  if (!step.id) throw new Error(`${abs}: script step missing id`);
  if (!step.respond?.type) throw new Error(`${abs}: script ${step.id} missing respond.type`);
  const lane = normalizeLane(scenario, step.lane);
  const match = { ...(step.match || {}) };
  match.requestKind = lane.requestKind;

  const opts = {
    id: step.id,
    lane,
    match,
    blocking: step.blocking !== false && step.blocking !== undefined
      ? step.blocking
      : step.neverEnd
        ? false
        : true,
    neverEnd: Boolean(step.neverEnd),
  };
  for (const flag of ['neverEnd', 'reusable', 'pathless', 'delayFirstToken', 'delayDone', 'disconnectMidSse']) {
    if (step[flag] !== undefined) opts[flag] = step[flag];
    if (step.respond?.[flag] !== undefined) opts[flag] = step.respond[flag];
  }

  const r = step.respond;
  switch (r.type) {
    case 'text':
      provider.expectText({ ...opts, text: r.text ?? 'ok' });
      break;
    case 'title':
      provider.expectTitle({ ...opts, text: r.text ?? 'E2E Test Session' });
      break;
    case 'tool-call': {
      let args = r.args || {};
      if (r.argsFrom === 'lastPtyId' || step.argsFrom === 'lastPtyId') {
        const base = { ...(r.args || {}) };
        args = (parsed) => {
          const messages = parsed?.messages || [];
          let ptyId = 'pty-unknown';
          for (let i = messages.length - 1; i >= 0; i -= 1) {
            const msg = messages[i];
            const content = typeof msg?.content === 'string' ? msg.content : JSON.stringify(msg?.content || '');
            const match = content.match(/"ptyId"\s*:\s*"([^"]+)"/) || content.match(/ptyId[=:]\s*([A-Za-z0-9_-]+)/);
            if (match) { ptyId = match[1]; break; }
          }
          return { agent: ptyId, ...base };
        };
      }
      provider.expectToolCall({ ...opts, tool: r.tool, args });
      break;
    }
    case 'error':
      provider.expectError({
        ...opts,
        status: r.status || 500,
        headers: r.headers,
        body: r.body || { error: 'mock error' },
      });
      break;
    case 'disconnect':
      provider.expectDisconnect(opts);
      break;
    default:
      throw new Error(`${abs}: script ${step.id} unknown respond.type ${r.type}`);
  }
}

/** Register all linear scripts onto the provider. */
export function loadScripts(provider, filePath) {
  const data = readScript(filePath);
  for (const step of data.scripts) {
    registerStep(provider, data.scenario, data.path, step);
  }
  return data;
}
