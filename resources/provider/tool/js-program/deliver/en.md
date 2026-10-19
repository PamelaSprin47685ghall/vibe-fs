deliver(value) — end this program now with an explicit terminal value. The program does not continue past the call: the delivery unwinds the stack, records `value` into the session's delivery cell, and makes it the call's result. Subagent programs must deliver explicitly; a plain `return` leaves the delivery cell empty and settles the child as `undelivered` (`node-undelivered` in `dag`).

The value is the program's own data. Catching the internal delivery sentinel keeps the program alive — the later return value then wins — so a holder that catches it is knowingly cancelling the delivery.
