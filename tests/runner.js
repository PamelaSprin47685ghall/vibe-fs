process.on('unhandledRejection', (reason, promise) => {
  console.error('UNHANDLED REJECTION AT:', promise, 'REASON:', reason?.stack || reason);
});

process.env.WANXIANGSHU_TEST = 'true';

const { runAll } = await import('../build/tests/Tests.js');

runAll(process.argv.slice(2))
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err?.stack || err);
        process.exit(2);
    });
