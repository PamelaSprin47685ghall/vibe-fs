# Remaining No-Go after rc.6 seal

| Item | State |
|---|---|
| ReviewConfirmation as HumanRoot | **Closed** — live orch dump: reviewer roots AgentOwnerRoot; confirmations PluginPromptClaimed ReviewConfirmation |
| resolveForSession A/A/B/B decision | **Closed** unit/integration path |
| Provider-visible same-run A→A→B→B **requests** | **Open** — host may not re-call provider after non-retryable APIError; PluginFallbackRetry claims EffectiveModel but wire request may not fire |
| RC observation | **Open** — criteria ready |
| Final 0.4.0 cut + second gate | **Open** |
