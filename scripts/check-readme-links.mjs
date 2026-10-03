// Checks that every relative link and image in README.md points at a file or directory that exists with exactly
// that spelling. GitHub serves paths case-sensitively, while Windows and macOS file systems usually do not, so a
// plain existsSync would pass a link that is broken on GitHub. Each path segment is compared against the real
// entries of its parent directory instead.
//
// Usage: node scripts/check-readme-links.mjs [path/to/README.md]

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const readme = path.resolve(root, process.argv[2] ?? 'README.md')
const text = fs.readFileSync(readme, 'utf8')

// Fenced code blocks can hold Markdown-looking text that is not a link.
const prose = text.replace(/^(`{3,}|~{3,})[^\n]*\n[\s\S]*?^\1[ \t]*$/gm, '')

const targets = new Set()
// Inline links and images: [text](target) and ![alt](target "title").
for (const match of prose.matchAll(/!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g)) targets.add(match[1])
// Reference definitions: [label]: target
for (const match of prose.matchAll(/^\s{0,3}\[[^\]]+\]:\s*<?(\S+?)>?(?:\s+"[^"]*")?\s*$/gm)) targets.add(match[1])
// HTML images and links: <img src="..."> and <a href="...">.
for (const match of prose.matchAll(/<(?:img|a)\b[^>]*?\s(?:src|href)="([^"]+)"/gi)) targets.add(match[1])

const isRelative = (target) => !/^(?:[a-z][a-z0-9+.-]*:|\/\/|#)/i.test(target)

/** True when every segment of the path exists with exactly this case. */
function existsExactly(relativePath) {
  let current = path.dirname(readme)
  for (const segment of relativePath.split('/').filter((s) => s !== '' && s !== '.')) {
    if (segment === '..') {
      current = path.dirname(current)
      continue
    }
    let entries
    try {
      entries = fs.readdirSync(current)
    } catch {
      return false
    }
    if (!entries.includes(segment)) return false
    current = path.join(current, segment)
  }
  return true
}

const broken = []
let checked = 0
for (const target of targets) {
  if (!isRelative(target)) continue
  const filePart = decodeURIComponent(target.split('#')[0].split('?')[0])
  if (filePart === '') continue
  checked++
  if (!existsExactly(filePart)) broken.push(target)
}

if (broken.length > 0) {
  console.error(`${broken.length} broken relative link(s) in ${path.relative(root, readme)} (missing, or wrong case):`)
  for (const target of broken) console.error(`  ${target}`)
  process.exit(1)
}

console.log(`All ${checked} relative link(s) in ${path.relative(root, readme)} resolve with exact case.`)
