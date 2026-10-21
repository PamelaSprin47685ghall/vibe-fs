# CUT-35 evidence

- `targeted-files.txt`: the 66 test files run on base and on every candidate (`node --test --test-concurrency=4`).
- `base-1b50bafbe.fail`: failing test names on master `1b50bafbe` after a full build (58).
- `candidate-22f6e8ac5.fail`: failing names on the last candidate (c35d round 2, `22f6e8ac5` on `5e76cb4a4`) after a full build (58).
- The two lists are identical (`comm -3` is empty). Runner: `wt/logs-cut28b/verify-tree2.sh`; landing loops: `wt/logs-cut35/land35.sh`, `wt/logs-cut35b/land.sh`; raw logs `wt/logs-cut35/run35{a,b,c}.out`, `wt/logs-cut35b/run35d.out` (box-local).
- Node 22 (`/workspace/vfs-cut/tools/node22`), wanxiangli gitlink 0.62.0.
