const isMuxE2e = process.argv.includes('--e2e-mux');
const isE2e = process.argv.includes('--e2e');
const isE2eOmp = process.argv.includes('--e2e-omp');

let runAll;
if (isMuxE2e) {
    runAll = (await import('../build/e2e/MuxTests.js')).runAll;
} else if (isE2eOmp) {
    runAll = (await import('../build/e2e/TestsOmp.js')).runAll;
} else if (isE2e) {
    runAll = (await import('../build/e2e/Tests.js')).runAll;
} else {
    runAll = (await import('../build/tests/Tests.js')).runAll;
}

runAll(process.argv.slice(2).filter(a => a !== '--e2e' && a !== '--e2e-mux' && a !== '--e2e-omp'))
    .then(code => process.exit(code))
    .catch(err => {
        console.error('RUNALL_FAILED:', err && err.message ? err.message : err);
        process.exit(2);
    });