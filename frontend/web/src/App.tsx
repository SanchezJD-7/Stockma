import { AuthFlow } from './features/auth'
import { LandingPage } from './features/landing'
import { Toaster } from './styles/components/Toaster'

function App() {
  const pathname = window.location.pathname.replace(/\/+$/, '') || '/'
  const page = pathname === '/login' ? <AuthFlow /> : <LandingPage />

  return (
    <>
      {page}
      <Toaster />
    </>
  )
}

export default App
