const isE2e = process.argv.includes('--e2e');
const isE2eOmp = process.argv.includes('--e2e-omp');
const runAll = isE2eOmp
    ? (await import('../build/e2e/TestsOmp.js')).runAll
    : isE2e
        ? (await import('../build/e2e/Tests.js')).runAll
        : (await import('../build/tests/Tests.js')).runAll;

runAll(process.argv.slice(2).filter(a => a !== '--e2e' && a !== '--e2e-omp'))
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err && err.message ? err.message : err);
        process.exit(2);
    });