Read unread output from a named continuing terminal.

Use read-terminal when new output may change what you do next.

Reading reveals output. It does not reveal endings; that is join.
It does not send input, signal the process, or mutate repository source.

name identifies the living process.

A successful return is newly appeared output, or that nothing new has
appeared. Absence of new output is not termination.
