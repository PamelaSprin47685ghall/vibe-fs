#!/usr/bin/env python3
"""Update wanxiangshu.fsproj with correct ordering:
- integration/*.fs + wrapper must come BEFORE tests/Tests.fs
- Budget E2E can come after the e2e block
Idempotent: does not duplicate entries if run twice."""
import os, re

base = '/home/kunweiz/Desktop/vibe/worktree-e2e'
fsproj = os.path.join(base, 'wanxiangshu.fsproj')

OC = "Open" + "code"  # 'Opencode'

with open(fsproj) as f:
    content = f.read()

changes = []

# ── 1. Integration files + wrapper before tests/Tests.fs ────────────────────
integration_entries = []
for suffix in [
    'PluginContractTestsPart2',
    'PluginContractTestsPart3',
    'PluginContractTestsPart4',
    'ContinueContractTests',
    'NudgeHostIntegrationTests',
]:
    fname = OC + suffix + '.fs'
    integration_entries.append(f'    <Compile Include="integration/{fname}"/>')

integration_entries.append('    <Compile Include="tests/IntegrationOpenCodeContractTests.fs"/>')
integration_block = '\n'.join(integration_entries) + '\n'

tests_fs = '    <Compile Include="tests/Tests.fs"/>'
if tests_fs in content and 'IntegrationOpenCodeContractTests' not in content:
    content = content.replace(tests_fs, integration_block + tests_fs)
    changes.append('Inserted integration + wrapper before tests/Tests.fs')

# ── 2. Budget E2E after last e2e entry ──────────────────────────────────────
budget_entries = []
for suffix in ['PluginContextBudgetTests', 'PluginContextBudgetE2e']:
    fname = OC + suffix + '.fs'
    budget_entries.append(f'    <Compile Include="e2e/{fname}"/>')

budget_block = '\n'.join(budget_entries) + '\n'

# Find all e2e Compile entries, take the last one's position
last_e2e_match = None
for m in re.finditer(r'    <Compile Include="e2e/\w+\.fs"/>', content):
    last_e2e_match = m

if last_e2e_match and 'OpencodePluginContextBudgetTests' not in content:
    insert_pos = last_e2e_match.end()
    content = content[:insert_pos] + '\n' + budget_block + content[insert_pos:]
    changes.append('Inserted Budget E2E after last e2e entry')

with open(fsproj, 'w') as f:
    f.write(content)

for c in changes:
    print(c)

# ── Show resulting order around Tests.fs and Budget ──────────────────────────
lines = content.split('\n')
print(f"\nTotal lines: {len(lines)}")

# Find and show Tests.fs area
for i, line in enumerate(lines):
    if 'Tests.fs' in line or 'integration/' in line or ('Budget' in line and 'e2e/' in line):
        marker = '>>>' if 'IntegrationOpenCode' in line or 'Budget' in line else '   '
        print(f"  {marker} L{i+1}: {line}")
