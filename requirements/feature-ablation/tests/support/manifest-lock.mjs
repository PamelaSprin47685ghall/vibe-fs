// One in-process mutex for tests that rewrite a shared manifest on disk.
//
// `node --test` loads these files into one process and may interleave their bodies,
// so a test that mutates `resources/ablation/nodes.json` and restores it afterwards
// can be observed by a sibling mid-mutation. Serialising the whole mutate/restore
// window — not just the write — is what makes the restore unconditional.
let chain = Promise.resolve()

export const withManifestLock = async (run) => {
  const previous = chain
  let release
  chain = new Promise((resolve) => {
    release = resolve
  })
  await previous
  try {
    return await run()
  } finally {
    release()
  }
}
