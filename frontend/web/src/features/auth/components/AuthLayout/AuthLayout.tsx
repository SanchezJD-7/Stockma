import type { ReactNode } from 'react'
import logoUrl from '../../../../assets/Stockma_logo.png'
import { Backdrop, Brand, Card, Logo } from './AuthLayout.styles'

interface AuthLayoutProps {
  children: ReactNode
}

export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <Backdrop>
      <Card>
        <Brand>
          <Logo src={logoUrl} alt='Stockma' />
        </Brand>
        {children}
      </Card>
    </Backdrop>
  )
}
