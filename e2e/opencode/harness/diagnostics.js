/**
 * diagnostics.js — Rich failure diagnostics for E2E test failures.
 *
 * Dumps on failure: events, mock provider requests, session messages,
 * NDJSON, stderr, process tree, workspace files.
 *
 * Usage:
 *   import { dumpDiagnostics } from './diagnostics.js';
 *   try { ... } catch (err) { await dumpDiagnostics(suite, err); }
 */

import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';

/**
 * Gather all diagnostic data from a suite context.
 */
export async function gatherDiagnostics(suite) {
  const diag = {
    timestamp: new Date().toISOString(),
    baseUrl: suite.host?.baseUrl,
    scenarioDir: suite.scenarioDir,
    workDir: suite.host?.workDir,
    events: [],
    mockRequests: [],
    sessionMessages: {},
    ndjson: null,
    stderr: null,
    processTree: null,
    workspaceFiles: null,
    sessionStatuses: {},
  };

  // Events from probe
  if (suite.events) {
    diag.events = suite.events.allEvents.slice(-100).map(e => ({
      seq: e.seq,
      time: new Date(e.time).toISOString().slice(11, 23),
      type: e.type,
      sessionID: e.sessionID ? String(e.sessionID).slice(0, 16) : undefined,
      error: e.error,
      status: e.status,
    }));
  }

  // Mock provider requests
  if (suite.provider) {
    diag.mockRequests = suite.provider.requests.map(r => ({
      tools: (r.tools || []).map(t => t.function?.name || t.name),
      messageCount: r.messages?.length || 0,
      lastUserMsg: (() => {
        const msgs = r.messages || [];
        const last = [...msgs].reverse().find(m => m.role === 'user');
        if (!last) return null;
        const c = last.content;
        return typeof c === 'string' ? c.slice(0, 200) : Array.isArray(c) ? JSON.stringify(c[0]).slice(0, 200) : null;
      })(),
    }));
    diag.unexpectedRequests = suite.provider.unexpectedRequests.map(u => ({
      tools: extractToolNames(u.body),
      messageCount: u.body?.messages?.length || 0,
    }));
    diag.remainingExpectations = suite.provider.remainingExpectations;
  }

  // Session messages for each tracked session
  if (suite.client && suite.sessionIds) {
    for (const sid of suite.sessionIds) {
      try {
        const msgs = await suite.client.messages(sid);
        const data = msgs.data?.data || msgs.data;
        if (Array.isArray(data)) {
          diag.sessionMessages[sid] = data.map(msg => ({
            role: msg.info?.role || msg.role,
            parts: (msg.parts || []).map(p => ({
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

  // Session statuses
  if (suite.client) {
    try {
      const statusRes = await suite.client.request('GET', '/session/status');
      diag.sessionStatuses = statusRes.data?.data || statusRes.data || {};
    } catch {}
  }

  // NDJSON
  if (suite.host?.workDir) {
    const ndjsonPath = path.join(suite.host.workDir, '.wanxiangshu.ndjson');
    if (fs.existsSync(ndjsonPath)) {
      try {
        const raw = fs.readFileSync(ndjsonPath, 'utf8');
        const lines = raw.split('\n').filter(Boolean);
        diag.ndjson = {
          path: ndjsonPath,
          lineCount: lines.length,
          lastLines: lines.slice(-30).map((l, i) => {
            try { return JSON.parse(l); } catch { return { raw: l.slice(0, 200) }; }
          }),
        };
      } catch {}
    }
  }

  // Stderr
  if (suite.host) {
    diag.stderr = (suite.host.stderrLog || '').slice(-3000);
    diag.stdout = (suite.host.stdoutLog || '').slice(-2000);
  }

  // Process tree
  try {
    const pid = suite.host?.pid;
    if (pid) {
      if (process.platform === 'linux') {
        const ps = execSync(`ps --ppid ${pid} -o pid=,cmd= 2>/dev/null || true`, { timeout: 2000 }).toString().trim();
        diag.processTree = ps || '(no children)';
      } else {
        const ps = execSync(`ps -o pid=,ppid=,command= -p ${pid} 2>/dev/null || true`, { timeout: 2000 }).toString().trim();
        diag.processTree = ps || '(process info unavailable)';
      }
    }
  } catch {}

  // Workspace files
  if (suite.host?.workDir) {
    try {
      const files = execSync(`find ${suite.host.workDir} -maxdepth 3 -not -path '*/.git/*' -not -path '*/node_modules/*' -not -name '.git' 2>/dev/null || true`, { timeout: 2000 }).toString().trim();
      diag.workspaceFiles = files.split('\n').filter(Boolean);
    } catch {}
  }

  return diag;
}

function extractToolNames(body) {
  const tools = body?.tools;
  if (Array.isArray(tools)) {
    return tools.flatMap(t => {
      const name = t?.function?.name ?? t?.name;
      return typeof name === 'string' ? [name] : [];
    });
  }
  return [];
}

/**
 * Format diagnostics for console output.
 */
export function formatDiagnostics(diag) {
  const lines = [];

  lines.push('══════════════════════ E2E DIAGNOSTICS ══════════════════════');

  // Events
  if (diag.events.length > 0) {
    lines.push(`\n── Last ${diag.events.length} events ──`);
    for (const e of diag.events) {
      const parts = [`#${e.seq}`, e.time, e.type];
      if (e.sessionID) parts.push(`session=${e.sessionID}`);
      if (e.error) parts.push(`error=${typeof e.error === 'string' ? e.error.slice(0, 80) : JSON.stringify(e.error).slice(0, 80)}`);
      lines.push('  ' + parts.join(' '));
    }
  }

  // Mock provider
  if (diag.mockRequests.length > 0) {
    lines.push(`\n── ${diag.mockRequests.length} LLM request(s) ──`);
    for (let i = 0; i < diag.mockRequests.length; i++) {
      const r = diag.mockRequests[i];
      lines.push(`  Req ${i}: tools=[${(r.tools || []).slice(0, 8).join(',')}${(r.tools || []).length > 8 ? '...' : ''}] msgs=${r.messageCount}`);
      if (r.lastUserMsg) lines.push(`    last user: ${r.lastUserMsg.slice(0, 150)}`);
    }
  }
  if (diag.unexpectedRequests && diag.unexpectedRequests.length > 0) {
    lines.push(`\n── ${diag.unexpectedRequests.length} unexpected LLM request(s) ──`);
    for (const u of diag.unexpectedRequests) {
      lines.push(`  tools=[${(u.tools || []).slice(0, 8).join(',')}] msgs=${u.messageCount}`);
    }
  }
  if (diag.remainingExpectations !== undefined && diag.remainingExpectations > 0) {
    lines.push(`\n  ${diag.remainingExpectations} unmatched expectations remaining`);
  }

  // Session messages
  for (const [sid, msgs] of Object.entries(diag.sessionMessages)) {
    lines.push(`\n── Session ${sid.slice(0, 12)} messages ──`);
    for (const msg of msgs) {
      const role = msg.role || '?';
      for (const p of msg.parts) {
        const text = p.text ? JSON.stringify(p.text.slice(0, 150)) : '';
        const state = p.state ? ` state=${p.state.slice(0, 100)}` : '';
        const err = p.error ? ` error=${p.error.slice(0, 100)}` : '';
        if (text || state || err) lines.push(`  ${role} ${p.type}: ${text}${state}${err}`);
      }
    }
  }

  // Session statuses
  const statusKeys = Object.keys(diag.sessionStatuses);
  if (statusKeys.length > 0) {
    lines.push(`\n── ${statusKeys.length} session(s) in status map ──`);
    for (const [sid, s] of Object.entries(diag.sessionStatuses)) {
      lines.push(`  ${sid.slice(0, 12)}: ${s.type || s.status || '?'}`);
    }
  }

  // NDJSON
  if (diag.ndjson) {
    lines.push(`\n── NDJSON (${diag.ndjson.lineCount} lines) ──`);
    for (const line of diag.ndjson.lastLines) {
      if (line.event_kind) {
        lines.push(`  ${line.event_kind}: session=${String(line.session_id || '').slice(0, 12)} gen=${line.generation || '?'}`);
      } else if (line.raw) {
        lines.push(`  ${line.raw}`);
      }
    }
  }

  // Stderr
  if (diag.stderr && diag.stderr.trim()) {
    lines.push(`\n── opencode stderr (last 3000 chars) ──\n${diag.stderr}`);
  }

  // Process tree
  if (diag.processTree) {
    lines.push(`\n── Process tree ──\n${diag.processTree}`);
  }

  // Workspace files
  if (diag.workspaceFiles && diag.workspaceFiles.length > 0) {
    const shown = diag.workspaceFiles.slice(0, 30);
    lines.push(`\n── Workspace files (${shown.length}/${diag.workspaceFiles.length}) ──`);
    for (const f of shown) lines.push(`  ${f}`);
    if (shown.length < diag.workspaceFiles.length) lines.push(`  ... and ${diag.workspaceFiles.length - shown.length} more`);
  }

  return lines.join('\n');
}

/**
 * Dump diagnostics to console.error on test failure.
 */
export async function dumpDiagnostics(suite, err) {
  console.error(`\n${'═'.repeat(60)}`);
  console.error(`DIAGNOSTICS for: ${err.message}`);
  console.error(`${'═'.repeat(60)}`);

  try {
    const diag = await gatherDiagnostics(suite);
    console.error(formatDiagnostics(diag));
  } catch (diagErr) {
    console.error(`Diagnostics collection failed: ${diagErr.message}`);
  }

  console.error(`${'═'.repeat(60)}\n`);
}
