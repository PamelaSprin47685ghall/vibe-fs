/**
 * e2e/omp-driver/ipc.ts — stdin/stdout JSON IPC channel.
 *
 * FIFO command dispatch: readline pushes parsed JSON onto a queue,
 * readStdinJson pops or registers a waiting resolver.
 */

import readline from 'node:readline';

export function respond(ok: boolean, data?: unknown, error?: string) {
	const res: Record<string, unknown> = { ok };
	if (data !== undefined) res.data = data;
	if (error !== undefined) res.error = error;
	process.stdout.write(JSON.stringify(res) + '\n');
}

const commandQueue: Array<Record<string, any> | null> = [];
const waitingResolvers: Array<(cmd: Record<string, any> | null) => void> = [];
let rlStarted = false;

function dispatch(cmd: Record<string, any> | null) {
	if (waitingResolvers.length > 0) waitingResolvers.shift()!(cmd);
	else commandQueue.push(cmd);
}

export function readStdinJson(): Promise<Record<string, any> | null> {
	if (!rlStarted) {
		rlStarted = true;
		const rl = readline.createInterface({ input: process.stdin, terminal: false });
		rl.on('line', (line) => {
			const trimmed = line.trim();
			if (!trimmed) { dispatch(null); return; }
			try { dispatch(JSON.parse(trimmed)); } catch { dispatch(null); }
		});
		rl.on('close', () => { while (waitingResolvers.length) waitingResolvers.shift()!(null); });
	}
	if (commandQueue.length) return Promise.resolve(commandQueue.shift()!);
	return new Promise((resolve) => waitingResolvers.push(resolve));
}
