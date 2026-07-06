const isE2eMux = process.argv.includes('--e2e-mux');
const isE2e = process.argv.includes('--e2e');
const isE2eOmp = process.argv.includes('--e2e-omp');
const isE2eOpencodePlugin = process.argv.includes('--e2e-opencode-plugin');

let runAll;
if (isE2eMux) {
    runAll = (await import('../build/e2e/MuxTests.js')).runAll;
} else if (isE2eOmp) {
    runAll = (await import('../build/e2e/TestsOmp.js')).runAll;
} else if (isE2eOpencodePlugin) {
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
} else if (isE2e) {
    runAll = (await import('../build/e2e/Tests.js')).runAll;
} else {
    runAll = (await import('../build/tests/Tests.js')).runAll;
}

const extraArgs = process.argv.slice(2).filter(a => a !== '--e2e' && a !== '--e2e-mux' && a !== '--e2e-omp' && a !== '--e2e-opencode-plugin');
runAll(extraArgs)
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err);
        process.exit(2);
    });