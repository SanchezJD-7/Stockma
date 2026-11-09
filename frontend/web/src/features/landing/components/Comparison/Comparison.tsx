import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import styles from './Comparison.module.css'

export function Comparison() {
  return (
    <section id='comparison' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>
            De los exceles y cuadernos a un inventario automatizado
          </LandingSectionTitle>
          <LandingLead>
            Cansado de los procesos manuales, rutinarios, y agotadores que te quitan tiempo para
            realizar tus actividades extralaborales? Con Stockma puedes tener un software
            automatizado que te evita esos dolores de cabeza y hace todos los trabajos manuales por
            ti
          </LandingLead>
        </LandingSectionHead>

        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Sin Stockma: Procesos manuales y pérdidas</h3>
            <ul className={styles.list}>
              <li>
                <strong>Descontrol de registros:</strong> Hojas físicas y archivos de Excel con
                múltiples versiones.
              </li>
              <li>
                <strong>Revisiones tediosas:</strong> Verificación manual lote por lote al cerrar la
                jornada.
              </li>
              <li>
                <strong>Pérdida de inversión:</strong> Medicamentos e insumos vencidos por falta de
                alertas.
              </li>
              <li>
                <strong>Rotación deficiente:</strong> Errores al despachar el lote más antiguo
                primero.
              </li>
              <li>
                <strong>Cierres con error:</strong> Cuadres manuales que obligan a rehacer cuentas.
              </li>
            </ul>
          </article>

          <article className={`${shared.card} ${styles.card} ${styles.after}`}>
            <h3 className={shared.cardTitle}>Con Stockma: Control total de tu negocio</h3>
            <ul className={styles.list}>
              <li>
                <strong>Inventario en tiempo real:</strong> Una única fuente de datos para todo tu
                equipo.
              </li>
              <li>
                <strong>Alertas automáticas:</strong> Notificaciones a tiempo antes de que un lote
                expire.
              </li>
              <li>
                <strong>Venta por orden de vencimiento:</strong> El sistema te indica cuál es la
                mercancía más vieja que debe salir primero para evitar pérdidas.
              </li>
              <li>
                <strong>Captura rápida:</strong> Escaneo de facturas y códigos de barras en
                segundos.
              </li>
              <li>
                <strong>Auditorías instantáneas:</strong> Reportes limpios para tu contador a un
                solo clic.
              </li>
            </ul>
          </article>
        </div>
      </div>
    </section>
  )
}
