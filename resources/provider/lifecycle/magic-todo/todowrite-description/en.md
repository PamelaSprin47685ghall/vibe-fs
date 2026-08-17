Replace the Manager's entire current owed-work account with `planComplete`, a `workingOn` focus name, and stable `{name,work}` obligations.

Use `planComplete=false` while the road is still being planned. In that relation, obligations may honestly describe concrete planning work still owed: investigation, analysis, decomposition, or decisions needed to finish the plan. Do not disguise planning as mission work merely to satisfy the ledger.

Set `planComplete=true` only when the road is complete enough to entrust and this submission is the complete mission-debt account you are willing to carry. The first accepted true is irreversible for this Manager Life: afterward the effective value stays true forever, even if a later call says false. There is no second first true.

Once effective `planComplete=true`, obligations must describe what still has to become true for the user's request, including closure evidence. Apply the completion counterfactual then: work whose perfect completion would only improve your understanding, inventory, plan, or next-step decision is planning cognition, not mission debt, unless that investigation, analysis, audit, diagnosis, or report is itself the user's requested deliverable.

In either relation, every obligation must be concrete and closable: another competent Manager must be able to tell what work is owed and what would close it. A slot-reserving label, bare phase name, `placeholder`, `TBD`, or deferred decision with no actual owed work is not an obligation. Each obligation requires a non-empty name unique within the account.

Keep an obligation while it remains owed and remove it only after its work has actually been discharged. For a non-empty account, `workingOn` must exactly name the one obligation you are actively working on now; every other obligation remains pending in the Host view. Change `workingOn` when your actual focus changes. For an empty account, use `workingOn=""`. Each accepted call becomes the current account immediately. Later bookkeeping may record consequences, but it does not roll the accepted account back.

Do not emit multiple todowrite calls in the same assistant message; any such batch is rejected entirely.
