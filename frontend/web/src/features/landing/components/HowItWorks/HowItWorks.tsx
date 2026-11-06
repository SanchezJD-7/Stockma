import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './HowItWorks.module.css'

export function HowItWorks() {
  return (
    <section id='how-it-works' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>De cero a tu inventario cargado</LandingSectionTitle>
          <LandingLead>Tres pasos y tu equipo ya está trabajando con datos reales.</LandingLead>
        </LandingSectionHead>
        <ol className={styles.steps}>
          <li className={styles.step}>
            <span className={styles.stepIndex}>1</span>
            <h3>Recibe tus credenciales</h3>
            <MutedText>
              Un administrador registra tu negocio en Stockma y te entrega el acceso.
            </MutedText>
          </li>
          <li className={styles.step}>
            <span className={styles.stepIndex}>2</span>
            <h3>Inicia sesión y confirma con SMS</h3>
            <MutedText>
              Correo, contraseña y un código que llega a tu celular. Blindaje extra de seguridad
              para que únicamente tus colaboradores tengan acceso a tu negocio.
            </MutedText>
          </li>
          <li className={styles.step}>
            <span className={styles.stepIndex}>3</span>
            <h3>Carga productos, lotes y fechas</h3>
            <MutedText>
              Con su nombre y su código de barras, cada producto queda con su lote y su vencimiento.
            </MutedText>
          </li>
        </ol>
      </div>
    </section>
  )
}
