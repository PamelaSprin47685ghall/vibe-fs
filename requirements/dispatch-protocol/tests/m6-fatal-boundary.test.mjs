import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[DISPATCH-PROTOCOL-014] dispatch fatal preserves exact claim truth and one injected fuse', () => assertFatalBoundary('dispatch-protocol'))
