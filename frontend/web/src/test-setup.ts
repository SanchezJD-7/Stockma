import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const tokensCss = readFileSync(resolve(process.cwd(), 'src/styles/tokens.css'), 'utf8')

const style = document.createElement('style')
style.dataset.testTokens = 'true'
style.textContent = tokensCss
document.head.appendChild(style)
