# Restart Status Guidance

The Wanxiangshu process has just restarted. Process-local state is gone, but durable facts, the shared workspace, and the Git repository remain.

This means:

- Tool calls that were running in the previous process are treated as interrupted: they stay failed in visible history and are never replayed or completed implicitly.
- Active child work left over by the previous runtime can no longer return, so it is voided rather than completed: `horizon` shows it as voided and `join` reports it once, then it retires.
- This process has already re-normalized from durable facts. Continue from the current real facts and do not assume the previous process's temporary state still exists.
