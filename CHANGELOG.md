# Changelog

## 0.4.0-rc.3-dev — development only

This marker is **not a release candidate for distribution**. It tracks internal work on the 0.4.0 critical path before a real `0.4.0-rc.3` gate.

### Status

- Prompt Authority / Logical Run / PromptDispatcher types and journal facts exist, but production wiring is incomplete.
- Manager typed completion and full Host loop are not yet release-ready.
- Companion eligibility, Review physical confirmation, and Logical-Run Fallback A/A/B/B remain open.

### Frozen product rules for this track

- Fallback belongs to a Logical Run. A new Authority Root starts a new Fallback epoch (`Failures = 0`, `Side = A`).
- New human prompts that omit model inherit `LastAuthorityProfile.BaseModel`, never the previous run's Side B EffectiveModel.
- Companion eligibility reads only `ActiveLogicalRun.Profile.Agent`.
- Plugin user-shaped messages must go through a single runtime `PromptAuthorityService`.

### Still blocking a real RC.3

- Single PromptAuthorityService + AgentOwnerRoot two-phase send
- Typed join JSON without F# Result arrays
- Manager full-loop Host E2E
- Companion / Review / Fallback product canaries
- Clean package install and three-round release gate

## 0.4.0-rc.2 / 0.4.0-rc.1 — historical development markers

Earlier `rc.1` / `rc.2` labels were development markers only. Do not treat `docs/RC-0.4.0-rc.2.md` as a green release gate.

## Distribution policy

The package remains private (`private: true`) under the provisional commercial license in `LICENSE` (`license: SEE LICENSE IN LICENSE`). It must not be published to the public npm registry. Any external distribution requires a signed commercial agreement and a real release gate with clean-checkout evidence.
