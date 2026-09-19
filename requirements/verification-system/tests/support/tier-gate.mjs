// requirements/verification-system/tests/support/tier-gate.mjs
// Tier-gating helper for multi-tier test execution (unit / integration / release).

import test from 'node:test'

export const INTEGRATION_TIER_ENV = 'WXS_TIER_INTEGRATION'
export const RELEASE_TIER_ENV = 'WXS_TIER_RELEASE'

export function isIntegrationTier() {
  return process.env[INTEGRATION_TIER_ENV] === '1'
}

export function isReleaseTier() {
  return process.env[RELEASE_TIER_ENV] === '1'
}

function createTierGatedTest(isTierActive, tierName) {
  function gatedTest(name, options, fn) {
    if (typeof options === 'function') {
      fn = options
      options = {}
    }
    if (!isTierActive()) {
      return test.skip(name, options, fn)
    }
    return test(name, options, fn)
  }

  gatedTest.skip = (name, options, fn) => test.skip(name, options, fn)
  gatedTest.only = (name, options, fn) => {
    if (typeof options === 'function') {
      fn = options
      options = {}
    }
    if (!isTierActive()) {
      return test.skip(name, options, fn)
    }
    return test.only(name, options, fn)
  }
  gatedTest.todo = (name, options, fn) => test.todo(name, options, fn)

  return gatedTest
}

export const integrationTest = createTierGatedTest(isIntegrationTier, 'integration')
export const releaseTest = createTierGatedTest(isReleaseTier, 'release')
export const e2eTest = releaseTest
