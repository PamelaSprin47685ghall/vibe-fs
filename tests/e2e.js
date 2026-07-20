process.on("unhandledRejection", (reason, promise) => {
  console.error("UNHANDLED REJECTION AT:", promise, "REASON:", reason);
});

const target = process.argv[2];

let runAll;
if (target === "mux") {
  runAll = (await import("../build/e2e/MuxTests.js")).runAll;
} else if (target === "omp") {
  runAll = (await import("../build/e2e/TestsOmp.js")).runAll;
} else if (target === "wanxiangzhen") {
  runAll = (await import("../build/e2e/WanxiangzhenPluginTests.js")).runAll;
} else if (target === "opencode-e2e-p0") {
  runAll = async (args) => {
    console.log("\n--- Running opencode e2e P0 canary suite ---");
    const { spawnSync } = await import("node:child_process");
    const result = spawnSync(
      "node",
      [new URL("../e2e/opencode/specs/p0-canary.js", import.meta.url).pathname],
      {
        stdio: "inherit",
        cwd: new URL("..", import.meta.url).pathname,
      },
    );
    const code = result.status ?? 1;
    console.log(`opencode e2e P0 canary suite exited with code ${code}`);
    return code;
  };
} else if (target === "opencode-e2e-full") {
  runAll = async (args) => {
    console.log("\n--- Running opencode e2e full suite ---");
    const { spawnSync } = await import("node:child_process");
    const result = spawnSync(
      "node",
      [new URL("../e2e/opencode/specs/full.js", import.meta.url).pathname],
      {
        stdio: "inherit",
        cwd: new URL("..", import.meta.url).pathname,
      },
    );
    const code = result.status ?? 1;
    console.log(`opencode e2e full suite exited with code ${code}`);
    return code;
  };
} else if (target === "core") {
  runAll = (await import("../build/e2e/Tests.js")).runAll;
} else if (target === "git-hooks") {
  runAll = async (args) => {
    console.log("\nRunning GitHookFormatterTests...");
    try {
      const { runAll: hookRun } = await import("./GitHookFormatterTests.js");
      return await hookRun(args);
    } catch (e) {
      console.error("Failed to import or run GitHookFormatterTests:", e);
      return 1;
    }
  };
} else {
  // Default E2E run: the old OpencodePluginTests/MimocodePluginTests/MimoTuiPluginTests
  // pseudo-E2E suites have been removed from E2E statistics. They remain as
  // integration-level contract tests and should be run under the integration
  // runner once that separation is fully wired.
  runAll = async (args) => {
    let totalFailed = 0;

    console.log("\n--- Running e2e core ---");
    const { runAll: coreRun } = await import("../build/e2e/Tests.js");
    totalFailed += await coreRun(args);

    console.log("\n--- Running e2e mux ---");
    const { runAll: muxRun } = await import("../build/e2e/MuxTests.js");
    totalFailed += await muxRun(args);

    console.log("\n--- Running e2e omp ---");
    const { runAll: ompRun } = await import("../build/e2e/TestsOmp.js");
    totalFailed += await ompRun(args);

    console.log("\n--- Running e2e wanxiangzhen ---");
    const { runAll: wanRun } =
      await import("../build/e2e/WanxiangzhenPluginTests.js");
    totalFailed += await wanRun(args);

    console.log("\n--- Running opencode e2e P0 canary suite ---");
    const { spawnSync } = await import("node:child_process");
    const p0result = spawnSync(
      "node",
      [new URL("../e2e/opencode/specs/p0-canary.js", import.meta.url).pathname],
      {
        stdio: "inherit",
        cwd: new URL("..", import.meta.url).pathname,
      },
    );
    totalFailed += p0result.status ?? 1;
    console.log(
      `opencode e2e P0 canary suite exited with code ${p0result.status}`,
    );

    console.log("\n--- Running GitHookFormatterTests ---");
    try {
      const { runAll: hookRun } = await import("./GitHookFormatterTests.js");
      totalFailed += await hookRun(args);
    } catch (e) {
      console.error("Failed to run GitHookFormatterTests:", e);
      totalFailed++;
    }

    return totalFailed;
  };
}

const extraArgs = process.argv.slice(3);
console.log("e2e runner started");
runAll(extraArgs)
  .then(async (code) => {
    console.log("e2e runner finished with code:", code);
    try {
      const { hostSingletonManager } =
        await import("../e2e/harness-bootstrap.js");
      await hostSingletonManager.teardownAll();
    } catch (e) {
      console.error("Teardown failed:", e);
    }
    process.exit(code);
  })
  .catch(async (err) => {
    console.error("RUNALL_FAILED:", err);
    try {
      const { hostSingletonManager } =
        await import("../e2e/harness-bootstrap.js");
      await hostSingletonManager.teardownAll();
    } catch (e) {
      console.error("Teardown failed on catch:", e);
    }
    process.exit(2);
  });
