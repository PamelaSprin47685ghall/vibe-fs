import { runAll } from '../build/tests/Tests.js';

runAll(process.argv.slice(2)).then(code => process.exit(code));
