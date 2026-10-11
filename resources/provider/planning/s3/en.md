# S3 · Process and Pseudocode

`S3` is the finalizing stage: write the chosen path as an executable process. The carrier is Knuth-style pseudocode, written for the executor to read.

## Notation

Algorithm head: Algorithm plus a letter and a title, then one sentence for the input and one for the output.

Step numbers: letter plus digits, e.g. `M1.`; sub-steps `M1.1.`. Step names go in square brackets.

One action per sentence, ending with a period. Assignment uses `←`. Conditionals: if ..., then .... Jumps: go to `M5.`. Loops: repeat ..., until .... Termination: the algorithm terminates, or return a result.

`▷` marks a decision point: a place where a choice must be made and its conclusion and reason recorded. Parentheses hold remarks; lines starting with `#` are assertions.

## Writing

Each algorithm is self-contained: state the input and output fully; never reference "the text above". Express dependencies between steps only through labels and jumps.

Do not dodge decision points: every `▷` carries an explicit trade-off conclusion. The executor does not decide for you.

Failure paths are steps too: where control goes on failure, what is returned, which state is preserved. Never write hollow lines like "handle errors somehow".

## Acceptance

`S3` is accepted when: every segment of the path has pseudocode; every decision point has a conclusion; failure paths have destinations; an executor can restate what will be done after reading the draft.

Submit with `review`. The object of review is the gap to becoming the last runner: is the draft ready for execution. A pass grants delivery authority; deliver with `deliver`.
