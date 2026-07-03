import type { ExtensionRuntime } from '../../packages/coding-agent/src/extensibility/extensions/loader';

export function createStubExtensionContext(cwd: string, sessionId: string) {
	const noop = () => undefined as any;
	const ui: any = new Proxy({}, { get(_, key) {
		if (key === 'theme') return {};
		if (key === 'getEditorText' || key === 'getToolsExpanded') return () => '';
		if (key === 'setTheme') return async () => ({ success: false, error: 'no UI' });
		if (key === 'getTheme' || key === 'getAllThemes') return async () => undefined;
		if (key === 'confirm') return async () => false;
		return async () => undefined;
	}});
	return {
		ui,
		getContextUsage: () => undefined,
		compact: async () => {},
		hasUI: false,
		cwd,
		sessionManager: { getSession: () => ({ id: sessionId }), getSessionId: () => sessionId, getCurrentSession: () => ({ id: sessionId }), on: noop },
		modelRegistry: new Proxy({}, { get: () => undefined as any }),
		get model() { return undefined; },
		models: { list: () => [], current: () => undefined, resolve: () => undefined, family: () => '' },
		isIdle: () => true, abort: noop, hasPendingMessages: () => false, shutdown: noop, getSystemPrompt: () => [],
	};
}

export function patchRuntime(runtime: ExtensionRuntime) {
	const stubs: Array<[string, unknown]> = [
		['sendMessage', () => {}],
		['sendUserMessage', () => {}],
		['appendEntry', () => {}],
		['setLabel', () => {}],
		['getActiveTools', () => []],
		['getAllTools', () => []],
		['setActiveTools', async () => {}],
		['getCommands', () => []],
		['setModel', async () => false],
		['getThinkingLevel', () => undefined],
		['setThinkingLevel', () => {}],
		['getSessionName', () => undefined],
		['setSessionName', async () => {}],
	];
	for (const [k, v] of stubs) (runtime as any)[k] = v;
}
