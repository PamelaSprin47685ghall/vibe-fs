console.log('before-fatal')
console.error(JSON.stringify({ diagnostic: 'fatal must terminate even when NODE_TEST_CONTEXT is inherited' }))
process.exit(1)
