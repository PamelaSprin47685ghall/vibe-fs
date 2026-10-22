# Tool · deliver

End the current incumbency via relay or completion, or proceed to execution when authorized.

## Contract and Usage

1. **Preconditions**:
   - The incumbency must hold no live blocking resources (such as a pending `devops`, background work, or unsettled tool calls). With blockers present, the call is rejected with the blocker list; settle them before delivering.
   - An unanswered user `ask` is an explicit wait, not completion.
   - For delivery, the plan artifact `P` must be present and non-empty; delivery is forbidden while `P` is empty or missing.

2. **Relay vs Completion**:
   - Naming a successor relays the work; naming none completes it.
   - One delivery intent is enough: a single call completes or relays atomically without a double-deliver or confirmation ritual.
   - Review is a recorded collaboration fact, not a precondition: unreviewed relays are permitted and succeed without forcing an assessment.
   - If history needs organizing before handover, supply both top-level programming parameters `prefixCount` and `carry` to keep what the successor needs; the current turn is kept automatically.
   - `deliver` retires this incumbency atomically and opens the successor at the same stage. A retired tenure cannot close or affect the new one.

3. **Delivery Governed by User Grant**:
   - Calling `deliver` with delivery authorization (after `S3` review passes and non-empty `P` is verified) proceeds according to the user's grant:
     - **Plan-Only Grant**: The system closes out the current incumbency and stops the attempt; the retired run is no longer nudged, and the finished plan `P` is returned.
     - **Plan-Then-Execute Grant**: When the user explicitly granted execution authority, the system launches the `S4` (Manager) execution session directly without forcing the user to repeat the decision or ask twice.
   - The delivery path is forbidden without authority; an unauthorized delivery relays rather than delivers.

4. **Idempotency, Replay, and Conflicts**:
   - Retries with the same request identity and fence replay the existing recorded outcome; side effects occur at most once.
   - A different payload under the same request identity or an expected advance mismatch results in a conflict.
   - Late or duplicate receipts do not double-apply.

