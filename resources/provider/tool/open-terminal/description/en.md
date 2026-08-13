Open a named continuing interactive process with a command.

Use open-terminal when interactive state itself must remain present across
turns: a REPL, a long-lived service, a wizard, a process that waits for input.

This is not a bounded command with a sought ending; that is run.
It does not mutate repository source.
It does not send input, read output, or signal the process.

name is the human name by which this living process remains recognizable.
command is what starts it.
A name may be used again only after its previous ending has been heard.

A successful return means that named terminal is open, not that the
operational objective is complete.
