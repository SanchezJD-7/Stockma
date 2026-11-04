import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider } from '@mui/material/styles'
import { StrictMode, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import './styles.css'
import { createMuiBridge } from './styles/theme/mui-bridge.ts'

function Root() {
  const [theme, setTheme] = useState(() => createMuiBridge())

  useEffect(() => {
    const onThemeChange = () => setTheme(createMuiBridge())
    window.addEventListener('stockma-theme-change', onThemeChange)
    return () => window.removeEventListener('stockma-theme-change', onThemeChange)
  }, [])

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Root />
  </StrictMode>,
)
