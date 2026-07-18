/**
 * diagnostics-format.js — Format a diagnostic record as console output.
 * Kept in its own module so diagnostics.js stays under the
 * 200-line Kolmogorov line budget and individual helpers stay
 * under the 50-line function budget.
 */

import { DIAG_CONSTANTS } from './diagnostics-collect.js';

const { MAX_UNEXPECTED_PREVIEW, MAX_WORKSPACE_FILES } = DIAG_CONSTANTS;

function formatToolsPreview(tools) {
  const list = tools || [];
  if (list.length <= MAX_UNEXPECTED_PREVIEW) return list.join(',');
  return list.slice(0, MAX_UNEXPECTED_PREVIEW).join(',') + '...';
}

function formatEventLine(e) {
  const parts = [`#${e.seq}`, e.time, e.type];
  if (e.sessionID) parts.push(`session=${e.sessionID}`);
  if (e.error) {
    const msg = typeof e.error === 'string' ? e.error.slice(0, 80) : JSON.stringify(e.error).slice(0, 80);
    parts.push(`error=${msg}`);
  }
  return '  ' + parts.join(' ');
}

function formatStatusLine(sid, s) {
  return `  ${sid.slice(0, 12)}: ${s.type || s.status || '?'}`;
}

function formatNdjsonLine(line) {
  if (line.event_kind) {
    return `  ${line.event_kind}: session=${String(line.session_id || '').slice(0, 12)} gen=${line.generation || '?'}`;
  }
  if (line.raw) return `  ${line.raw}`;
  return null;
}

function formatSessionMessages(sid, msgs) {
  const out = [`\n── Session ${sid.slice(0, 12)} messages ──`];
  for (const msg of msgs) {
    const role = msg.role || '?';
    for (const p of msg.parts) {
      const text = p.text ? JSON.stringify(p.text.slice(0, 150)) : '';
      const state = p.state ? ` state=${p.state.slice(0, 100)}` : '';
      const err = p.error ? ` error=${p.error.slice(0, 100)}` : '';
      if (text || state || err) out.push(`  ${role} ${p.type}: ${text}${state}${err}`);
    }
  }
  return out;
}

function formatMockRequests(diag) {
  const out = [];
  if (diag.mockRequests && diag.mockRequests.length > 0) {
    out.push(`\n── ${diag.mockRequests.length} LLM request(s) ──`);
    diag.mockRequests.forEach((r, i) => {
      out.push(`  Req ${i}: tools=[${formatToolsPreview(r.tools)}] msgs=${r.messageCount}`);
      if (r.lastUserMsg) out.push(`    last user: ${r.lastUserMsg.slice(0, 150)}`);
    });
  }
  if (diag.unexpectedRequests && diag.unexpectedRequests.length > 0) {
    out.push(`\n── ${diag.unexpectedRequests.length} unexpected LLM request(s) ──`);
    for (const u of diag.unexpectedRequests) {
      out.push(`  tools=[${formatToolsPreview(u.tools)}] msgs=${u.messageCount}`);
    }
  }
  if (diag.remainingExpectations !== undefined && diag.remainingExpectations > 0) {
    out.push(`\n  ${diag.remainingExpectations} unmatched expectations remaining`);
  }
  return out;
}

function formatStatuses(diag) {
  const out = [];
  const keys = Object.keys(diag.sessionStatuses || {});
  if (keys.length === 0) return out;
  out.push(`\n── ${keys.length} session(s) in status map ──`);
  for (const [sid, s] of Object.entries(diag.sessionStatuses)) {
    out.push(formatStatusLine(sid, s));
  }
  return out;
}

function formatNdjson(diag) {
  const out = [];
  if (!diag.ndjson) return out;
  out.push(`\n── NDJSON (${diag.ndjson.lineCount} lines) ──`);
  for (const line of diag.ndjson.lastLines) {
    const l = formatNdjsonLine(line);
    if (l) out.push(l);
  }
  return out;
}

function formatProcessAndFiles(diag) {
  const out = [];
  if (diag.stderr && diag.stderr.trim()) {
    out.push(`\n── opencode stderr (last 3000 chars) ──\n${diag.stderr}`);
  }
  if (diag.processTree) {
    out.push(`\n── Process tree ──\n${diag.processTree}`);
  }
  if (diag.workspaceFiles && diag.workspaceFiles.length > 0) {
    const shown = diag.workspaceFiles.slice(0, MAX_WORKSPACE_FILES);
    out.push(`\n── Workspace files (${shown.length}/${diag.workspaceFiles.length}) ──`);
    for (const f of shown) out.push(`  ${f}`);
    if (shown.length < diag.workspaceFiles.length) {
      out.push(`  ... and ${diag.workspaceFiles.length - shown.length} more`);
    }
  }
  return out;
}

function formatEvents(diag) {
  const out = [];
  if (diag.events.length > 0) {
    out.push(`\n── Last ${diag.events.length} events ──`);
    for (const e of diag.events) out.push(formatEventLine(e));
  }
  return out;
}

export function formatDiagnostics(diag) {
  return [
    '══════════════════════ E2E DIAGNOSTICS ══════════════════════',
    ...formatEvents(diag),
    ...formatMockRequests(diag),
    ...Object.entries(diag.sessionMessages || {}).flatMap(([sid, msgs]) => formatSessionMessages(sid, msgs)),
    ...formatStatuses(diag),
    ...formatNdjson(diag),
    ...formatProcessAndFiles(diag),
  ].join('\n');
}
