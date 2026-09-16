import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[ENF-020] invalid configuration reaches one injected fatal adapter only through composition', () => assertFatalBoundary('capability-enforcement', 'not-required'))
