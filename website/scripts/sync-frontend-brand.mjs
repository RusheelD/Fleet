import { mkdir, copyFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)
const websiteRoot = path.resolve(__dirname, '..')
const repoRoot = path.resolve(websiteRoot, '..')
const generatedRoot = path.join(websiteRoot, 'src', 'generated', 'frontend-brand')

const filesToCopy = [
  {
    from: path.join(repoRoot, 'frontend', 'src', 'theme.ts'),
    to: path.join(generatedRoot, 'theme.ts'),
  },
  {
    from: path.join(repoRoot, 'frontend', 'src', 'styles', 'appTokens.ts'),
    to: path.join(generatedRoot, 'appTokens.ts'),
  },
  {
    from: path.join(repoRoot, 'frontend', 'src', 'components', 'shared', 'FleetRocketLogo.tsx'),
    to: path.join(generatedRoot, 'FleetRocketLogo.tsx'),
  },
  {
    from: path.join(repoRoot, 'frontend', 'src', 'index.css'),
    to: path.join(generatedRoot, 'index.css'),
  },
]

await mkdir(generatedRoot, { recursive: true })

for (const file of filesToCopy) {
  await copyFile(file.from, file.to)
}

console.log(`Synced ${filesToCopy.length} frontend brand files into website/src/generated/frontend-brand.`)
