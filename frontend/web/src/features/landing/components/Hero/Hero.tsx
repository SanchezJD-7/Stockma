import { PrimaryButton } from '../../../../styles/components/buttons'
import styles from './Hero.module.css'
import { HeroBody, HeroTitle } from './Hero.styles'
import { HeroMockTable } from './HeroMockTable'

export function Hero() {
  return (
    <section id='hero' className={styles.root}>
      <div className={styles.layout}>
        <div className={styles.copy}>
          <p className={styles.eyebrow}>Stockma</p>
          <HeroTitle>
            Automatiza tu inventario y <em>deja las planillas atrás</em>
          </HeroTitle>
          <HeroBody>
            Stockma es la plataforma de inventarios ideal para tu negocio: cada lote con su
            vencimiento, cada producto con su código, y el trabajo repetitivo en manos del sistema,
            no en las tuyas.
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
