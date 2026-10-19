# Tool · ask

Ask the user a concrete question that must be answered before the task can proceed. Pass the question, including the decision or missing information you need, in `question`.

On success, the tool returns an acknowledgement, not an answer. The system records the wait and interrupts this attempt after the Host accepts the tool result. The user's next real input resumes the same unfinished task. Do not issue another tool or end the task yourself while waiting.

This tool is for Plan only; Manager is forbidden from calling `ask`. Use `devops` for Plan investigations. Plain prose, notifications and an ordinary stop never authorize a user wait. A failed `ask` does not authorize stopping.
