# User pause, 2026-10-21

Both original runs have stopped and all 13 recorded process IDs are absent in `ps` and `lsof`. The original harness status remains `Failed`, exit 1, because its cancellation path uses that status. The pause receipt records the user-requested reason separately. Neither requested duration has passed, and no complete phase has passed.

Canonical receipt: `/private/tmp/vibe-fs-physical-soak-20261021-a/pause-receipt.json`. An identical copy is in the PTY test directory. The offline receipt generator is `pause-receipt.mjs`; it reads the saved evidence, checks the PTY cleanup HMAC and own PID absence, and starts no product process.

## Complete Process/Supervision source set

- `/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/coordinator.mjs`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/lane.mjs`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/worker.mjs`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/package.json`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/package-lock.json`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/harness-sha256.txt`
- `/private/tmp/vibe-fs-physical-soak-20261021-a/launch.json`

There is no separate node-liveness helper: `worker.mjs`, mode `keeper`, imports public `wanxiangli/time-node` and executes the actual four-hour owned keepalive wait. The same file contains the paced target and final descendant modes. `lane.mjs` implements the continuous output/input checks and physical process/FD sampling. `coordinator.mjs` starts one Node22 lane and one Bun lane.

Fixed library: commit `3db7b439283779f6037244d57cf05e6ec3e7ab31`, package `/private/tmp/vibe-fs-module-baseline-20261021/final18/wanxiangli-0.9.0.tgz`, SHA-256 `2b7ce8d4c96bc5563e807e4c082c7dc4fa049afe7caf6432d4e25fece7292da7`. Package build/consumer receipt: `/private/tmp/vibe-fs-module-baseline-20261021/final18/receipt.json`.

Original exact command, run from the consumer directory:

```sh
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node /private/tmp/vibe-fs-physical-soak-20261021-a/consumer/coordinator.mjs
```

The coordinator launches that exact Node22 binary and `/opt/homebrew/bin/bun` (1.3.14), with PATH `/usr/bin:/bin:/usr/sbin:/sbin`, per-lane own TMPDIR, and no inherited environment spread. Per-lane stdout/stderr are saved in `runner.log`.

## Complete PTY source set

- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/controller.mjs`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/pty-soak.mjs`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/target.mjs`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/package.json`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/harness-sha256.txt`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/launch.json`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/gen100-build-manifest.json`
- `/private/tmp/vibe-fs-pty-soak-gen100-20261021/copied-artifact-receipt.json`

The immutable copied runtime is under this consumer's `dist/`, `node_modules/bun-pty/`, and `node_modules/wanxiangli/`. It contains 831 byte-equal files with per-file hashes in the artifact receipt; no symlinks or main-worktree fallback. The gen100 manifest SHA-256 is `44ee86790fc57398105f36b03b6f736949ea5c00babd0e7b291d48b924514ea0`. Its installed library package version is 0.9.2; it must not be confused with the independent Process test's fixed 0.9.0 package. The real native package is bun-pty 0.4.10.

Original exact command, run from the consumer directory:

```sh
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node /private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/controller.mjs
```

The controller launches `/opt/homebrew/bin/bun` with PATH `/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin` and this test's own TMPDIR. It has a finite independent SIGCONT/SIGKILL cleanup envelope. The target lives in `target.mjs`; no external target helper is missing.

## Restart, only after a new authorization

1. Keep these experiment directories and logs unchanged. Create fresh sibling roots, not subdirectories inside the old witness directories.
2. Copy the complete corresponding source set. Manually update its explicit root constants in coordinator/controller/harness to the fresh root. The worker and PTY target locate their own supporting files by `import.meta.url`; no code generation is needed.
3. For Process, create fresh `consumer`, `node22/tmp`, and `bun/tmp` directories. Preserve the package/lock relative layout: from the new consumer, `../../vibe-fs-module-baseline-20261021/final18/wanxiangli-0.9.0.tgz` must resolve to the fixed tarball. Verify its SHA-256 above, then run `npm ci --ignore-scripts` from that consumer. If the tarball location changes, update both the local `file:` dependency and its lock normally before installation. Preserve the installed package as the only `wanxiangli` resolution; do not use a neighboring checkout fallback. Verify Node22/Bun versions. Do not run a test during reconstruction.
4. For an identical gen100 PTY run, copy the retained immutable runtime directories above to the new consumer and verify every path/hash against `copied-artifact-receipt.json`; preserve `package.json` and create the fresh root's `tmp` directory. If these artifacts are unavailable, build a separately identified fresh main candidate with the supported Fable command and record its own manifest and hashes. Such a run is a new candidate, not a claim of exact gen100 reconstruction.
5. Run the corresponding original command with its new root. Both experiments always start from zero; neither supports resuming a canceled phase. Retain updated launch and harness hash receipts. The requested minimums are four real hours and two real hours, respectively, unless the user changes the scope.
6. The old main and library source trees need no changes to restore these consumers. No product probe or restart was performed while preparing this handover.

## Saved limits

Process collectors kept running hashes only in memory. Requested cancellation did not write final channel digests or a target stdin EOF witness. Their actual observed byte counts are retained; do not synthesize an expected digest and call it observed. The final canceled Wait snapshot has one active descriptor per exited harness, so normal owned-registry settlement was not verified.

PTY raw output is retained in `output.jsonl`. The offline receipt verifies the complete received 12,200-byte prefix against all 89 actual acknowledged responses and stores its SHA-256. A generated input hash is explicitly distinct from the unobserved target EOF aggregate digest. The cleanup terminal is HMAC-verified Ended/SIGTERM/stopped=true; the planned natural EOF/exit23/descendant/Unknown/TERM-KILL sequence was not exercised. Durable claim/request/available/terminal files remain as evidence; the live control socket and scope directory are absent.
