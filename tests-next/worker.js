import { pathToFileURL } from 'node:url';

const [file, exportName] = process.argv.slice(2);

process.channel?.ref();

globalThis.__resetAssertionTimeout = function() {
  process.send?.({ status: 'heartbeat' });
};

import(pathToFileURL(file).href)
  .then(async (mod) => {
    const result = await mod[exportName]();
    process.send?.({ status: 'ok', result });
  })
  .catch((error) => {
    process.send?.({ status: 'error', message: error.stack || error.message || String(error) });
  });
