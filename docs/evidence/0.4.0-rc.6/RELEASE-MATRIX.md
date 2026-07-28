# 0.4.0-rc.6 Release Matrix

| Gate | Result |
|---|---|
| `git clean -xfd && npm ci` | pass |
| `npm run build` | pass |
| `tests-next` | 281 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=3` (17 canaries) | pass |
| `npm pack ./build` | pass (`wanxiangshu-0.4.0-rc.6.tgz`) |
| empty-dir install + `import('wanxiangshu')` | pass |
| package version / private / LICENSE / prompts / Plugin entry | pass |

## Production fixes vs rc.5

- Prompt correlation via text-part metadata + unique pending claim
- ReviewConfirmation is continuation (not HumanRoot)
- session.error ProviderError path + dual-source subscription
- GuardPromptAccepted written once (HostReviewGuard only)

## Still blocking final 0.4.0

- RC observation period
- Provider-visible same-run A→A→B→B **request** trajectory under host re-prompt
- Final version cut + second clean gate
- Private distribution default
