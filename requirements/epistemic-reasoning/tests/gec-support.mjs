import { createHash } from "node:crypto"
import { readFile } from "node:fs/promises"
import path from "node:path"
import { fileURLToPath } from "node:url"

const here = path.dirname(fileURLToPath(import.meta.url))
export const fixture = (...parts) => path.join(here, "fixtures", ...parts)
export const source = (...parts) => path.join(here, "..", "..", "..", "src", "Wanxiangshu", "Sphinx", ...parts)

export async function loadGecSurface() {
  const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js")
  return gecSurface
}

export async function readJson(file) {
  return JSON.parse(await readFile(file, "utf8"))
}

export async function sha256File(file) {
  return createHash("sha256").update(await readFile(file)).digest("hex")
}
