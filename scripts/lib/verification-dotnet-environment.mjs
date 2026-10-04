import fs from 'node:fs'
import path from 'node:path'

export function privateDotnetEnvironment(root, sdkRoot, executable) {
  for (const name of ['home', 'cli', 'config', 'cache', 'data', 'state', 'tmp', 'packages', 'nuget-scratch', 'nuget-plugins']) {
    fs.mkdirSync(path.join(root, name))
  }
  return {
    CI: 'true',
    HOME: path.join(root, 'home'),
    DOTNET_CLI_HOME: path.join(root, 'cli'),
    DOTNET_ROOT: sdkRoot,
    DOTNET_HOST_PATH: executable,
    DOTNET_CLI_UI_LANGUAGE: 'en-US',
    DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DOTNET_NOLOGO: '1',
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: 'true',
    XDG_CONFIG_HOME: path.join(root, 'config'),
    XDG_CACHE_HOME: path.join(root, 'cache'),
    XDG_DATA_HOME: path.join(root, 'data'),
    XDG_STATE_HOME: path.join(root, 'state'),
    TMPDIR: path.join(root, 'tmp'),
    TMP: path.join(root, 'tmp'),
    TEMP: path.join(root, 'tmp'),
    NUGET_PACKAGES: path.join(root, 'packages'),
    NUGET_HTTP_CACHE_PATH: path.join(root, 'cache'),
    NUGET_SCRATCH: path.join(root, 'nuget-scratch'),
    NUGET_PLUGINS_CACHE_PATH: path.join(root, 'nuget-plugins'),
    PATH: sdkRoot,
    LANG: 'C',
    LC_ALL: 'C',
  }
}
