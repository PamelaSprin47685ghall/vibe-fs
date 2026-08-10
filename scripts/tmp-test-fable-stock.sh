#!/usr/bin/env bash
set -euo pipefail
cd /home/kunweiz/Desktop/vibe/wanxiangshu

python3 -c '
from pathlib import Path
p = Path("src/Wanxiangshu/Wanxiangshu.fsproj")
text = p.read_text()
for line in [
    "<Compile Include=\"Kernel/CausalWait.fs\"/>\n",
    "<Compile Include=\"Session/CausalWaitBridge.fs\"/>\n",
    "<Compile Include=\"Session/CausalWaitRegistry.fs\"/>\n",
    "<Compile Include=\"Session/CausalAwait.fs\"/>\n",
]:
    text = text.replace(line, "")
p.write_text(text)
print("stripped")
'

git checkout -- \
  src/Wanxiangshu/Session/StudentTeacherRuntime.fs \
  src/Wanxiangshu/Application/Orchestration/ManagerJob.fs \
  src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityController.fs \
  src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs

set +e
npm run build
echo BUILD_EXIT:$?
set -e

git checkout -- src/Wanxiangshu/Wanxiangshu.fsproj
python3 -c '
from pathlib import Path
p = Path("src/Wanxiangshu/Wanxiangshu.fsproj")
text = p.read_text()
text = text.replace(
    "<Compile Include=\"Kernel/AsyncSupport.fs\"/>\n    <Compile Include=\"Kernel/Outcome.fs\"/>",
    "<Compile Include=\"Kernel/AsyncSupport.fs\"/>\n    <Compile Include=\"Kernel/CausalWait.fs\"/>\n    <Compile Include=\"Kernel/Outcome.fs\"/>",
)
text = text.replace(
    "<Compile Include=\"Session/JoinInterruptRegistry.fs\"/>\n    <Compile Include=\"Session/ForkRecovery.fs\"/>",
    "<Compile Include=\"Session/JoinInterruptRegistry.fs\"/>\n    <Compile Include=\"Session/CausalWaitBridge.fs\"/>\n    <Compile Include=\"Session/CausalWaitRegistry.fs\"/>\n    <Compile Include=\"Session/CausalAwait.fs\"/>\n    <Compile Include=\"Session/ForkRecovery.fs\"/>",
)
p.write_text(text)
print("causal includes restored")
'
