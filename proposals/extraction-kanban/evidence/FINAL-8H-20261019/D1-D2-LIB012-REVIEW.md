# D1/D2 + wanxiangli0.6.12: final bounded independent review

Date: 2026-10-19. Reviewer: non-author d1_review_git. Main HEAD 3e7d1f53aa7a2b372cbcf6a4ca61ece5019fe626; library HEAD 8bb1021fc3519b0846e70af3319be9822d4b6b80. Both worktrees were clean when read. Reviewed the delivered D1 commit 54c7a9266 and D2 commit 5157c4915 and the corresponding actual current source. No shared edits, build, moving dist reads, new test execution, commit or external effect was performed.

## Finding

No concrete new regression or blocking interface mismatch was found in this limited source review. Existing archived fixed-package tests remain the execution evidence; this review does not add new passing test cases or claim all Process/Host/PTY behavior is proved. No unresolved new risk justified repeating the large green matrices.

## Checked integration boundaries

- ProcessRequest.fs:88 requireText only accepts a confirmed real Exited code, no stop, no fault and separate complete in-memory text. Signals, Unconfirmed, mixed spool and stopped execution cannot silently become ordinary Git exit1, empty stdout or success.
- ProcessRunner.fs:42 and ProcessGitRawStore.fs:426 independently require capture/scope closure, no fault and no stop before claiming complete output. The D1 stopped-capture regression is not restored by the D2 binary adapter. Target end evidence remains separate, so a known target exit is preserved even if output/cleanup fails.
- NodeProcessHost.fs:259 serializes preselected16MiB input blocks, waits for Written before advancing and requests stop on refusal. It does not resend a failed block. Empty Some input still reaches endInput; binary bytes do not pass through UTF8 text conversion. The bridge's observer at388 validates the actual target exit/signal rather than manufacturing it from supervisor close.
- Library ProcessSession.fs:181 keeps one pending copied input and returns Unknown on write failure. NativeProcess/BunProcess.fs:138 and162 keep the pending native write until success is published on setImmediate; an earlier exit/error completes it once and late success cannot clear or overwrite a later write. This is the documented local native-callback boundary, not the explanation of the separately diagnosed iterator tail17 loss.
- Library supervision/Native.fs:105 marks the pump pending before source.read; synchronous end/error/readable cannot reenter the same read. The stopped check after read prevents writing bytes after a source error. The final true end is forwarded only after read returns null and the final write callback has completed. Close without end is faulted. Target refusal detaches the pump without destroying borrowed stdin or discarding target output, as Worker.fs:72-74 intends.
- Written remains the acknowledged first-hop pipe write, not proof that the target consumed all bytes. This documented scope is necessary when target stdin rejects. D2's actual Git roundtrip and target-original evidence, rather than the Written label alone, establishes the accepted large input paths.
- Library SupervisedProcess.fs:147 and213 retains separate exact stdout/stderr extents, target exit and output failure. It does not turn supervisor cleanup or interrupted capture into complete target output. Main stopped/failed result gates remain conservative on top of it.
- ProcessGitRawStore.fs:37 only parses a complete no-stop/no-fault real exit. The strict metadata/body/tree paths at284/312 preserve raw bytes and length bounds; cache writes occur after successful validation, and object-return byte copies avoid mutable cache aliasing. Missing object/tree content remains an error, including native Git's ambiguous missing result. ReadRef at343 uses the explicit Git>=2.43 existence status, separately from object lookup.
- Git/Gateway.fs:123 and235 distinguish execution uncertainty from confirmed Git rejection. Execution failure is returned directly before rediscovery/retry; no Unknown push is resent. Operations and Change GitAdapter use typed result short-circuiting, require stale-ref deletion success and do not continue writes after an unsuccessful state query. Spool paths are rejected and released by the text adapter rather than supplied as empty stdout. Hook formats the typed Git-storage failure, retaining stderr diagnostics.

## Version and owner evidence

git diff 5157c4915..HEAD was empty for Process, Git, ProcessGitRawStore, GitTypes and WriterStreamSync. The later G140/R1 work therefore did not introduce a new source version of the mechanisms reviewed here. Main vendor/wanxiangli-0.6.12.tgz SHA256 is a039ba669299c6919776196fd9b462624bcbe515d03593804596dbc86f43ac56, matching the library's recorded 22-file delivered candidate and its fixed 663 integration.

The physical library still owns native process I/O and same-group supervision. Main owns text/spool collection, explicit process observations, budgets and Git plumbing composition. RawStore does not recreate loose ODB, refs or a second execution backend. Shared private Bun I/O is the explicitly permitted exact shared path, rather than cross-importing ProcessSession/SupervisedProcess state. The reviewed changes remain consistent with those applicable main and library AGENTS responsibility boundaries.

Archived proof referenced: main PROCESS-TERMINATION-D1-2026-10-18.md, PROCESS-BINARY-D2-2026-10-18.md and evidence/R6-D1/R6-D2; library evidence/process-stdin-0.6.12/README.md and receipt.json. In particular, the0.6.11 local barrier green did not erase the661/662 tail17 red; the0.6.12 explicit pump has its own actual input/callback/EOF and fixed-package consumer evidence. Author-introduced regressions and reviewer hypotheses remain separately classified in those records.

## Scope retained

This does not reopen or classify R12-U1 Unknown as an implementation bug. It does not claim escaped-process ownership, PTY/restart completion, Windows/Linux coverage, complete RSS bounds, deployed Host execution or registry publication. Hook latency and independent remaining cards do not become new D1/D2 regressions merely because their broader obligations remain open.

## Selected source SHA256

- ProcessRequest.fs bd3224109f60131c8a2abd35cffd7262730412c639ce40da0c7a21350b2a72f8
- ProcessRunner.fs b80dbe49743b9f6845ff047966b9a65cb94a4ba2510f62f2d50c7a2c81f9022a
- NodeProcessHost.fs b0bd8db09ac487f0f837b5f8572686d829465021c2766168764f7452daf74a2b
- NodeProcessWait.fs 5971019ccc5e0c7734c7deeea59133a1fc04a167f4828a9d2e62b711414762cd
- ProcessGitRawStore.fs 029214ea3032a8319142218f813bd9b1ca8be5d89e2788aef2a951b35f37c483
- Git/Gateway.fs 9891b32aeedbf786a2547f1dead8692cf9b2ffe7c3cb5076539c91ec6e67a365
- Library supervision/Native.fs ae6f8f6d3d900ebc6929af5cc438952d40e59ce5bab21c21e4c804060efd32bf
- Library supervision/Worker.fs 67073afafc4b4aa69526f225b54e53a2d7620a6568585f9eccc9d264da34766c
- Library native-process/BunProcess.fs 38b447437c4569d4c9af4dd23787d5ba8c9de0853b25fdb2f255e8a5fb5d4233
- Library process/ProcessSession.fs 1daa8686af3227f0ceb53b18f47298e9c99f2a07bf50b232ebaa67b91aacbe37
- Library supervision/SupervisedProcess.fs d7ed25e87e259b5ec35cccf242d3027fc7e195543ef251a966031b1539ec6df9
