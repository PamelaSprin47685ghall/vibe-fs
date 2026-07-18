process.on('unhandledRejection', (reason, promise) => {
    console.error('UNHANDLED REJECTION AT:', promise, 'REASON:', reason);
});

// Run the integration subset of the F# test suite (labels starting with
// "Integration"). The opencode-family plugin tests moved from e2e/ to
// integration/ are not wired here yet because their legacy harness cleanup
// needs separate hardening (see follow-up).
const { runAll } = await import('../build/tests/Tests.js');
const code = await runAll(['Integration']);
process.exit(code);
