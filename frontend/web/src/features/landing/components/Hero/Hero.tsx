import { useState, useEffect } from 'react'
import { PrimaryButton } from '../../../../styles/components/buttons'
import styles from './Hero.module.css'
import { HeroBody, HeroTitle } from './Hero.styles'
import { HeroMockTable } from './HeroMockTable'

const ROTATING_TEXTS = [
  'deja las planillas atrás',
  'olvídate de los excel',
  'dile adiós a las libretas',
]

export function Hero() {
  const [index, setIndex] = useState(0)
  const [fadeState, setFadeState] = useState<'in' | 'out'>('in')

  useEffect(() => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return

    let transitionTimeout: number | undefined
    const interval = setInterval(() => {
      setFadeState('out')
      transitionTimeout = window.setTimeout(() => {
        setIndex((prevIndex) => (prevIndex + 1) % ROTATING_TEXTS.length)
        setFadeState('in')
      }, 400)
    }, 3000)

    return () => {
      clearInterval(interval)
      if (transitionTimeout !== undefined) window.clearTimeout(transitionTimeout)
    }
  }, [])

  return (
    <section id='hero' className={styles.root}>
      <div className={styles.layout}>
        <div className={styles.copy}>
          <HeroTitle>
            Automatiza tu inventario y{' '}
            <span className={`${styles.animatedText} ${styles[fadeState]}`}>
              {ROTATING_TEXTS[index]}
            </span>
          </HeroTitle>
          <HeroBody>
            Stockma detecta y frena las pérdidas silenciosas de tu negocio antes de que ocurran: te
            avisa qué productos están por vencerse para que no pongas de tu bolsillo, te libera del
            conteo manual y deja que el sistema maneje el stock por ti, mientras tú te enfocas en
            vender y hacer crecer tu negocio.
          </HeroBody>
          <div className={styles.actions}>
            <PrimaryButton className={styles.cta} type='button'>
              Únete a la lista de espera
            </PrimaryButton>
            <a className={styles.cta} href='#how-it-works'>
              Ver cómo funciona
            </a>
          </div>
        </div>
        <HeroMockTable />
      </div>
    </section>
  )
}
