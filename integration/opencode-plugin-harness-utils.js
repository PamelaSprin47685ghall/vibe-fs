import fs from 'node:fs';
import path from 'node:path';
import {
  FallbackRuntimeStore__GetSession_Z721C83C5,
  FallbackRuntimeStore__OnStateChanged,
} from '../build/src/Runtime/Fallback/RuntimeStore.js';

export function readPartsText(output) {
  const parts = output?.parts;
  if (!Array.isArray(parts)) return '';
  return parts
    .filter((p) => p && p.type === 'text')
    .map((p) => p.text || '')
    .join('\n');
}

export function getLastLlmRequest(mockLLM) {
  if (!mockLLM.calls || mockLLM.calls.length === 0) return null;
  return mockLLM.calls[mockLLM.calls.length - 1];
}

export function getLlmCalls(mockLLM) {
  return mockLLM.calls || [];
}

export async function waitForCalls(mockLLM, count, timeoutMs = 1000) {
  const deadline = Date.now() + timeoutMs;
  while (mockLLM.calls.length < count) {
    if (Date.now() > deadline) {
      throw new Error(`timed out waiting for ${count} llm calls; saw ${mockLLM.calls.length}`);
    }
    await new Promise((r) => setTimeout(r, 20));
  }
  return mockLLM.calls.length;
}

export function expectTool(mockLLM, t, a) {
  mockLLM.expectTool(t, a);
}

export function expectText(mockLLM, t) {
  mockLLM.expectText(t);
}

export function resetMock(mockLLM) {
  mockLLM.reset();
}

export function readFile(workDir, relPath) {
  return fs.readFileSync(path.join(workDir, relPath), 'utf8');
}

export function fileExists(workDir, relPath) {
  return fs.existsSync(path.join(workDir, relPath));
}

export async function waitForFile(getSandboxDir, sessionId, relPath, timeoutMs = 1000) {
  const deadline = Date.now() + timeoutMs;
  const absPath = path.join(getSandboxDir(sessionId), relPath);
  while (Date.now() < deadline) {
    if (fs.existsSync(absPath)) return true;
    await new Promise((r) => setTimeout(r, 50));
  }
  return false;
}

function hasNoPendingContinuation(runtime, sessionId) {
  const session = FallbackRuntimeStore__GetSession_Z721C83C5(runtime, sessionId);
  const pending = session?.PendingLease;
  const isPending = pending && pending.Status && pending.Status.tag < 2;
  return !session || !isPending;
}

export function waitForPendingFallbackWork(runtime, sessionId) {
  if (!runtime || !sessionId) return Promise.resolve();
  if (hasNoPendingContinuation(runtime, sessionId)) return Promise.resolve();
  return new Promise((resolve) => {
    function onChange() {
      if (hasNoPendingContinuation(runtime, sessionId)) {
        resolve();
      } else {
        FallbackRuntimeStore__OnStateChanged(runtime, sessionId, onChange);
      }
    }
    FallbackRuntimeStore__OnStateChanged(runtime, sessionId, onChange);
  });
}

export async function disposeHarness(mockLLM, workDir, home) {
  await mockLLM.stop().catch(() => {});
  try {
    const lockPath = path.join(workDir, '.wanxiangshu.ndjson.lock');
    if (fs.existsSync(lockPath)) fs.rmSync(lockPath);
  } catch {}
  try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
}
