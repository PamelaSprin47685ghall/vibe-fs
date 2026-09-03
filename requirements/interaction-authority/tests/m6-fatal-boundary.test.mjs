import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[INTERACTION-AUTHORITY-020] repair fatal preserves exact claim settlement and one injected fuse', () => assertFatalBoundary('interaction-authority'))
