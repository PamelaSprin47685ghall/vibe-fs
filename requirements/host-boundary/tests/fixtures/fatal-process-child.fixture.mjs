process.env.NODE_TEST_CONTEXT = 'inherited-host-value'
delete process.env.WANXIANGSHU_NO_FATAL_EXIT

const { trip } = await import('../../../../dist/Foundation/FatalProcess.js')

console.log('before-fatal')
trip('fixture-fatal', 'fatal must terminate even when NODE_TEST_CONTEXT is inherited')
console.log('after-fatal')
