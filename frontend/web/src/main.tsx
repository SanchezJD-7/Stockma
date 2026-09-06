import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider } from '@mui/material/styles'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import './styles.css'
import { createMuiBridge } from './styles/mui-bridge.ts'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={createMuiBridge()}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  </StrictMode>,
)
