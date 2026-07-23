/**
 * scenario-http.js — Plain HTTP and FS helpers for E2E scenarios.
 * Extracted from scenario.js so the main file stays under the
 * 200-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import path from 'node:path';

export class FsOracle {
  constructor(workDir) { this._workDir = workDir; }
  expectFile(relPath) {
    if (!fs.existsSync(path.join(this._workDir, relPath))) {
      throw new Error(`FS: expected file missing: ${relPath}`);
    }
  }
  expectNoFile(relPath) {
    if (fs.existsSync(path.join(this._workDir, relPath))) {
      throw new Error(`FS: unexpected file exists: ${relPath}`);
    }
  }
  expectFileContent(relPath, expected) {
    const abs = path.join(this._workDir, relPath);
    if (!fs.existsSync(abs)) throw new Error(`FS: file not found: ${relPath}`);
    const actual = fs.readFileSync(abs);
    const expBuf = typeof expected === 'string' ? Buffer.from(expected, 'utf8') : expected;
    if (!actual.equals(expBuf)) {
      const a = actual.toString('utf8').slice(0, 200);
      const e = expBuf.toString('utf8').slice(0, 200);
      throw new Error(`FS: content mismatch: ${relPath}\n  exp: ${JSON.stringify(e)}\n  got: ${JSON.stringify(a)}`);
    }
  }
  expectFileContains(relPath, substr) {
    const content = fs.readFileSync(path.join(this._workDir, relPath), 'utf8');
    if (!content.includes(substr)) throw new Error(`FS: ${relPath} does not contain: ${substr}`);
  }
}

export class HttpClient {
  constructor(baseUrl, workDir) {
    this._baseUrl = baseUrl;
    this._workDir = workDir;
    this.onSessionCreated = null;
  }
  async request(method, urlPath, opts = {}) {
    const qs = opts.query ? '?' + new URLSearchParams(opts.query).toString() : '';
    const res = await fetch(this._baseUrl + urlPath + qs, {
      method,
      headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir, ...(opts.headers || {}) },
      body: opts.body ? JSON.stringify(opts.body) : undefined,
    });
    const text = await res.text();
    try { return { status: res.status, ok: res.ok, data: JSON.parse(text) }; }
    catch { return { status: res.status, ok: res.ok, data: text }; }
  }
  async requestWithSignal(method, urlPath, opts, signal) {
    const qs = opts.query ? '?' + new URLSearchParams(opts.query).toString() : '';
    try {
      const res = await fetch(this._baseUrl + urlPath + qs, {
        method, signal,
        headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir, ...(opts.headers || {}) },
        body: opts.body ? JSON.stringify(opts.body) : undefined,
      });
      const text = await res.text();
      try { return { status: res.status, ok: res.ok, data: JSON.parse(text) }; }
      catch { return { status: res.status, ok: res.ok, data: text }; }
    } catch (err) {
      return { status: 0, ok: false, data: err.message };
    }
  }
  async createSession(model = { id: 'test-model', providerID: 'test' }) {
    const body = typeof model === 'object' && model !== null && (model.model || model.providerID)
      ? { model }
      : { model: { id: 'test-model', providerID: 'test' } };
    const res = await this.request('POST', '/api/session', { body });
    if (res.ok && this.onSessionCreated) {
      const sid = res.data?.data?.data?.id || res.data?.data?.id;
      if (sid) this.onSessionCreated(sid);
    }
    return res;
  }
  async prompt(sessionID, text, model, timeoutMs = 120000) {
    const ac = new AbortController();
    const promptModel = model || { providerID: 'test', modelID: 'test-model' };
    const timer = setTimeout(() => ac.abort(), timeoutMs);
    try {
      const res = await fetch(`${this._baseUrl}/session/${sessionID}/prompt_async`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir },
        body: JSON.stringify({ parts: [{ type: 'text', text }], model: promptModel }),
        signal: ac.signal,
      });
      const bodyText = await res.text();
      return { status: res.status, ok: res.ok, data: bodyText };
    } catch (err) {
      return { status: 0, ok: false, data: err.message };
    } finally { clearTimeout(timer); }
  }
  async messages(sessionID) { return this.request('GET', `/session/${sessionID}/message`); }
  async sessionStatus(sessionID) { return this.request('GET', `/session/${sessionID}`); }
  async runCommand(sessionID, command, args = '', timeoutMs = 30000) {
    const ac = new AbortController();
    const timer = setTimeout(() => ac.abort(), timeoutMs);
    try {
      const res = await this.requestWithSignal('POST', `/session/${sessionID}/command`, { body: { command, arguments: args } }, ac.signal);
      return res;
    } finally { clearTimeout(timer); }
  }
  async abort(sessionID) { return this.request('POST', `/session/${sessionID}/abort`, { body: {} }); }

  async waitForSessionIdle(sessionID, timeoutMs = 30000) {
    const deadline = Date.now() + timeoutMs;
    let sawNonIdle = false;
    while (Date.now() < deadline) {
      const res = await this.request('GET', '/session/status');
      if (res.ok && res.data) {
        const statusMap = res.data.data || res.data || {};
        const status = statusMap[sessionID];
        if (status) {
          if (status.type === 'idle' || status.status === 'idle') {
            if (sawNonIdle) return true;
          } else {
            sawNonIdle = true;
          }
        }
      }
      await new Promise((r) => setTimeout(r, 100));
    }
    throw new Error(`waitForSessionIdle timed out or failed to transition to idle for session ${sessionID}`);
  }
}

export function getSessionId(sess) {
  return sess?.data?.data?.data?.id || sess?.data?.data?.id;
}
