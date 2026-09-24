A standard jq program evaluated over the current persistent canvas. The current canvas is `.`.

It must produce exactly one JSON value, and that value atomically becomes the new canvas and commits a phase.

Use `.` for a no-op update when you only want to declare todos while keeping the canvas unchanged; that is still a real phase commit.

Zero outputs, several outputs, or a jq compile/runtime failure reject the whole call: canvas, todos, phase ordinal and epoch all stay unchanged.
