deliver(value) — end this program now with an explicit terminal value. The program does not continue past the call: the delivery unwinds the stack and the value becomes the call's result, exactly as `return` would. Use it when the ending is a declaration, not an accident: a subagent program must deliver explicitly instead of falling off the end of `run()`.

The value is the program's own data. Catching the internal delivery sentinel keeps the program alive — the later return value then wins — so a holder that catches it is knowingly cancelling the delivery.
