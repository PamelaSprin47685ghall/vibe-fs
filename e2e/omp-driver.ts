/**
 * e2e/omp-driver.ts — real oh-my-pi extension loader IPC driver.
 * Loads wanxiangshuExtension via REAL loader, exposes Extension over stdin/stdout JSON IPC.
 * Commands: emit|callTool|readNdjson|readFile|fileExists|waitForNdjson|getToolNames|getCommands|getHandlers|dispose
 */

import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { respond, readStdinJson } from './omp-driver/ipc';
import { createStubExtensionContext, patchRuntime } from './omp-driver/stub';

const originalFetch = global.fetch;
global.fetch = async (url, options) => {
  if (typeof url === 'string' && url.startsWith('https://ollama.com/api')) {
    const json = () => url.includes('web_search') ? ({ results: [{ title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' }] }) : ({ title: 'Example Domain', byline: 'IANA', length: 500, content: 'Example Domain\n\nThis domain is for use in documentation examples.' });
    return { ok: true, status: 200, json: async () => json() };
  }
  return typeof originalFetch === 'function' ? originalFetch(url, options) : Promise.reject(new Error(`fetch not stubbed: ${url}`));
};

let globalBindings: any = null;

let mockReportMarkdown = 'Accepted: Pre-review passed.';

async function callMockLLM(prompt: string): Promise<string> {
  const llmUrl = process.env.MOCK_LLM_URL;
  if (!llmUrl) return mockReportMarkdown;
  try {
    const fetchUrl = `${llmUrl}/v1/chat/completions`;
    process.stderr.write(`[omp-driver-debug] fetching: ${fetchUrl}\n`);
    const res = await (globalThis.fetch || fetch)(fetchUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        messages: [{ role: 'user', content: prompt }],
        model: 'gpt-4o'
      })
    });
    process.stderr.write(`[omp-driver-debug] fetch res status: ${res.status}\n`);
    if (!res.ok) return mockReportMarkdown;
    const text = await res.text();
    process.stderr.write(`[omp-driver-debug] fetch text len: ${text.length}\n`);
    let content = '';
    for (const line of text.split('\n')) {
      if (line.startsWith('data: ')) {
        const dataStr = line.slice(6).trim();
        if (dataStr === '[DONE]') continue;
        try {
          const parsed = JSON.parse(dataStr);
          const delta = parsed.choices?.[0]?.delta;
          if (delta && typeof delta.content === 'string') {
            content += delta.content;
          }
        } catch {}
      }
    }
    return content || mockReportMarkdown;
  } catch (err) {
    process.stderr.write(`[omp-driver-debug] callMockLLM err: ${err}\n`);
    return mockReportMarkdown;
  }
}

async function callCurried(fn: any, args: any[]) {
	if (typeof fn !== 'function') return fn;
	if (fn.length > 1) {
		return fn(...args);
	}
	let current = fn;
	for (const arg of args) {
		if (typeof current !== 'function') break;
		current = await current(arg);
	}
	return current;
}

function ensureSandbox(workdir: string, sessionId: string) {
	const finalWorkdir = path.join(workdir, 'sandboxes', sessionId);
	if (!fs.existsSync(finalWorkdir)) {
		fs.mkdirSync(finalWorkdir, { recursive: true });
		try { execSync('git init -q && git config user.email test@test && git config user.name test', { cwd: finalWorkdir, stdio: 'ignore' }); }
		catch { fs.mkdirSync(path.join(finalWorkdir, '.git'), { recursive: true }); }
	}
	return finalWorkdir;
}

function createOmpContext(workdir: string, sessionId: string) {
	const finalWorkdir = ensureSandbox(workdir, sessionId);
	return Object.assign(createStubExtensionContext(finalWorkdir, sessionId), {
		workspaceRoot: finalWorkdir,
		root: finalWorkdir,
	});
}

async function handleOmpFileOps(cmd: any, workdir: string) {
	const sessionId = cmd.sessionId || 'omp-e2e-session';
	const finalWorkdir = ensureSandbox(workdir, sessionId);
	const ndjsonPath = path.join(finalWorkdir, '.wanxiangshu.ndjson');

	if (cmd.type === 'readNdjson') {
		let c = ''; try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {} respond(true, { content: c });
	} else if (cmd.type === 'readFile') {
		let c = ''; try { c = fs.readFileSync(path.join(finalWorkdir, cmd.path), 'utf8'); }
		catch (e) { respond(false, null, `Read failed: ${e instanceof Error ? e.message : String(e)}`); return; }
		respond(true, { content: c });
	} else if (cmd.type === 'fileExists') {
		respond(true, { exists: fs.existsSync(path.join(finalWorkdir, cmd.path)) });
	} else if (cmd.type === 'waitForNdjson') {
		const min = cmd.min ?? 1, deadline = Date.now() + (cmd.maxMs ?? 1000);
		let count = 0;
		while (Date.now() < deadline) {
			let c = ''; try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {}
			count = c.split('\n').filter(Boolean).length; if (count >= min) break;
			await new Promise((r) => setTimeout(r, 50));
		}
		respond(true, { ready: count >= min, count });
	}
}

async function executeOmpTool(ex: any, workdir: string, cmd: any) {
	process.stderr.write(`[omp-driver-debug] executeOmpTool start: ${cmd.name} sessionId=${cmd.sessionId}\n`);
	const entry = ex.tools.get(cmd.name);
	if (!entry) { respond(false, null, `Unknown tool: ${cmd.name}`); return; }
	const def = (entry as any).definition ?? entry;
	const onUpdate = (partial: any) => process.stderr.write(`[partial] ${JSON.stringify(partial)}\n`);
	const toolCallId = cmd.toolCallId ?? 'call-' + Date.now();
	const sessionId = cmd.sessionId || 'omp-e2e-session';
	const finalWorkdir = ensureSandbox(workdir, sessionId);
	const toolCtx = createOmpContext(workdir, sessionId);
	let result: unknown, threw = true;
	try {
		const callArgs = [toolCallId, cmd.params, undefined, onUpdate, toolCtx];
		result = await callCurried(def.execute, callArgs);
		threw = false;
	}
	catch (e) {
		process.stderr.write(`[omp-driver-debug] executeOmpTool exception in ${cmd.name}: ${e instanceof Error ? e.stack || e.message : String(e)}\n`);
		result = { thrown: e instanceof Error ? e.message : String(e) };
	}
	process.stderr.write(`[callTool] ${cmd.name} ok=${!threw} result=${JSON.stringify(result)}\n`);
	respond(true, result);
}

async function runOmpCommand(ex: any, workdir: string, cmd: any) {
	const entry = ex.commands.get(cmd.name);
	if (!entry) { respond(false, null, `Unknown command: ${cmd.name}`); return; }
	const sessionId = cmd.sessionId || 'omp-e2e-session';
	const toolCtx = createOmpContext(workdir, sessionId);
	let result: unknown, threw = true;
	try {
		let f = entry.handler || entry.execute;
		process.stderr.write(`[omp-driver] runCommand handler type: ${typeof f}\n`);
		if (typeof f === 'function') {
			let r = f;
			if (typeof r === 'function') r = await r(cmd.args ?? '');
			if (typeof r === 'function') r = await r(toolCtx);
			result = r;
		} else {
			result = "handler is not a function";
		}
		threw = false;
	}
	catch (e) { result = { thrown: e instanceof Error ? e.message : String(e) }; }
	respond(true, { success: !threw, result });
}

async function emitOmpEvent(ex: any, workdir: string, cmd: any) {
	const handlers = ex.handlers.get(cmd.eventType) ?? [];
	const event = { ...(cmd.event ?? {}), type: cmd.eventType, sessionId: cmd.sessionId };
	const sessionId = cmd.sessionId || 'omp-e2e-session';
	const toolCtx = createOmpContext(workdir, sessionId);
	const results: Array<{ ok: boolean; error?: string }> = [];
	for (const handler of handlers) {
		try {
			const r = handler(event, toolCtx);
			if (r && typeof (r as any).then === 'function') await (r as Promise<unknown>);
			results.push({ ok: true });
		}
		catch (e) {
			results.push({ ok: false, error: e instanceof Error ? e.message : String(e) });
		}
	}
	respond(true, { results });
}

async function handleCommand(ex: any, workdir: string, cmd: Record<string, any>): Promise<boolean | void> {
	if (cmd.type === 'emit') {
		await emitOmpEvent(ex, workdir, cmd);
	} else if (cmd.type === 'callTool') {
		await executeOmpTool(ex, workdir, cmd);
	} else if (['readNdjson', 'readFile', 'fileExists', 'waitForNdjson'].includes(cmd.type)) {
		await handleOmpFileOps(cmd, workdir);
	} else if (cmd.type === 'getToolNames') {
		respond(true, { toolNames: Array.from(ex.tools.keys()) });
	} else if (cmd.type === 'getCommands') {
		respond(true, { commandNames: Array.from(ex.commands.keys()) });
	} else if (cmd.type === 'runCommand') {
		await runOmpCommand(ex, workdir, cmd);
	} else if (cmd.type === 'getHandlers') {
		respond(true, { handlerKeys: Array.from(ex.handlers.keys()) });
	} else if (cmd.type === 'cleanSandbox') {
		const sessionId = cmd.sessionId;
		if (sessionId) {
			const finalWorkdir = path.join(workdir, 'sandboxes', sessionId);
			try { fs.rmSync(finalWorkdir, { recursive: true, force: true }); } catch {}
		}
		respond(true, { cleaned: true });
	} else if (cmd.type === 'dispose') {
		try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {}
		respond(true, { disposed: true });
		return true;
	} else {
		respond(false, null, `Unknown command: ${cmd.type}`);
	}
}

class MockExtension {
	tools = new Map<string, any>();
	commands = new Map<string, any>();
	handlers = new Map<string, any[]>();
}

function createMockPiContext(workdir: string, extension: MockExtension) {
	let childIdSeq = 0;
	const typebox = {
		Type: {
			String: (opts?: any) => ({ type: 'string', ...(opts || {}) }),
			Number: (opts?: any) => ({ type: 'number', ...(opts || {}) }),
			Boolean: (opts?: any) => ({ type: 'boolean', ...(opts || {}) }),
			Array: (schema: any, opts?: any) => ({ type: 'array', items: schema, ...(opts || {}) }),
			Object: (props: any, opts?: any) => ({ type: 'object', properties: props, ...(opts || {}) }),
			Optional: (schema: any) => ({ ...schema, optional: true }),
			Union: (schemas: any[]) => ({ anyOf: schemas }),
			Enum: (values: any[], opts?: any) => ({ type: 'string', enum: values, ...(opts || {}) }),
			Null: (opts?: any) => ({ type: 'null', ...(opts || {}) }),
		}
	};
	const piObj: any = {
		directory: workdir,
		cwd: workdir,
		typebox,
		createAgentSession: async (body: any) => {
			const childId = `omp-child-${++childIdSeq}`;
			const messages: any[] = [];
			const sm = {
				getSessionId: () => childId,
				getEntries: () => messages,
				messages: messages,
			};
			const sessionWorkdir = (typeof body?.cwd === 'string' && body.cwd) ? body.cwd : path.join(workdir, 'sandboxes', 'e2e-omp-session-1');
			const parentSessionId = path.basename(sessionWorkdir);
			const promptFn = async (promptText: any) => {
				const text = typeof promptText === 'string' ? promptText : (promptText?.text || promptText?.prompt || JSON.stringify(promptText));
				process.stderr.write(`[omp-driver-debug] promptFn text: ${text.slice(0, 100)}\n`);
				let report = mockReportMarkdown;
				try {
					report = await callMockLLM(text);
				} catch (err) {
					process.stderr.write(`[omp-driver-debug] callMockLLM error: ${err}\n`);
				}
				messages.push({ role: 'user', type: 'user', message: { role: 'user', content: text } });
				messages.push({
					role: 'assistant',
					type: 'assistant',
					message: {
						role: 'assistant',
						content: [{ type: 'text', text: report }]
					},
					text: report
				});
				const msgId = `msg-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
				const msgUpdateObj = {
					type: 'message.updated',
					workspaceId: childId,
					sessionID: childId,
					sessionId: childId,
					event: {
						type: 'message.updated',
						info: {
							role: 'assistant',
							sessionID: childId,
							sessionId: childId
						}
					},
					props: {
						sessionID: childId,
						sessionId: childId,
						workspaceId: childId,
						parts: [{ type: 'text', text: report }]
					}
				};
				const idleObj = {
					type: 'session.idle',
					workspaceId: childId,
					sessionID: childId,
					sessionId: childId,
					event: {
						type: 'session.idle',
						sessionID: childId,
						sessionId: childId
					},
					props: {
						sessionID: childId,
						sessionId: childId,
						workspaceId: childId,
						parts: [{ type: 'text', text: report }],
						metadata: { finishReason: 'stop', muxStopReason: 'stop' }
					}
				};
				setTimeout(async () => {
					try {
						const { tryIdle } = await import('../build/src/Runtime/Subsession/SubsessionEventRouter.js');
						await tryIdle(sessionWorkdir, childId);
						await tryIdle(workdir, childId);
					} catch {}
					if (globalBindings?.registration?.eventHook) {
						try {
							const ctxObj = { cwd: sessionWorkdir, directory: sessionWorkdir, workspacePath: sessionWorkdir, sessionID: childId, sessionId: childId, workspaceId: childId };
							await globalBindings.registration.eventHook(msgUpdateObj, ctxObj);
							await globalBindings.registration.eventHook(idleObj, ctxObj);
						} catch (e: any) {
							process.stderr.write(`[omp-driver-debug] eventHook error: ${e?.stack || e?.message || e}\n`);
						}
					}
				}, 10);
				return {
					id: msgId,
					messageId: msgId,
					text: report,
					content: report,
					output: report,
					data: { id: msgId },
					toString: () => report
				};
			};
			const sessionObj = {
				sessionManager: sm,
				isIdle: true,
				status: 'idle',
				prompt: promptFn,
			};
			return {
				session: sessionObj,
				prompt: promptFn,
				dispose: () => {},
			};
		},
		session: {
			on: (evt: string, fn: any) => {
				const list = extension.handlers.get(evt) || [];
				list.push(fn);
				extension.handlers.set(evt, list);
			}
		},
		events: {
			on: (evt: string, fn: any) => {
				const list = extension.handlers.get(evt) || [];
				list.push(fn);
				extension.handlers.set(evt, list);
			},
			emit: () => {}
		},
		registerTool: (toolDef: any) => {
			const name = toolDef.name || toolDef.id;
			if (name) extension.tools.set(name, toolDef);
		},
		registerCommand: (name: string, cmdDef: any) => {
			extension.commands.set(name, cmdDef);
		},
		on: (evt: string, fn: any) => {
			const list = extension.handlers.get(evt) || [];
			list.push(fn);
			extension.handlers.set(evt, list);
		}
	};
	piObj.pi = piObj;
	return piObj;
}

async function main() {
	const pluginPath = process.env.WANXIANGSHU_PLUGIN_PATH;
	if (!pluginPath) { process.stderr.write('WANXIANGSHU_PLUGIN_PATH env var required\n'); process.exit(1); }
	let workdir = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-driver-'));
	try { execSync('git init -q && git config user.email test@test && git config user.name test', { cwd: workdir, stdio: 'ignore' }); }
	catch { fs.mkdirSync(path.join(workdir, '.git'), { recursive: true }); }

	const mod = await import(pluginPath);
	const factory = mod.default ?? mod.wanxiangshuExtension ?? mod.plugin;
	if (typeof factory !== 'function') { respond(false, null, `Plugin does not export a factory function: got ${typeof factory}`); process.exit(1); }

	const extension = new MockExtension();
	globalBindings = {
		registration: {
			eventHook: async (evt: any, ctx: any) => {
				const handlers = [...(extension.handlers.get('event') || []), ...(extension.handlers.get('stream-end') || []), ...(extension.handlers.get('agent_end') || [])];
				for (const fn of handlers) {
					try {
						const r = fn(evt, ctx);
						if (r && typeof r.then === 'function') await r;
					} catch {}
				}
			}
		}
	};
	const pi = createMockPiContext(workdir, extension);
	try {
		const res = factory(pi);
		if (res && typeof res.then === 'function') await res;
	} catch (err) {
		respond(false, null, `factory execution failed: ${err instanceof Error ? err.message : String(err)}`);
		process.exit(1);
	}

	process.stderr.write(`[omp-driver] base workdir=${workdir}\n`);
	process.stdout.write('ready\n');
	for (;;) {
		let cmd: Record<string, any> | null;
		try { cmd = await readStdinJson(); } catch { respond(false, null, 'Failed to parse stdin JSON'); continue; }
		if (!cmd) break;
		try {
			const overrideDir = (typeof cmd.workdir === 'string' && cmd.workdir) ? cmd.workdir : null;
			if (overrideDir) workdir = overrideDir;
			const done = await handleCommand(extension, workdir, cmd);
			if (done) { try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {} process.exit(0); }
		} catch (err) { respond(false, null, `Command ${cmd.type} failed: ${err instanceof Error ? err.message : String(err)}`); }
	}
	try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {}
	process.exit(0);
}

main().catch((err) => { process.stderr.write(`driver fatal: ${err instanceof Error ? err.message : String(err)}\n`); process.exit(1); });