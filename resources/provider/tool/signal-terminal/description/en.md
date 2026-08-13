Send a structured signal to a named continuing terminal.

Use signal-terminal when process control is required.

A signal is an act, not an exit.
Do not treat the process as ended until its ending arrives.
This does not read output, send input, or mutate repository source.

name identifies the living process.
signal is one of INT, TERM, KILL, HUP, QUIT, USR1, USR2.

A successful return means that signal was sent, not that the process has
exited.
