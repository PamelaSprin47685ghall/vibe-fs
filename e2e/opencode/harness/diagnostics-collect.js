/**
 * diagnostics-collect.js — Gather diagnostic data from a Scenario on
 * test failure. Kept in its own module so diagnostics.js stays under
 * the 200-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';

const TAIL_EVENT_LIMIT = 100;
const TAIL_NDJSON_LINES = 30;
const STDERR_TAIL = 3000;
const STDOUT_TAIL = 2000;
const PROC_TREE_TIMEOUT_MS = 2000;
const WORKSPACE_FIND_MAXDEPTH = 3;
const MAX_UNEXPECTED_PREVIEW = 8;
const MAX_WORKSPACE_FILES = 30;

export async function gatherDiagnostics(scenario) {
  const diag = {
    timestamp: new Date().toISOString(),
    baseUrl: scenario.host?.baseUrl,
    scenarioDir: scenario.scenarioDir,
    workDir: scenario.host?.workDir,
    events: [],
    mockRequests: [],
    sessionMessages: {},
    ndjson: null,
    stderr: null,
    processTree: null,
    workspaceFiles: null,
    sessionStatuses: {},
  };
  collectEvents(diag, scenario);
  collectMockRequests(diag, scenario);
  await collectSessionMessages(diag, scenario);
  collectSessionStatuses(diag, scenario);
  collectNdjson(diag, scenario);
  collectStderrStdout(diag, scenario);
  collectProcessTree(diag, scenario);
  collectWorkspaceFiles(diag, scenario);
  return diag;
}

function collectEvents(diag, scenario) {
  if (!scenario.events) return;
  diag.events = scenario.events.allEvents.slice(-TAIL_EVENT_LIMIT).map((e) => ({
    seq: e.seq,
    time: new Date(e.time).toISOString().slice(11, 23),
    type: e.type,
    sessionID: e.sessionID ? String(e.sessionID).slice(0, 16) : undefined,
    error: e.error,
    status: e.status,
  }));
}

function extractToolNamesFromTools(tools) {
  if (!Array.isArray(tools)) return [];
  return tools.flatMap((t) => {
    const name = t?.function?.name ?? t?.name;
    return typeof name === 'string' ? [name] : [];
  });
}

function lastUserPreview(req) {
  const msgs = req.messages || [];
  const last = [...msgs].reverse().find((m) => m.role === 'user');
  if (!last) return null;
  const c = last.content;
  return typeof c === 'string' ? c.slice(0, 200) : Array.isArray(c) ? JSON.stringify(c[0]).slice(0, 200) : null;
}

function collectMockRequests(diag, scenario) {
  if (!scenario.provider) return;
  diag.mockRequests = scenario.provider.requests.map((r) => ({
    tools: extractToolNamesFromTools(r.tools),
    messageCount: r.messages?.length || 0,
    lastUserMsg: lastUserPreview(r),
  }));
  diag.unexpectedRequests = scenario.provider.unexpectedRequests.map((u) => ({
    tools: extractToolNamesFromTools(u.body?.tools),
    messageCount: u.body?.messages?.length || 0,
  }));
  diag.remainingExpectations = scenario.provider.remainingExpectations;
}

async function collectSessionMessages(diag, scenario) {
  if (!scenario.client || !scenario.sessionIds) return;
  for (const sid of scenario.sessionIds) {
    try {
      const msgs = await scenario.client.messages(sid);
      const data = msgs.data?.data || msgs.data;
      if (Array.isArray(data)) {
        diag.sessionMessages[sid] = data.map((msg) => ({
          role: msg.info?.role || msg.role,
          parts: (msg.parts || []).map((p) => ({
            type: p.type,
            text: (p.text || '').slice(0, 300),
            state: p.state ? JSON.stringify(p.state).slice(0, 200) : undefined,
            error: p.error,
          })),
        }));
      }
    } catch {}
  }
}

async function collectSessionStatuses(diag, scenario) {
  if (!scenario.client) return;
  try {
    const statusRes = await scenario.client.request('GET', '/session/status');
    diag.sessionStatuses = statusRes.data?.data || statusRes.data || {};
  } catch {}
}

function collectNdjson(diag, scenario) {
  const workDir = scenario.host?.workDir;
  if (!workDir) return;
  const ndjsonPath = path.join(workDir, '.wanxiangshu.ndjson');
  if (!fs.existsSync(ndjsonPath)) return;
  try {
    const raw = fs.readFileSync(ndjsonPath, 'utf8');
    const lines = raw.split('\n').filter(Boolean);
    diag.ndjson = {
      path: ndjsonPath,
      lineCount: lines.length,
      lastLines: lines.slice(-TAIL_NDJSON_LINES).map((l) => {
        try { return JSON.parse(l); } catch { return { raw: l.slice(0, 200) }; }
      }),
    };
  } catch {}
}

function collectStderrStdout(diag, scenario) {
  if (!scenario.host) return;
  diag.stderr = (scenario.host.stderrLog || '').slice(-STDERR_TAIL);
  diag.stdout = (scenario.host.stdoutLog || '').slice(-STDOUT_TAIL);
}

function collectProcessTree(diag, scenario) {
  const pid = scenario.host?.pid;
  if (!pid) return;
  try {
    const cmd = process.platform === 'linux'
      ? `ps --ppid ${pid} -o pid=,cmd= 2>/dev/null || true`
      : process.platform === 'darwin'
        ? `pgrep -P ${pid} 2>/dev/null || true`
        : `ps -o pid=,ppid=,command= -p ${pid} 2>/dev/null || true`;
    diag.processTree = execSync(cmd, { timeout: PROC_TREE_TIMEOUT_MS }).toString().trim();
  } catch {}
}

function collectWorkspaceFiles(diag, scenario) {
  const workDir = scenario.host?.workDir;
  if (!workDir) return;
  try {
    const files = execSync(
      `find ${workDir} -maxdepth ${WORKSPACE_FIND_MAXDEPTH} -not -path '*/.git/*' -not -path '*/node_modules/*' -not -name '.git' 2>/dev/null || true`,
      { timeout: PROC_TREE_TIMEOUT_MS },
    ).toString().trim();
    diag.workspaceFiles = files.split('\n').filter(Boolean);
  } catch {}
}

export const DIAG_CONSTANTS = {
  TAIL_EVENT_LIMIT,
  TAIL_NDJSON_LINES,
  STDERR_TAIL,
  STDOUT_TAIL,
  PROC_TREE_TIMEOUT_MS,
  MAX_UNEXPECTED_PREVIEW,
  MAX_WORKSPACE_FILES,
};
