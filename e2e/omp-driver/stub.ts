import type { ExtensionRuntime } from '../../packages/coding-agent/src/extensibility/extensions/loader';

export function createStubExtensionContext(cwd: string, sessionId: string) {
	const noop = () => undefined as any;
	const authStorage: any = new Proxy({
		onCredentialDisabled: () => {},
		setFallbackResolver: () => {}
	}, {
		get(target: any, key: string) {
			if (key in target) return target[key];
			if (key === 'hasAuth') return () => true;
			if (key === 'getApiKey') return () => Promise.resolve('test-key');
			return () => undefined;
		}
	});
	const ompRepo = process.env.WANXIANGSHU_OMP_REPO || process.cwd();
	const ModelRegistryClass = require(`${ompRepo}/packages/coding-agent/src/config/model-registry`).ModelRegistry;
	const modelRegistry = new ModelRegistryClass(authStorage);
	const mockModel = modelRegistry.find('openai', 'gpt-4o') || {
		id: 'gpt-4o',
		name: 'GPT-4o',
		provider: 'openai',
		model: 'gpt-4o',
		api: 'openai-completions'
	};
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
		authStorage,
		getContextUsage: () => undefined,
		compact: async () => {},
		hasUI: false,
		cwd,
		sessionManager: { getSession: () => ({ id: sessionId }), getSessionId: () => sessionId, getCurrentSession: () => ({ id: sessionId }), on: noop },
		modelRegistry,
		get model() { return mockModel; },
		models: { list: () => [mockModel], current: () => mockModel, resolve: () => mockModel, family: () => 'openai' },
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
