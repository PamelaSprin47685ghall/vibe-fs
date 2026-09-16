import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[REPOSITORY-PROGRAMMING-025] transaction fatal preserves rollback or cut settlement and one injected fuse', () => assertFatalBoundary('repository-programming'))
