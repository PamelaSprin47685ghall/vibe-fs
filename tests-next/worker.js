import { pathToFileURL } from "node:url";

const args = process.argv.slice(2);

globalThis.__resetAssertionTimeout = function() {
  process.send?.({ status: "heartbeat" });
};

if (args[0] === "--discover") {
  const file = args[1];
  import(pathToFileURL(file).href)
    .then((mod) => {
      const exports = [];
      for (const [key, value] of Object.entries(mod)) {
        if (
          typeof value === "function" &&
          !key.startsWith("_") &&
          !value.toString().startsWith("class ") &&
          !key.endsWith("_$ctor") &&
          !key.endsWith("_$reflection") &&
          !key.startsWith("check") &&
          !key.startsWith("contains")
        ) {
          exports.push(key);
        }
      }
      process.send?.({ status: "discovered", exports }, () => process.exit(0));
    })
    .catch((err) => {
      process.send?.({ status: "error", message: err.stack || err.message }, () => process.exit(1));
    });
} else {
  const [file, exportName] = args;
  import(pathToFileURL(file).href)
    .then(async (mod) => {
      const result = await mod[exportName]();
      process.send?.({ status: "ok", result }, () => process.exit(0));
    })
    .catch((error) => {
      process.send?.({ status: "error", message: error.stack || error.message || String(error) }, () => process.exit(1));
    });
}
