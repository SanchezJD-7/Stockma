import { useEffect, useState } from 'react'
import { PrimaryButton } from '../../../../styles/components/buttons'
import shared from '../../landing.module.css'
import styles from './WaitlistSection.module.css'
import { WaitlistBody, WaitlistTitle } from './WaitlistSection.styles'

export function WaitlistSection() {
  const [hidden, setHidden] = useState(false)

  useEffect(() => {
    const demoElement = document.getElementById('demo')
    if (!demoElement || typeof window.IntersectionObserver !== 'function') return

    const observer = new window.IntersectionObserver(
      ([entry]) => {
        setHidden(entry.isIntersecting)
      },
      {
        threshold: 0.1,
      },
    )

    observer.observe(demoElement)

    return () => {
      observer.disconnect()
    }
  }, [])

  if (hidden) return null

  return (
    <aside id='waitlist' aria-label='Lista de espera' className={styles.root}>
      <div className={`${shared.wrap} ${styles.inner}`}>
        <div>
          <WaitlistTitle>Únete a la lista de espera</WaitlistTitle>
          <WaitlistBody>
            Rellena el formulario y recibe en primicia información de nuestro lanzamiento
          </WaitlistBody>
        </div>
        <PrimaryButton className={styles.cta} type='button'>
          Unirme a la lista de espera
        </PrimaryButton>
      </div>
    </aside>
  )
}
