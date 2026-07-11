process.on('unhandledRejection', (reason, promise) => {
  console.error('UNHANDLED REJECTION AT:', promise, 'REASON:', reason);
});

const target = process.argv[2];

let runAll;
if (target === 'mux') {
    runAll = (await import('../build/e2e/MuxTests.js')).runAll;
} else if (target === 'omp') {
    runAll = (await import('../build/e2e/TestsOmp.js')).runAll;
} else if (target === 'wanxiangzhen') {
    runAll = (await import('../build/e2e/WanxiangzhenPluginTests.js')).runAll;
} else if (target === 'opencode-context-budget') {
    runAll = (await import('../build/e2e/OpencodePluginContextBudgetE2e.js')).runAll;
} else if (target === 'opencode-plugin') {
    runAll = async (args) => {
        const suites = [
            '../build/e2e/OpencodePluginTests.js',
            '../build/e2e/MimocodePluginTests.js',
            '../build/e2e/MimoTuiPluginTests.js',
        ];
        let totalFailed = 0;
        for (const suite of suites) {
            const { runAll: suiteRun } = await import(suite);
            totalFailed += await suiteRun(args);
        }
        if (totalFailed > 0) {
            console.error(`\n✗ ${totalFailed} total failures across opencode-family e2e suites`);
        } else {
            console.log('\n✓ All opencode-family e2e suites passed');
        }
        return totalFailed;
    };
} else if (target === 'core') {
    runAll = (await import('../build/e2e/Tests.js')).runAll;
} else if (target === 'git-hooks') {
    runAll = async (args) => {
        console.log('\nRunning GitHookFormatterTests...');
        try {
            const { runAll: hookRun } = await import('./GitHookFormatterTests.js');
            return await hookRun(args);
        } catch (e) {
            console.error('Failed to import or run GitHookFormatterTests:', e);
            return 1;
        }
    };
} else {
    // Run everything by default if no target
    runAll = async (args) => {
        let totalFailed = 0;
        
        console.log('\n--- Running e2e core ---');
        const { runAll: coreRun } = await import('../build/e2e/Tests.js');
        totalFailed += await coreRun(args);

        console.log('\n--- Running e2e mux ---');
        const { runAll: muxRun } = await import('../build/e2e/MuxTests.js');
        totalFailed += await muxRun(args);

        console.log('\n--- Running e2e omp ---');
        const { runAll: ompRun } = await import('../build/e2e/TestsOmp.js');
        totalFailed += await ompRun(args);

        console.log('\n--- Running e2e wanxiangzhen ---');
        const { runAll: wanRun } = await import('../build/e2e/WanxiangzhenPluginTests.js');
        totalFailed += await wanRun(args);

        console.log('\n--- Running e2e opencode-family ---');
        for (const suite of ['../build/e2e/OpencodePluginTests.js', '../build/e2e/MimocodePluginTests.js', '../build/e2e/MimoTuiPluginTests.js']) {
            const { runAll: suiteRun } = await import(suite);
            totalFailed += await suiteRun(args);
        }

        console.log('\n--- Running GitHookFormatterTests ---');
        try {
            const { runAll: hookRun } = await import('./GitHookFormatterTests.js');
            totalFailed += await hookRun(args);
        } catch (e) {
            console.error('Failed to run GitHookFormatterTests:', e);
            totalFailed++;
        }

        return totalFailed;
    };
}

const extraArgs = process.argv.slice(3);
runAll(extraArgs)
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err);
        process.exit(2);
    });