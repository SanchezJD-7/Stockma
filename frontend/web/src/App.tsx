import { AuthFlow } from './features/auth'
import { LandingPage } from './features/landing'

function App() {
  const pathname = window.location.pathname.replace(/\/+$/, '') || '/'

  if (pathname === '/login') {
    return <AuthFlow />
  }

  return <LandingPage />
}

export default App
