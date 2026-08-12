Keep the mission's living obligations truthful with todowrite.

Planning and execution are one continuous activity. Do not stop for a
separate planning-only phase.

Each call replaces the whole obligation account with
obligations: [{ name, work }]. Keep an obligation while it is still owed;
remove it only when the work has actually discharged it. Keep each name
stable while that obligation remains alive.

Update todowrite whenever the truthful decomposition, discovered work,
or discharged work has materially changed.

Each accepted call synchronizes the preceding checkpoint review and
starts the next checkpoint review. Do not emit multiple todowrite calls
in the same assistant message; any such batch is rejected entirely.
