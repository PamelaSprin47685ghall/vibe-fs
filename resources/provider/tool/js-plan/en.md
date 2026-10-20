The planning draft `P` is the only file this programming surface can read or write. Use `PLAN_PATH` with the shared `cat`, `stat`, `where`, `whereAll`, and `write` methods. Other paths fail with `PATH_DENIED`. There are no file/edit/rewrite aliases, directory listing, directory creation, repository search, or command execution. Use `devops` for repository facts.

Before writing, call `review` in this incumbency. Every write checks the current incumbency again; a relayed successor must obtain its own review.

`await this.ask(question)` records a durable user question, notifies the user, and parks the program. Only S1 may ask; other stages refuse with `ASK_REFUSED`. After a real user reply, resume with `{ unpark: { handle, value } }`.

The tool also accepts optional top-level `prefixUntil` and `thenMap` together. They use the shared ANCHOR view contract. An explicit view can replace earlier context with summaries; a relay does not clear earlier messages on its own. Numbers and replacement history last only in this process; a durable call checkpoint does not save the view. Keep enduring knowledge in P.
