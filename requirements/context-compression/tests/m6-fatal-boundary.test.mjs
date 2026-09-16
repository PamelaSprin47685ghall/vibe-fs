import test from 'node:test'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[CONTEXT-COMPRESSION-025] Blogger fatal binds exact request settlement and one injected fuse', () => assertFatalBoundary('context-compression'))
