#!/bin/zsh
cd /Users/yuanxi/Workwork/vibe-fs || exit 1
/usr/bin/env -u WXS_TIER_INTEGRATION -u WXS_TIER_RELEASE \
  PATH="/private/tmp/node-v22.23.3-darwin-arm64/bin:/opt/homebrew/Cellar/dotnet/10.0.302/libexec:$PATH" \
  WXS_VERIFICATION_NPM_CLI=/private/tmp/vibe-fs-t388-selected-npm/package/bin/npm-cli.js \
  WXS_VERIFICATION_DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.302/libexec \
  DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.302/libexec \
  NODE_TEST_CONCURRENCY=2 \
  UNIT_VERDICT_SILENCE_MS=30000 \
  TESTS_MJS_FILES=requirements/verification-system/tests/016.test.mjs \
  /private/tmp/node-v22.23.3-darwin-arm64/bin/node requirements/verification-system/tests/run.mjs \
  > /private/tmp/vibe-fs-w1-016-gen295-20261007/unit.log 2>&1
w1_exit=$?
printf '%s\n' "$w1_exit" > /private/tmp/vibe-fs-w1-016-gen295-20261007/exit-code.txt
exit "$w1_exit"
