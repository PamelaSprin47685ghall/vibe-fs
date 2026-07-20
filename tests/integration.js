process.on('unhandledRejection', (reason, promise) => {
    console.error('UNHANDLED REJECTION AT:', promise, 'REASON:', reason);
});

// Integration tests run the opencode plugin end-to-end; disable the
// retry-dispatch rate-limit backoff so mocked prompts resolve promptly.
process.env.WANXIANGSHU_TEST = 'true';

// Run the integration subset of the F# test suite (labels starting with
// "Integration"). Opencode-family plugin tests live under tests/integration/
// and are wired through the main Tests.fs entry point.
const { runAll } = await import('../build/tests/Tests.js');
const code = await runAll(['Integration']);
process.exit(code);
