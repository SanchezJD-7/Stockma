import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './HowItWorks.module.css'

export function HowItWorks() {
  return (
    <section id='how-it-works' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>De cero a tu inventario bajo control en minutos</LandingSectionTitle>
          <LandingLead>
            Sin configuraciones complejas ni semanas de capacitación. Tres pasos simples y tu
            negocio empieza a ahorrar dinero desde el primer día.
          </LandingLead>
        </LandingSectionHead>
        <ol className={styles.steps}>
          <li className={styles.step}>
            <span className={styles.stepIndex}>1</span>
            <h3 className={shared.cardTitle}>Importa tus datos o empieza desde cero</h3>
            <MutedText>
              Subes tu Excel actual o registras tus productos en segundos escaneando su código de
              barras. Olvídate de migraciones tediosas o formularios infinitos.
            </MutedText>
          </li>
          <li className={styles.step}>
            <span className={styles.stepIndex}>2</span>
            <h3 className={shared.cardTitle}>Asigna lotes, cantidades y vencimientos</h3>
            <MutedText>
              Vincula cada producto con su fecha de caducidad y ubicación exacta. El sistema
              organiza tu stock automáticamente sin que tengas que hacer cálculos a mano.
            </MutedText>
          </li>
          <li className={styles.step}>
            <span className={styles.stepIndex}>3</span>
            <h3 className={shared.cardTitle}>Activa el monitoreo automático 24/7</h3>
            <MutedText>
              Recibe alertas inteligentes antes de que se agoten tus productos estrella o se venza
              la mercancía en estantería. Toma el control total sin trabajar a puerta cerrada.
            </MutedText>
          </li>
        </ol>
      </div>
    </section>
  )
}
