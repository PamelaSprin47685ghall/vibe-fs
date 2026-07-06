export function createOmpHarness({ sendCommand, child, releaseLock, mockLlm }) {
    return {
        handlers: {},
        tools: [],

        async expectText(text) {
            if (mockLlm) mockLlm.expectText(text);
        },

        async expectTool(tool, args) {
            if (mockLlm) mockLlm.expectTool(tool, args);
        },

        async getToolNames() {
            const res = await sendCommand({ type: 'getToolNames' });
            if (!res.ok) throw new Error(res.error || 'getToolNames failed');
            return res.data.toolNames;
        },

        async getCommands() {
            const res = await sendCommand({ type: 'getCommands' });
            if (!res.ok) throw new Error(res.error || 'getCommands failed');
            return res.data;
        },

        getRemainingExpectations() {
            if (mockLlm) return mockLlm.getRemainingExpectations();
            return 0;
        },

        async runCommand(name, args, sessionId) {
            const res = await sendCommand({ type: 'runCommand', name, args, sessionId });
            if (!res.ok) throw new Error(res.error || 'runCommand failed');
            return res.data;
        },

        async triggerTool(name, params, sessionId, extraCtx) {
            const res = await sendCommand({ type: 'callTool', toolCallId: sessionId, name, params, sessionId });
            if (!res.ok) throw new Error(res.error || 'callTool failed');
            return res.data;
        },

        async emitEvent(name, event, sessionId) {
            const res = await sendCommand({ type: 'emit', eventType: name, event, sessionId });
            if (!res.ok) throw new Error(res.error || 'emit failed');
            return res.data;
        },

        async readNdjson() {
            const res = await sendCommand({ type: 'readNdjson' });
            if (!res.ok) throw new Error(res.error || 'readNdjson failed');
            return res.data.content;
        },

        async readFile(p) {
            const res = await sendCommand({ type: 'readFile', path: p });
            if (!res.ok) throw new Error(res.error || 'readFile failed');
            return res.data.content;
        },

        async fileExists(p) {
            const res = await sendCommand({ type: 'fileExists', path: p });
            if (!res.ok) throw new Error(res.error || 'fileExists failed');
            return res.data.exists;
        },

        async waitForNdjson(min, maxMs) {
            const res = await sendCommand({ type: 'waitForNdjson', min, maxMs });
            if (!res.ok) throw new Error(res.error || 'waitForNdjson failed');
            return res.data.ready;
        },

        async dispose() {
            releaseLock();
            try { await sendCommand({ type: 'dispose' }); } catch {}
            try { child.kill('SIGTERM'); } catch {}
            await new Promise((resolve) => {
                const t = setTimeout(() => { try { child.kill('SIGKILL'); } catch {} resolve(); }, 5000);
                child.once('exit', () => { clearTimeout(t); resolve(); });
            });
        },
    };
}
