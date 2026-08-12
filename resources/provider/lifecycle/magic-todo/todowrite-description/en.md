Replace the mission's entire living obligation account with {"obligations":[{"name":"stable human-readable name","work":"what is still owed and how verified"}]}.
Each obligation requires "name" (non-empty, unique within the list) and "work" (specific owed work).
Keep an obligation while it remains owed and remove it only after the work has actually discharged it.
Each accepted call synchronizes the preceding process review and starts the next checkpoint review.
Do not emit multiple todowrite calls in the same assistant message; any such batch is rejected entirely.
