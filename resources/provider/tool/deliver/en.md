# Tool · deliver

Promote or complete the current incumbency when eligible, or relay it; deliver performs the promotion itself, and refuses while blockers or obligations remain.

## Contract and Usage

1. **Preconditions**:
   - The incumbency must hold no live blocking resources (such as a pending `devops`, background work, or unsettled tool calls). With blockers present, the call is rejected with the blocker list; settle them before delivering.
   - An unanswered user `ask` is an explicit wait, not completion.
   - For delivery, the plan artifact `P` must be present and non-empty; delivery is forbidden while `P` is empty or missing.

2. **Promotion, Relay, Completion**:
   - Review grants eligibility; `deliver` performs the promotion. When eligible and the obligation ledger is empty, deliver advances the stage (`S1`→`S2`, `S2`→`S3`, `S3`→delivery authority) and its return presents the next stage's method.
   - For an execution (`S4`) tenure, an eligible deliver completes the work; an ineligible deliver (or one naming a successor) relays atomically to a successor at the same stage.
   - `deliver` refuses — returning the outstanding items — while a resource is still blocked or the obligation ledger is not empty. There is no double-deliver and no confirmation ritual.
   - If history needs organizing before handover, supply both top-level programming parameters `prefixCount` and `carry` to keep what the successor needs; the current turn is kept automatically.
   - A retired tenure cannot close or affect the new one.

3. **Delivery Governed by User Grant**:
   - Calling `deliver` with delivery authorization (after `S3` gains authority and non-empty `P` is verified) proceeds according to the user's grant:
     - **Plan-Only Grant**: The system closes out the current incumbency and stops the attempt; the retired run is no longer nudged, and the finished plan `P` is returned.
     - **Plan-Then-Execute Grant**: When the user explicitly granted execution authority, the system launches the `S4` (Manager) execution session directly without forcing the user to repeat the decision or ask twice.
   - The delivery path is forbidden without authority; an unauthorized delivery relays rather than delivers.

4. **Idempotency, Replay, and Conflicts**:
   - Retries with the same request identity and fence replay the existing recorded outcome; side effects occur at most once.
   - A different payload under the same request identity or an expected advance mismatch results in a conflict.
   - Late or duplicate receipts do not double-apply.

