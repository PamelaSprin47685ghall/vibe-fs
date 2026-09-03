import test from 'node:test'
import { assertOptionalObservationNoninterference } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[DG-013] diagnostic failure cannot alter loop guard arm interrupt consume or continuation', async () => {
  await assertOptionalObservationNoninterference()
})
