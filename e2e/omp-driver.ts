/**
 * e2e/omp-driver.ts — real oh-my-pi extension loader IPC driver.
 * Loads wanxiangshuExtension via REAL loader, exposes Extension over stdin/stdout JSON IPC.
 * Commands: emit|callTool|readNdjson|readFile|fileExists|waitForNdjson|getToolNames|getCommands|getHandlers|dispose
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { respond, readStdinJson } from './omp-driver/ipc';
import { createStubExtensionContext, patchRuntime } from './omp-driver/stub';

async function handleCommand(ex: any, workdir: string, ndjsonPath: string, cmd: Record<string, any>): Promise<boolean | void> {
	switch (cmd.type) {
		case 'emit': {
			const handlers = ex.handlers.get(cmd.eventType) ?? [];
			const event = { ...(cmd.event ?? {}), type: cmd.eventType, sessionId: cmd.sessionId };
			const results: Array<{ ok: boolean; error?: string }> = [];
			for (const handler of handlers) {
				try { const r = handler(event, createStubExtensionContext(workdir, cmd.sessionId ?? '')); if (r && typeof (r as any).then === 'function') await (r as Promise<unknown>); results.push({ ok: true }); }
				catch (e) { results.push({ ok: false, error: e instanceof Error ? e.message : String(e) }); }
			}
			respond(true, { results }); break;
		}
		case 'callTool': {
			const entry = ex.tools.get(cmd.name);
			if (!entry) { respond(false, null, `Unknown tool: ${cmd.name}`); break; }
			const def = (entry as any).definition ?? entry;
			const onUpdate = (partial: any) => process.stderr.write(`[partial] ${JSON.stringify(partial)}\n`);
			const toolCallId = cmd.toolCallId ?? 'call-' + Date.now();
			const toolCtx = createStubExtensionContext(workdir, cmd.sessionId ?? '');
			let result: unknown, threw = true;
			try { let f: any = def.execute; f = await f(toolCallId); f = await f(cmd.params); f = await f(undefined); f = await f(onUpdate); f = await f(toolCtx); result = f; threw = false; }
			catch (e) { result = { thrown: e instanceof Error ? e.message : String(e) }; }
			process.stderr.write(`[callTool] ${cmd.name} ok=${!threw} result=${JSON.stringify(result)}\n`);
			respond(true, result); break;
		}
		case 'readNdjson': { let c = ''; try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {} respond(true, { content: c }); break; }
		case 'readFile': { let c = ''; try { c = fs.readFileSync(path.join(workdir, cmd.path), 'utf8'); } catch (e) { respond(false, null, `Read failed: ${e instanceof Error ? e.message : String(e)}`); break; } respond(true, { content: c }); break; }
		case 'fileExists': { respond(true, { exists: fs.existsSync(path.join(workdir, cmd.path)) }); break; }
		case 'waitForNdjson': {
			const min = cmd.min ?? 1, deadline = Date.now() + (cmd.maxMs ?? 30000);
			let count = 0;
			while (Date.now() < deadline) { let c = ''; try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {} count = c.split('\n').filter(Boolean).length; if (count >= min) break; await new Promise((r) => setTimeout(r, 50)); }
			respond(true, { ready: count >= min, count }); break;
		}
		case 'getToolNames': { respond(true, { toolNames: Array.from(ex.tools.keys()) }); break; }
		case 'getCommands': { respond(true, { commandNames: Array.from(ex.commands.keys()) }); break; }
		case 'getHandlers': { respond(true, { handlerKeys: Array.from(ex.handlers.keys()) }); break; }
		case 'dispose': { try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {} respond(true, { disposed: true }); return true; }
		default: respond(false, null, `Unknown command: ${cmd.type}`);
	}
}

async function main() {
	const ompRepo = process.env.WANXIANGSHU_OMP_REPO ?? process.cwd();
	process.env.OPENAI_API_KEY = 'test-key';
	const { loadExtensionFromFactory, ExtensionRuntime } = await import(`${ompRepo}/packages/coding-agent/src/extensibility/extensions/loader`);
	const { EventBus } = await import(`${ompRepo}/packages/coding-agent/src/utils/event-bus`);
	const pluginPath = process.env.WANXIANGSHU_PLUGIN_PATH;
	if (!pluginPath) { process.stderr.write('WANXIANGSHU_PLUGIN_PATH env var required\n'); process.exit(1); }
	const workdir = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-driver-'));
	try { execSync('git init -q && git config user.email test@test && git config user.name test', { cwd: workdir, stdio: 'ignore' }); }
	catch { fs.mkdirSync(path.join(workdir, '.git'), { recursive: true }); }
	const ndjsonPath = path.join(workdir, '.wanxiangshu.ndjson');

	const mod = await import(pluginPath);
	const factory = mod.default ?? mod.wanxiangshuExtension ?? mod.plugin;
	if (typeof factory !== 'function') { respond(false, null, `Plugin does not export a factory function: got ${typeof factory}`); process.exit(1); }
	const runtime = new ExtensionRuntime();
	patchRuntime(runtime);
	let extension: any;
	try { extension = await loadExtensionFromFactory(factory, workdir, new EventBus(), runtime, 'wanxiangshu'); }
	catch (err) { respond(false, null, `loadExtensionFromFactory failed: ${err instanceof Error ? err.message : String(err)}`); process.exit(1); }
	process.stdout.write('ready\n');
	for (;;) {
		let cmd: Record<string, any> | null;
		try { cmd = await readStdinJson(); } catch { respond(false, null, 'Failed to parse stdin JSON'); continue; }
		if (!cmd) break;
		try {
			const done = await handleCommand(extension, workdir, ndjsonPath, cmd);
			if (done) { try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {} process.exit(0); }
		} catch (err) { respond(false, null, `Command ${cmd.type} failed: ${err instanceof Error ? err.message : String(err)}`); }
	}
	try { fs.rmSync(workdir, { recursive: true, force: true }); } catch {}
	process.exit(0);
}

main().catch((err) => { process.stderr.write(`driver fatal: ${err instanceof Error ? err.message : String(err)}\n`); process.exit(1); });
