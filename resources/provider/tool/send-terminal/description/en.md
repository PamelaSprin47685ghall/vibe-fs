Send input to a named continuing terminal.

Use send-terminal when that process is waiting for your input.

This does not open a terminal, read its output, or signal it.
It does not mutate repository source.
Sending input is an act; it is not an ending.

name identifies the living process.
input is the text to send. A trailing newline is appended when missing.

A successful return means the input was sent, not that the process has ended
or answered.
