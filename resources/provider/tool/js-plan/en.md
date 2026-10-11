# Tool · js-plan

The programming surface for reading and writing the planning draft `P`. `P` is the only persistently readable and writable file of this task; the system derives its path, and you never pass a path in arguments.

## Contract and Usage

1. **Sole Target**: operates only on the current task's draft `P`. No directory listing; no other file is readable or writable.
2. **Capability Primitives**: `read()` for the full content; `edit(patches)` for precise local patches; `rewrite(content)` for full replacement. Modifications stage first and commit atomically.
3. **No Repository Access**: this surface has no glob, no grep, and no access to repository files. For repository facts use `ask`.
4. **The Draft Is the State**: the current content of `P` is the single truth. Read before writing at the start of each incumbency; do not rely on memory of the previous runner's content.
